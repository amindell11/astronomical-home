#!/bin/bash

# Quick Quality Level Performance Test
# Tests quality levels 0-2 with short training runs

CONFIG="results/CommanderCurriculum_v2/configuration.yaml"
BASE_RUN_ID="QualityTest"
TEST_DURATION=120  # 2 minutes per test

echo "=== ML-Agents Quality Level Performance Test ==="
echo "Testing quality levels 0, 1, 2 for $TEST_DURATION seconds each"
echo "Config: $CONFIG"
echo ""

declare -a QUALITY_LEVELS=(0 1 2)
declare -a RESULTS=()

for quality in "${QUALITY_LEVELS[@]}"; do
    echo "--- Testing Quality Level $quality ---"
    
    RUN_ID="${BASE_RUN_ID}_Q${quality}"
    
    # Start training in background
    timeout ${TEST_DURATION}s mlagents-learn $CONFIG \
        --run-id=$RUN_ID \
        --quality-level=$quality \
        --time-scale=25 \
        --num-envs=4 \
        --no-graphics \
        > /tmp/quality_test_${quality}.log 2>&1 &
    
    TRAIN_PID=$!
    START_TIME=$(date +%s)
    
    # Monitor CPU and memory for the duration
    CPU_SAMPLES=()
    MEM_SAMPLES=()
    
    while kill -0 $TRAIN_PID 2>/dev/null; do
        if command -v top >/dev/null; then
            # Linux/Mac
            CPU=$(top -bn1 | grep "Cpu(s)" | awk '{print $2}' | cut -d'%' -f1)
            MEM=$(free -m | awk 'NR==2{printf "%.1f", $3*100/$2}')
        else
            # Fallback for other systems
            CPU="N/A"
            MEM="N/A"
        fi
        
        CPU_SAMPLES+=($CPU)
        MEM_SAMPLES+=($MEM)
        sleep 5
    done
    
    END_TIME=$(date +%s)
    DURATION=$((END_TIME - START_TIME))
    
    # Calculate averages
    if [[ ${CPU_SAMPLES[0]} != "N/A" ]]; then
        AVG_CPU=$(echo "${CPU_SAMPLES[@]}" | awk '{for(i=1;i<=NF;i++)sum+=$i;print sum/NF}')
        AVG_MEM=$(echo "${MEM_SAMPLES[@]}" | awk '{for(i=1;i<=NF;i++)sum+=$i;print sum/NF}')
    else
        AVG_CPU="N/A"
        AVG_MEM="N/A"
    fi
    
    # Extract steps/sec from log if available
    STEPS_PER_SEC="N/A"
    if grep -q "Steps:" /tmp/quality_test_${quality}.log; then
        STEPS_PER_SEC=$(grep "Steps:" /tmp/quality_test_${quality}.log | tail -1 | awk '{print $4}' | sed 's/[^0-9.]//g')
    fi
    
    echo "  Duration: ${DURATION}s"
    echo "  Avg CPU: ${AVG_CPU}%"
    echo "  Avg Memory: ${AVG_MEM}%"
    echo "  Steps/sec: $STEPS_PER_SEC"
    echo ""
    
    RESULTS+=("Quality $quality: CPU=${AVG_CPU}%, Mem=${AVG_MEM}%, Steps/sec=${STEPS_PER_SEC}")
done

echo "=== RESULTS SUMMARY ==="
for result in "${RESULTS[@]}"; do
    echo "$result"
done

echo ""
echo "RECOMMENDATION:"
echo "Use Quality Level 0 for fastest training (--quality-level=0)"
echo ""
echo "Full command example:"
echo "mlagents-learn $CONFIG --run-id=Production --quality-level=0 --time-scale=30 --num-envs=8 --no-graphics" 