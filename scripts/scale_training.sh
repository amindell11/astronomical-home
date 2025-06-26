#!/bin/bash

# Systematic Performance Scaling Script
# Gradually increases num-envs and timescale to find optimal settings

CONFIG="results/CommanderCurriculum_v2/configuration.yaml"
BACKUP_CONFIG="${CONFIG}.backup"
TEST_DURATION=180  # 3 minutes per test

echo "=== ML-Agents Performance Scaling Test ==="
echo "Starting from your current low usage (20% CPU, 50% Memory)"
echo "Testing duration: $TEST_DURATION seconds per configuration"
echo ""

# Backup original config
cp "$CONFIG" "$BACKUP_CONFIG"

# Test configurations - progressively more aggressive
declare -a TEST_CONFIGS=(
    "16 35"   # Current recommendation
    "20 35"   # More environments
    "24 35"   # Even more environments  
    "16 40"   # Higher timescale
    "20 40"   # Both increased
    "24 40"   # Aggressive combo
    "32 35"   # Many environments
    "24 45"   # High timescale
)

declare -a RESULTS=()

for config in "${TEST_CONFIGS[@]}"; do
    read NUM_ENVS TIME_SCALE <<< "$config"
    
    echo "--- Testing $NUM_ENVS environments at ${TIME_SCALE}x timescale ---"
    
    # Update config file
    sed -i.tmp "s/num_envs: [0-9]*/num_envs: $NUM_ENVS/" "$CONFIG"
    sed -i.tmp "s/time_scale: [0-9]*/time_scale: $TIME_SCALE/" "$CONFIG"
    rm -f "${CONFIG}.tmp"
    
    RUN_ID="ScaleTest_E${NUM_ENVS}_T${TIME_SCALE}"
    
    # Start training
    echo "  Starting training..."
    timeout ${TEST_DURATION}s mlagents-learn "$CONFIG" \
        --run-id="$RUN_ID" \
        --quality-level=0 \
        --no-graphics \
        > "/tmp/scale_test_${NUM_ENVS}_${TIME_SCALE}.log" 2>&1 &
    
    TRAIN_PID=$!
    START_TIME=$(date +%s)
    
    # Monitor performance every 10 seconds
    MAX_CPU=0
    MAX_MEM=0
    SAMPLE_COUNT=0
    STABLE=true
    
    while kill -0 $TRAIN_PID 2>/dev/null; do
        sleep 10
        SAMPLE_COUNT=$((SAMPLE_COUNT + 1))
        
        # Get system stats (cross-platform approach)
        if command -v python3 >/dev/null; then
            STATS=$(python3 -c "
import psutil
cpu = psutil.cpu_percent(interval=1)
mem = psutil.virtual_memory().percent
print(f'{cpu:.1f} {mem:.1f}')
" 2>/dev/null)
            
            if [[ -n "$STATS" ]]; then
                read CPU MEM <<< "$STATS"
                
                # Track maximums
                if (( $(echo "$CPU > $MAX_CPU" | bc -l 2>/dev/null || echo "0") )); then
                    MAX_CPU=$CPU
                fi
                if (( $(echo "$MEM > $MAX_MEM" | bc -l 2>/dev/null || echo "0") )); then
                    MAX_MEM=$MEM
                fi
                
                # Check for instability
                if (( $(echo "$CPU > 95" | bc -l 2>/dev/null || echo "0") )) || (( $(echo "$MEM > 90" | bc -l 2>/dev/null || echo "0") )); then
                    echo "  WARNING: High resource usage detected (CPU: ${CPU}%, MEM: ${MEM}%)"
                    STABLE=false
                fi
                
                echo "  Sample $SAMPLE_COUNT: CPU: ${CPU}%, Memory: ${MEM}%"
            fi
        else
            echo "  Sample $SAMPLE_COUNT: (Install python3 + psutil for detailed monitoring)"
        fi
    done
    
    wait $TRAIN_PID
    EXIT_CODE=$?
    
    END_TIME=$(date +%s)
    DURATION=$((END_TIME - START_TIME))
    
    # Check for training errors
    if [[ $EXIT_CODE -ne 0 ]] && [[ $EXIT_CODE -ne 124 ]]; then  # 124 is timeout
        echo "  ERROR: Training crashed (exit code: $EXIT_CODE)"
        STABLE=false
    fi
    
    # Extract training metrics if available
    STEPS_PER_SEC="N/A"
    EPISODES="N/A"
    if [[ -f "/tmp/scale_test_${NUM_ENVS}_${TIME_SCALE}.log" ]]; then
        # Look for steps per second in the log
        if grep -q "Step:" "/tmp/scale_test_${NUM_ENVS}_${TIME_SCALE}.log"; then
            LAST_STEP_LINE=$(grep "Step:" "/tmp/scale_test_${NUM_ENVS}_${TIME_SCALE}.log" | tail -1)
            if [[ $LAST_STEP_LINE =~ Step:\ *([0-9]+) ]]; then
                TOTAL_STEPS=${BASH_REMATCH[1]}
                STEPS_PER_SEC=$(echo "scale=1; $TOTAL_STEPS / $DURATION" | bc -l 2>/dev/null || echo "N/A")
            fi
        fi
    fi
    
    # Determine overall stability
    STATUS="STABLE"
    if [[ $STABLE == false ]]; then
        STATUS="UNSTABLE"
    fi
    
    echo "  Results: Max CPU: ${MAX_CPU}%, Max Memory: ${MAX_MEM}%, Steps/sec: ${STEPS_PER_SEC}, Status: $STATUS"
    echo ""
    
    RESULTS+=("$NUM_ENVS envs, ${TIME_SCALE}x time: CPU=${MAX_CPU}%, Mem=${MAX_MEM}%, Steps/sec=${STEPS_PER_SEC}, $STATUS")
    
    # Stop testing if we hit resource limits
    if [[ $STABLE == false ]]; then
        echo "=== STOPPING: Resource limits reached ==="
        break
    fi
done

# Restore original config
cp "$BACKUP_CONFIG" "$CONFIG"
rm -f "$BACKUP_CONFIG"

echo "=== SCALING TEST RESULTS ==="
for result in "${RESULTS[@]}"; do
    echo "$result"
done

echo ""
echo "=== RECOMMENDATIONS ==="

# Find the best stable configuration
BEST_STABLE=""
BEST_SCORE=0

for result in "${RESULTS[@]}"; do
    if [[ $result == *"STABLE"* ]]; then
        # Extract num_envs and time_scale for scoring
        if [[ $result =~ ([0-9]+)\ envs,\ ([0-9]+)x ]]; then
            ENV_COUNT=${BASH_REMATCH[1]}
            TIME_SCALE=${BASH_REMATCH[2]}
            SCORE=$((ENV_COUNT * TIME_SCALE))
            
            if [[ $SCORE -gt $BEST_SCORE ]]; then
                BEST_SCORE=$SCORE
                BEST_STABLE="$ENV_COUNT environments, ${TIME_SCALE}x timescale"
            fi
        fi
    fi
done

if [[ -n $BEST_STABLE ]]; then
    echo "✅ OPTIMAL CONFIGURATION: $BEST_STABLE"
    echo "   This should give you ~${BEST_SCORE}x baseline performance"
else
    echo "⚠️  No stable configuration found. Try smaller increments."
fi

echo ""
echo "💡 NEXT STEPS:"
echo "1. Update your config with the optimal settings above"
echo "2. Monitor first few hours of training for stability"  
echo "3. If stable, you can train overnight at these settings"
echo ""
echo "Example command:"
if [[ -n $BEST_STABLE ]]; then
    BEST_ENV=$(echo "$BEST_STABLE" | grep -o '[0-9]*' | head -1)
    BEST_TIME=$(echo "$BEST_STABLE" | grep -o '[0-9]*x' | sed 's/x//')
    echo "mlagents-learn $CONFIG --run-id=OptimizedTraining --quality-level=0 --num-envs=$BEST_ENV --time-scale=$BEST_TIME --no-graphics"
fi 