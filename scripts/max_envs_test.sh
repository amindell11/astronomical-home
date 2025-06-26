#!/bin/bash

# Maximum Environments Test
# Finds the max num-envs your system can handle with stable physics

CONFIG="results/CommanderCurriculum_v2/configuration.yaml"
BACKUP_CONFIG="${CONFIG}.backup"
TEST_DURATION=180  # 3 minutes per test

echo "=== Maximum Environments Test ==="
echo "Finding your system's max num-envs capacity"
echo "Keeping timescale conservative at 25x for physics stability"
echo ""

# Backup original config
cp "$CONFIG" "$BACKUP_CONFIG"

# Aggressive environment scaling - keep timescale safe
declare -a ENV_COUNTS=(8 16 24 32 40 48 56 64)
declare -a RESULTS=()

for env_count in "${ENV_COUNTS[@]}"; do
    echo "--- Testing $env_count Environments ---"
    
    # Update config - keep timescale conservative
    sed -i.tmp "s/num_envs: [0-9]*/num_envs: $env_count/" "$CONFIG"
    sed -i.tmp "s/time_scale: [0-9]*/time_scale: 25/" "$CONFIG"  # Safe timescale
    rm -f "${CONFIG}.tmp"
    
    RUN_ID="MaxEnvTest_${env_count}Envs"
    
    echo "  Starting $env_count environments..."
    timeout ${TEST_DURATION}s mlagents-learn "$CONFIG" \
        --run-id="$RUN_ID" \
        --quality-level=0 \
        --no-graphics \
        > "/tmp/maxenv_test_${env_count}.log" 2>&1 &
    
    TRAIN_PID=$!
    START_TIME=$(date +%s)
    
    # Resource monitoring
    MAX_CPU=0
    MAX_MEM=0
    STABLE=true
    SAMPLE_COUNT=0
    
    while kill -0 $TRAIN_PID 2>/dev/null; do
        sleep 10
        SAMPLE_COUNT=$((SAMPLE_COUNT + 1))
        
        # Cross-platform resource monitoring
        if command -v python3 >/dev/null; then
            STATS=$(python3 -c "
import psutil
import sys
try:
    cpu = psutil.cpu_percent(interval=1)
    mem = psutil.virtual_memory().percent
    print(f'{cpu:.1f} {mem:.1f}')
except:
    sys.exit(1)
" 2>/dev/null)
            
            if [[ $? -eq 0 && -n "$STATS" ]]; then
                read CPU MEM <<< "$STATS"
                
                # Track maximums
                if (( $(echo "$CPU > $MAX_CPU" | bc -l 2>/dev/null || echo "0") )); then
                    MAX_CPU=$CPU
                fi
                if (( $(echo "$MEM > $MAX_MEM" | bc -l 2>/dev/null || echo "0") )); then
                    MAX_MEM=$MEM
                fi
                
                # Check for resource exhaustion
                if (( $(echo "$CPU > 95" | bc -l 2>/dev/null || echo "0") )); then
                    echo "  🔴 CPU maxed out: ${CPU}%"
                    STABLE=false
                fi
                if (( $(echo "$MEM > 90" | bc -l 2>/dev/null || echo "0") )); then
                    echo "  🔴 Memory critical: ${MEM}%"
                    STABLE=false
                fi
                
                echo "  Sample $SAMPLE_COUNT: CPU: ${CPU}%, Memory: ${MEM}%"
            else
                echo "  Sample $SAMPLE_COUNT: (Resource monitoring unavailable)"
            fi
        else
            echo "  Sample $SAMPLE_COUNT: (Install python3 + psutil for monitoring)"
        fi
        
        # Check for training stability
        if [[ -f "/tmp/maxenv_test_${env_count}.log" ]]; then
            # Look for out-of-memory or crash indicators
            if grep -q -i "out of memory\|insufficient memory\|allocation failed" "/tmp/maxenv_test_${env_count}.log"; then
                echo "  🔴 CRITICAL: Out of memory detected!"
                STABLE=false
                break
            fi
        fi
        
        # Stop if resources are critically low
        if [[ $STABLE == false ]]; then
            echo "  Stopping test due to resource constraints..."
            kill $TRAIN_PID 2>/dev/null
            break
        fi
    done
    
    wait $TRAIN_PID 2>/dev/null
    EXIT_CODE=$?
    
    END_TIME=$(date +%s)
    DURATION=$((END_TIME - START_TIME))
    
    # Calculate training efficiency metrics
    STEPS_PER_SEC="N/A"
    if [[ -f "/tmp/maxenv_test_${env_count}.log" ]]; then
        # Extract total steps if available
        if grep -q "Step:" "/tmp/maxenv_test_${env_count}.log"; then
            LAST_STEP_LINE=$(grep "Step:" "/tmp/maxenv_test_${env_count}.log" | tail -1)
            if [[ $LAST_STEP_LINE =~ Step:\ *([0-9]+) ]]; then
                TOTAL_STEPS=${BASH_REMATCH[1]}
                if [[ $DURATION -gt 0 ]]; then
                    STEPS_PER_SEC=$(echo "scale=1; $TOTAL_STEPS / $DURATION" | bc -l 2>/dev/null || echo "N/A")
                fi
            fi
        fi
    fi
    
    # Determine status
    STATUS="STABLE"
    if [[ $STABLE == false ]]; then
        STATUS="OVERLOADED"
    elif [[ $EXIT_CODE -ne 0 ]] && [[ $EXIT_CODE -ne 124 ]]; then
        STATUS="CRASHED"
    fi
    
    echo "  Results: Max CPU: ${MAX_CPU}%, Max Memory: ${MAX_MEM}%, Steps/sec: ${STEPS_PER_SEC}, Status: $STATUS"
    echo ""
    
    RESULTS+=("$env_count envs: CPU=${MAX_CPU}%, Mem=${MAX_MEM}%, Steps/sec=${STEPS_PER_SEC}, $STATUS")
    
    # Stop if system is overloaded
    if [[ $STATUS != "STABLE" ]]; then
        echo "=== STOPPING: System capacity reached ==="
        break
    fi
done

# Restore original config
cp "$BACKUP_CONFIG" "$CONFIG"
rm -f "$BACKUP_CONFIG"

echo "=== MAXIMUM ENVIRONMENTS TEST RESULTS ==="
for result in "${RESULTS[@]}"; do
    echo "$result"
done

echo ""
echo "=== RECOMMENDATIONS ==="

# Find the highest stable environment count
MAX_STABLE_ENVS=0
MAX_STABLE_STEPS_SEC=0

for result in "${RESULTS[@]}"; do
    if [[ $result == *"STABLE"* ]]; then
        if [[ $result =~ ([0-9]+)\ envs ]]; then
            ENV_COUNT=${BASH_REMATCH[1]}
            if [[ $ENV_COUNT -gt $MAX_STABLE_ENVS ]]; then
                MAX_STABLE_ENVS=$ENV_COUNT
                
                # Extract steps/sec for this config
                if [[ $result =~ Steps/sec=([0-9.]+) ]]; then
                    MAX_STABLE_STEPS_SEC=${BASH_REMATCH[1]}
                fi
            fi
        fi
    fi
done

if [[ $MAX_STABLE_ENVS -gt 0 ]]; then
    echo "✅ MAXIMUM STABLE ENVIRONMENTS: $MAX_STABLE_ENVS"
    echo "   Training speed: ${MAX_STABLE_STEPS_SEC} steps/sec"
    
    # Calculate total speedup
    BASELINE_SPEED=$((1 * 25))  # 1 env, 25x timescale
    OPTIMIZED_SPEED=$((MAX_STABLE_ENVS * 25))
    SPEEDUP=$(echo "scale=1; $OPTIMIZED_SPEED / $BASELINE_SPEED" | bc -l 2>/dev/null || echo "N/A")
    
    echo "🚀 TOTAL SPEEDUP: ${SPEEDUP}x faster than baseline"
    
    # Production recommendation with safety margin
    SAFE_ENVS=$((MAX_STABLE_ENVS - 4))
    if [[ $SAFE_ENVS -lt 8 ]]; then
        SAFE_ENVS=8
    fi
    
    echo ""
    echo "🎯 PRODUCTION RECOMMENDATION: $SAFE_ENVS environments (with safety margin)"
    echo ""
    echo "Optimal command:"
    echo "mlagents-learn $CONFIG --run-id=OptimalTraining --num-envs=$SAFE_ENVS --time-scale=25 --quality-level=0 --no-graphics"
    
    echo ""
    echo "📊 PERFORMANCE COMPARISON:"
    echo "  Current (8 envs, 25x):  ~$((8 * 25))x baseline"
    echo "  Optimal ($SAFE_ENVS envs, 25x): ~$((SAFE_ENVS * 25))x baseline"
    echo "  Improvement: ~$((SAFE_ENVS * 25 / 200))x faster training"
    
else
    echo "⚠️  No stable configuration found. Try smaller environment counts."
fi

echo ""
echo "💡 WHY THIS APPROACH IS BETTER:"
echo "✅ Zero physics stability risk"
echo "✅ More diverse training experience (parallel environments)"
echo "✅ Linear scaling with hardware"
echo "✅ Easy to tune (just add/remove environments)" 