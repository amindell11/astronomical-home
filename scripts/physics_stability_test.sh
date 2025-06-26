#!/bin/bash

# Physics Stability Testing Script
# Tests timescales incrementally to find physics breaking point

CONFIG="results/CommanderCurriculum_v2/configuration.yaml"
BACKUP_CONFIG="${CONFIG}.backup"
TEST_DURATION=300  # 5 minutes per test - longer to catch physics issues

echo "=== ML-Agents Physics Stability Test ==="
echo "Testing timescales incrementally to find physics breaking point"
echo "Test duration: $TEST_DURATION seconds per configuration"
echo ""

# Backup original config
cp "$CONFIG" "$BACKUP_CONFIG"

# Conservative timescale progression - start safe, go until physics breaks
declare -a TIMESCALES=(20 25 30 35 40 45 50)
declare -a RESULTS=()

for timescale in "${TIMESCALES[@]}"; do
    echo "--- Testing Timescale ${timescale}x (Physics Stability Focus) ---"
    
    # Update config with fixed num_envs for consistent testing
    sed -i.tmp "s/num_envs: [0-9]*/num_envs: 4/" "$CONFIG"  # Lower envs to focus on physics
    sed -i.tmp "s/time_scale: [0-9]*/time_scale: $timescale/" "$CONFIG"
    rm -f "${CONFIG}.tmp"
    
    RUN_ID="PhysicsTest_T${timescale}"
    
    echo "  Starting training with physics monitoring..."
    timeout ${TEST_DURATION}s mlagents-learn "$CONFIG" \
        --run-id="$RUN_ID" \
        --quality-level=0 \
        --no-graphics \
        > "/tmp/physics_test_${timescale}.log" 2>&1 &
    
    TRAIN_PID=$!
    START_TIME=$(date +%s)
    
    # Physics-specific monitoring
    PHYSICS_WARNINGS=0
    COLLISION_ERRORS=0
    EPISODE_LENGTH_VARIANCE=0
    REWARD_ANOMALIES=0
    SAMPLE_COUNT=0
    
    declare -a EPISODE_LENGTHS=()
    declare -a REWARDS=()
    
    while kill -0 $TRAIN_PID 2>/dev/null; do
        sleep 15  # Longer intervals for physics observation
        SAMPLE_COUNT=$((SAMPLE_COUNT + 1))
        
        # Check log for physics-related warnings
        if [[ -f "/tmp/physics_test_${timescale}.log" ]]; then
            # Count new physics warnings since last check
            NEW_WARNINGS=$(grep -c -i "warning\|error\|collision\|rigidbody\|physics" "/tmp/physics_test_${timescale}.log" 2>/dev/null || echo "0")
            if [[ $NEW_WARNINGS -gt $PHYSICS_WARNINGS ]]; then
                echo "  ⚠️  Physics warnings detected: $((NEW_WARNINGS - PHYSICS_WARNINGS)) new"
                PHYSICS_WARNINGS=$NEW_WARNINGS
            fi
            
            # Check for episode length consistency (physics affects episode duration)
            RECENT_EPISODES=$(grep "Episode length:" "/tmp/physics_test_${timescale}.log" 2>/dev/null | tail -10 || echo "")
            if [[ -n "$RECENT_EPISODES" ]]; then
                # Calculate variance in episode lengths (high variance = physics instability)
                LENGTHS=$(echo "$RECENT_EPISODES" | grep -o '[0-9]*' | tail -10)
                if [[ -n "$LENGTHS" ]]; then
                    AVG_LENGTH=$(echo "$LENGTHS" | awk '{sum+=$1} END {print sum/NR}')
                    VARIANCE=$(echo "$LENGTHS" | awk -v avg="$AVG_LENGTH" '{sum+=($1-avg)^2} END {print sqrt(sum/NR)}')
                    
                    # High variance indicates physics problems
                    if (( $(echo "$VARIANCE > $AVG_LENGTH * 0.3" | bc -l 2>/dev/null || echo "0") )); then
                        echo "  🔴 High episode length variance detected: ${VARIANCE} (avg: ${AVG_LENGTH})"
                        EPISODE_LENGTH_VARIANCE=1
                    fi
                fi
            fi
            
            # Check for reward anomalies (NaN, extreme values indicate physics breakdown)
            RECENT_REWARDS=$(grep "Mean Reward:" "/tmp/physics_test_${timescale}.log" 2>/dev/null | tail -5 || echo "")
            if [[ -n "$RECENT_REWARDS" ]]; then
                if echo "$RECENT_REWARDS" | grep -q -i "nan\|inf\|-inf"; then
                    echo "  🔴 CRITICAL: NaN/Infinite rewards detected - physics broken!"
                    REWARD_ANOMALIES=1
                fi
            fi
            
            # Check for collision detection failures (ships surviving too long)
            SURVIVAL_TIMES=$(grep "Episode length:" "/tmp/physics_test_${timescale}.log" 2>/dev/null | tail -5 | grep -o '[0-9]*')
            if [[ -n "$SURVIVAL_TIMES" ]]; then
                MAX_SURVIVAL=$(echo "$SURVIVAL_TIMES" | sort -nr | head -1)
                # If episodes are running much longer than expected, collision detection may be failing
                if [[ $MAX_SURVIVAL -gt 10000 ]]; then  # Arbitrary threshold - adjust based on your game
                    echo "  ⚠️  Unusually long episodes detected: ${MAX_SURVIVAL} steps (possible collision tunneling)"
                    COLLISION_ERRORS=1
                fi
            fi
        fi
        
        echo "  Sample $SAMPLE_COUNT: Monitoring physics stability..."
    done
    
    wait $TRAIN_PID
    EXIT_CODE=$?
    
    END_TIME=$(date +%s)
    DURATION=$((END_TIME - START_TIME))
    
    # Analyze final physics stability
    PHYSICS_STABLE=true
    STABILITY_SCORE=100
    
    # Deduct points for each type of instability
    if [[ $PHYSICS_WARNINGS -gt 5 ]]; then
        PHYSICS_STABLE=false
        STABILITY_SCORE=$((STABILITY_SCORE - 20))
        echo "  🔴 Excessive physics warnings: $PHYSICS_WARNINGS"
    fi
    
    if [[ $EPISODE_LENGTH_VARIANCE -eq 1 ]]; then
        PHYSICS_STABLE=false
        STABILITY_SCORE=$((STABILITY_SCORE - 30))
        echo "  🔴 High episode variance detected"
    fi
    
    if [[ $REWARD_ANOMALIES -eq 1 ]]; then
        PHYSICS_STABLE=false
        STABILITY_SCORE=$((STABILITY_SCORE - 50))
        echo "  🔴 CRITICAL: Reward anomalies detected"
    fi
    
    if [[ $COLLISION_ERRORS -eq 1 ]]; then
        PHYSICS_STABLE=false
        STABILITY_SCORE=$((STABILITY_SCORE - 25))
        echo "  🔴 Possible collision detection failures"
    fi
    
    if [[ $EXIT_CODE -ne 0 ]] && [[ $EXIT_CODE -ne 124 ]]; then
        PHYSICS_STABLE=false
        STABILITY_SCORE=$((STABILITY_SCORE - 40))
        echo "  🔴 Training crashed: exit code $EXIT_CODE"
    fi
    
    # Status determination
    STATUS="STABLE"
    if [[ $PHYSICS_STABLE == false ]]; then
        if [[ $STABILITY_SCORE -lt 50 ]]; then
            STATUS="BROKEN"
        else
            STATUS="UNSTABLE"
        fi
    fi
    
    echo "  Physics Stability Score: ${STABILITY_SCORE}/100 - $STATUS"
    echo ""
    
    RESULTS+=("Timescale ${timescale}x: Stability=${STABILITY_SCORE}/100, Status=$STATUS")
    
    # Stop if physics is critically broken
    if [[ $STATUS == "BROKEN" ]]; then
        echo "=== STOPPING: Physics stability critically compromised ==="
        break
    fi
done

# Restore original config
cp "$BACKUP_CONFIG" "$CONFIG"
rm -f "$BACKUP_CONFIG"

echo "=== PHYSICS STABILITY TEST RESULTS ==="
for result in "${RESULTS[@]}"; do
    echo "$result"
done

echo ""
echo "=== RECOMMENDATIONS ==="

# Find the highest stable timescale
MAX_STABLE_TIMESCALE=0
for result in "${RESULTS[@]}"; do
    if [[ $result == *"Status=STABLE"* ]]; then
        if [[ $result =~ Timescale\ ([0-9]+)x ]]; then
            TIMESCALE=${BASH_REMATCH[1]}
            if [[ $TIMESCALE -gt $MAX_STABLE_TIMESCALE ]]; then
                MAX_STABLE_TIMESCALE=$TIMESCALE
            fi
        fi
    fi
done

if [[ $MAX_STABLE_TIMESCALE -gt 0 ]]; then
    echo "✅ MAXIMUM SAFE TIMESCALE: ${MAX_STABLE_TIMESCALE}x"
    echo "   Use this as your upper limit for production training"
    
    # Calculate safe production recommendation (10% safety margin)
    SAFE_TIMESCALE=$((MAX_STABLE_TIMESCALE - 5))
    if [[ $SAFE_TIMESCALE -lt 20 ]]; then
        SAFE_TIMESCALE=20
    fi
    
    echo "🎯 PRODUCTION RECOMMENDATION: ${SAFE_TIMESCALE}x (with safety margin)"
    echo ""
    echo "Safe command:"
    echo "mlagents-learn $CONFIG --run-id=SafeTraining --time-scale=$SAFE_TIMESCALE --num-envs=16 --quality-level=0 --no-graphics"
else
    echo "⚠️  Even lowest timescale showed instability. Check your physics settings!"
fi

echo ""
echo "🔍 PHYSICS DEBUGGING TIPS:"
echo "1. Check Unity Physics settings (Fixed Timestep, Solver Iterations)"
echo "2. Verify collision detection settings on asteroids and ships"
echo "3. Review missile guidance update frequency"
echo "4. Monitor collision layer matrix for conflicts" 