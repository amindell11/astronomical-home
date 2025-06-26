#!/bin/bash

# Optimal Environments Calculator for ML-Agents
# Calculates the theoretically optimal num_envs based on buffer settings

CONFIG="results/CommanderCurriculum_v2/configuration.yaml"

echo "=== ML-Agents Optimal Environment Calculator ==="
echo ""

# Extract key parameters
BUFFER_SIZE=$(grep 'buffer_size:' $CONFIG | awk '{print $2}')
TIME_HORIZON=$(grep 'time_horizon:' $CONFIG | awk '{print $2}')
BATCH_SIZE=$(grep 'batch_size:' $CONFIG | awk '{print $2}')
CURRENT_NUM_ENVS=$(grep 'num_envs:' $CONFIG | awk '{print $2}')

echo "📊 CURRENT CONFIGURATION:"
echo "Buffer Size: $BUFFER_SIZE"
echo "Time Horizon: $TIME_HORIZON"
echo "Batch Size: $BATCH_SIZE"
echo "Current num_envs: $CURRENT_NUM_ENVS"
echo ""

# Calculate optimal values
OPTIMAL_ENVS=$((BUFFER_SIZE / TIME_HORIZON))
BUFFER_RATIO=$((BUFFER_SIZE / BATCH_SIZE))
EXPERIENCES_PER_UPDATE=$((TIME_HORIZON * CURRENT_NUM_ENVS))

echo "🧮 CALCULATIONS:"
echo ""

echo "1. OPTIMAL ENVIRONMENTS:"
echo "   Formula: buffer_size ÷ time_horizon"
echo "   Calculation: $BUFFER_SIZE ÷ $TIME_HORIZON = $OPTIMAL_ENVS environments"
echo ""

echo "2. BUFFER EFFICIENCY:"
echo "   Buffer ratio: $BUFFER_SIZE ÷ $BATCH_SIZE = ${BUFFER_RATIO}x"
if [[ $BUFFER_RATIO -ge 8 ]] && [[ $BUFFER_RATIO -le 12 ]]; then
    echo "   Status: ✅ GOOD (8-12x is optimal)"
else
    echo "   Status: ⚠️  SUBOPTIMAL (should be 8-12x)"
fi
echo ""

echo "3. EXPERIENCE COLLECTION:"
echo "   Current collection rate: $TIME_HORIZON × $CURRENT_NUM_ENVS = $EXPERIENCES_PER_UPDATE experiences per update"
echo "   Optimal collection rate: $TIME_HORIZON × $OPTIMAL_ENVS = $((TIME_HORIZON * OPTIMAL_ENVS)) experiences per update"
echo ""

# Analyze current vs optimal
if [[ $CURRENT_NUM_ENVS -gt $((OPTIMAL_ENVS * 2)) ]]; then
    STATUS="🔴 TOO MANY"
    RECOMMENDATION="Reduce environments - you're overwhelming the buffer"
elif [[ $CURRENT_NUM_ENVS -gt $OPTIMAL_ENVS ]]; then
    STATUS="🟡 SLIGHTLY HIGH"
    RECOMMENDATION="Consider reducing for better sample efficiency"
elif [[ $CURRENT_NUM_ENVS -lt $((OPTIMAL_ENVS / 2)) ]]; then
    STATUS="🟠 TOO FEW"
    RECOMMENDATION="Increase environments - you're underutilizing the buffer"
else
    STATUS="✅ GOOD RANGE"
    RECOMMENDATION="Current setting is reasonable"
fi

echo "=== ANALYSIS ==="
echo ""
echo "Current vs Optimal: $STATUS"
echo "Recommendation: $RECOMMENDATION"
echo ""

# Calculate specific recommendations
CONSERVATIVE_ENVS=$((OPTIMAL_ENVS - 2))
AGGRESSIVE_ENVS=$((OPTIMAL_ENVS + 4))

if [[ $CONSERVATIVE_ENVS -lt 4 ]]; then
    CONSERVATIVE_ENVS=4
fi

echo "🎯 SPECIFIC RECOMMENDATIONS:"
echo ""
echo "Conservative: $CONSERVATIVE_ENVS environments"
echo "  - Good for: Stable learning, debugging"
echo "  - Speed: ~$((CONSERVATIVE_ENVS * 25))x baseline"
echo ""
echo "Optimal: $OPTIMAL_ENVS environments"
echo "  - Good for: Balanced speed and quality"
echo "  - Speed: ~$((OPTIMAL_ENVS * 25))x baseline"
echo ""
echo "Aggressive: $AGGRESSIVE_ENVS environments"
echo "  - Good for: Maximum speed (if hardware allows)"
echo "  - Speed: ~$((AGGRESSIVE_ENVS * 25))x baseline"
echo "  - Risk: May reduce sample diversity"
echo ""

echo "=== WHY TOO MANY ENVIRONMENTS HURT LEARNING ==="
echo ""
echo "1. 🔄 SAMPLE CORRELATION:"
echo "   - Too many parallel environments collect similar experiences"
echo "   - Reduces diversity in training batches"
echo "   - Agent learns from redundant data"
echo ""
echo "2. 📊 BUFFER OVERFLOW:"
echo "   - Buffer fills too quickly with correlated samples"
echo "   - Old diverse experiences get pushed out too fast"
echo "   - Training becomes less stable"
echo ""
echo "3. ⚡ SYNCHRONIZATION OVERHEAD:"
echo "   - All environments must step together"
echo "   - Slowest environment blocks all others"
echo "   - Communication overhead increases quadratically"
echo ""
echo "4. 🧠 LEARNING EFFICIENCY:"
echo "   - PPO works best with specific experience ratios"
echo "   - Too much parallel data can overwhelm the algorithm"
echo "   - Quality > Quantity for RL learning"
echo ""

echo "=== IMMEDIATE ACTION ==="
echo ""
if [[ $CURRENT_NUM_ENVS -gt $((OPTIMAL_ENVS * 2)) ]]; then
    echo "🚨 URGENT: Your num_envs is TOO HIGH"
    echo ""
    echo "Try this immediately:"
    echo "mlagents-learn $CONFIG --run-id=OptimalTest --num-envs=$OPTIMAL_ENVS --time-scale=25 --quality-level=0 --no-graphics"
    echo ""
    echo "Expected results:"
    echo "✅ Better learning stability"
    echo "✅ More diverse experiences" 
    echo "✅ Potentially faster convergence"
    echo "⚠️  Lower raw step/sec (but better learning/sec)"
else
    echo "✅ Your current num_envs is in a reasonable range"
    echo ""
    echo "For comparison, try the theoretical optimal:"
    echo "mlagents-learn $CONFIG --run-id=OptimalTest --num-envs=$OPTIMAL_ENVS --time-scale=25 --quality-level=0 --no-graphics"
fi

echo ""
echo "🔍 MONITORING TIPS:"
echo "- Watch for decreasing episode length variance (good sign)"
echo "- Monitor reward curve smoothness"
echo "- Check for faster policy convergence"
echo "- Look for more consistent training metrics" 