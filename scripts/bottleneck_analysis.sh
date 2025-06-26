#!/bin/bash

# ML-Agents Bottleneck Analysis Script
# Identifies where the real performance bottleneck is

echo "=== ML-Agents Bottleneck Analysis ==="
echo "This will help identify what's actually limiting your training speed"
echo ""

# Check current setup
CONFIG="results/CommanderCurriculum_v2/configuration.yaml"
echo "🔍 CURRENT CONFIGURATION:"
echo "Num Envs: $(grep 'num_envs:' $CONFIG | awk '{print $2}')"
echo "Time Scale: $(grep 'time_scale:' $CONFIG | awk '{print $2}')"
echo "Graphics: $(grep 'no_graphics:' $CONFIG | awk '{print $2}')"
echo ""

echo "❓ DIAGNOSTIC QUESTIONS:"
echo ""

# Question 1: Build vs Editor
echo "1. Are you running from Unity Editor or a Build?"
echo "   - Editor: SLOW (10x slower than build)"
echo "   - Build: FAST (recommended)"
echo "   Current env_path: $(grep 'env_path:' $CONFIG | awk '{print $2}')"

if grep -q "env_path: null" $CONFIG; then
    echo "   ⚠️  WARNING: env_path is null - you're probably running in Editor!"
    echo "   This is your bottleneck. Build the game first."
    LIKELY_BOTTLENECK="Unity Editor"
else
    echo "   ✅ Using build executable"
    LIKELY_BOTTLENECK="Unknown"
fi

echo ""

# Question 2: System specs
echo "2. System Resources:"
if command -v python3 >/dev/null && python3 -c "import psutil" 2>/dev/null; then
    python3 -c "
import psutil
print(f'   CPU Cores: {psutil.cpu_count()} logical, {psutil.cpu_count(logical=False)} physical')
print(f'   RAM: {psutil.virtual_memory().total // (1024**3)} GB')
print(f'   Current CPU: {psutil.cpu_percent(interval=1):.1f}%')
print(f'   Current Memory: {psutil.virtual_memory().percent:.1f}%')
"
else
    echo "   (Install python3 + psutil for detailed info)"
fi

echo ""

# Question 3: Network setup (for distributed training)
echo "3. Network Configuration:"
BASE_PORT=$(grep 'base_port:' $CONFIG | awk '{print $2}')
echo "   Base port: $BASE_PORT"
echo "   Are you running distributed training? (multiple machines)"
echo ""

# Question 4: GPU usage
echo "4. GPU Status:"
if command -v nvidia-smi >/dev/null; then
    echo "   GPU detected:"
    nvidia-smi --query-gpu=name,utilization.gpu,memory.used,memory.total --format=csv,noheader,nounits | head -1 | awk -F',' '{printf "   %s: %s%% GPU, %s/%s MB VRAM\n", $1, $2, $3, $4}'
else
    echo "   No NVIDIA GPU detected (CPU-only training)"
fi

echo ""

# Question 5: Quick performance test
echo "5. Running quick performance test..."

# Test with minimal setup
echo "   Testing 1 environment baseline..."
timeout 60s mlagents-learn $CONFIG \
    --run-id=BottleneckTest_1env \
    --num-envs=1 \
    --time-scale=20 \
    --quality-level=0 \
    --no-graphics \
    > /tmp/bottleneck_1env.log 2>&1 &

TEST_PID=$!
sleep 60
kill $TEST_PID 2>/dev/null
wait $TEST_PID 2>/dev/null

# Extract metrics
if [[ -f /tmp/bottleneck_1env.log ]]; then
    STEPS_1ENV=$(grep "Step:" /tmp/bottleneck_1env.log | tail -1 | grep -o 'Step: [0-9]*' | grep -o '[0-9]*' || echo "0")
    echo "   1 environment: $STEPS_1ENV steps in 60 seconds = $((STEPS_1ENV / 60)) steps/sec"
else
    echo "   ⚠️  Test failed to run"
fi

echo ""
echo "=== BOTTLENECK ANALYSIS ==="

if [[ "$LIKELY_BOTTLENECK" == "Unity Editor" ]]; then
    echo "🔴 PRIMARY BOTTLENECK: Unity Editor"
    echo ""
    echo "SOLUTION: Build your game executable"
    echo "1. Open Unity Editor"
    echo "2. File → Build Settings"
    echo "3. Select 'PC, Mac & Linux Standalone'"
    echo "4. Build to: src/Asteroids3D/TrainingBuild/Asteroids3D.exe"
    echo "5. Update config:"
    echo "   sed -i 's/env_path: null/env_path: src\/Asteroids3D\/TrainingBuild\/Asteroids3D.exe/' $CONFIG"
    echo ""
    echo "Expected speedup: 5-10x faster"

elif [[ $STEPS_1ENV -lt 100 ]]; then
    echo "🔴 PRIMARY BOTTLENECK: Slow step rate"
    echo ""
    echo "Possible causes:"
    echo "1. Complex physics simulation"
    echo "2. Unity build not optimized"
    echo "3. Slow Python/PyTorch setup"
    echo "4. Network communication issues"
    echo ""
    echo "SOLUTIONS:"
    echo "- Use Release build (not Debug)"
    echo "- Lower physics quality in Unity"
    echo "- Check Python environment (conda vs pip)"
    echo "- Use --env-args for Unity optimizations"

else
    echo "🟡 SECONDARY BOTTLENECKS:"
    echo ""
    echo "Step rate seems reasonable. Other possible bottlenecks:"
    echo "1. Neural network size (reduce hidden_units)"
    echo "2. Observation complexity (too many sensors)"
    echo "3. Batch processing inefficiency"
    echo "4. Memory bandwidth limits"
fi

echo ""
echo "=== IMMEDIATE ACTIONS ==="
echo "1. Check if you're using Unity Editor (biggest bottleneck)"
echo "2. Build optimized executable if needed"
echo "3. Monitor actual experience collection rate, not just step rate"
echo "4. Profile Unity build performance"

echo ""
echo "=== MEASURING THE RIGHT METRICS ==="
echo ""
echo "❌ WRONG: Steps per second (this stays constant)"
echo "✅ RIGHT: Experience samples per second"
echo ""
echo "With more environments:"
echo "- Steps/sec might stay the same"
echo "- But each step contains MORE experience"
echo "- Total learning speed increases"
echo ""
echo "Example:"
echo "1 env × 100 steps/sec = 100 experiences/sec"
echo "32 envs × 100 steps/sec = 3200 experiences/sec"

echo ""
echo "Check your tensorboard logs for:"
echo "- Environment/Episode Length"
echo "- Environment/Cumulative Reward"
echo "- Losses/Policy Loss"
echo ""
echo "These should update faster with more environments!" 