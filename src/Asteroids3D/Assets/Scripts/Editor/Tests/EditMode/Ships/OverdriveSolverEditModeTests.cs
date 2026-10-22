#if UNITY_EDITOR
using System.Linq;
using AI.Navigation.MPC;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Keystones;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Ships
{
    /// <summary>The solver plans with the live cap: the same request at the base cap stays under it, and under overdrive's dynamics goes past it.</summary>
    [Category("MPC")]
    public class OverdriveSolverEditModeTests
    {
        private const string MpcSettingsPath = "Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset";
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";

        [Test]
        public void Plan_UnderOverdriveDynamics_GoesPastTheBaseCap()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MpcSettings>(MpcSettingsPath);
            var valis = AssetDatabase.LoadAssetAtPath<Ship>(ValisPath);
            var overdrive = valis.KeystonePrefab as Overdrive;
            Assert.That(overdrive, Is.Not.Null, "Valis carries Overdrive");
            var baseDynamics = valis.ResolveStats().Dynamics;
            var raised = baseDynamics.Scaled(overdrive.PeakSpeedScale, overdrive.PeakThrustScale);

            var atCap = new Kinematics(Vector2.zero, new Vector2(0f, baseDynamics.maxSpeed), 0f, 0f, 0f);
            var inputs = new MpcInputs
            {
                kinematics = atCap,
                velocityReference = new float2(0f, raised.maxSpeed * 1.5f),
                facingRad = float.NaN,
                enemyYaw = float.NaN,
                obstacleScan = default,
                enableObstacleAvoidance = false,
            };

            using var mpc = new Mpc(settings, baseDynamics, 0u);
            for (var i = 0; i < 6; i++) mpc.Plan(in inputs);
            var basePeak = mpc.PredictedStates.Max(s => math.length(s.vel));

            mpc.SetDynamics(in raised);
            for (var i = 0; i < 6; i++) mpc.Plan(in inputs);
            var raisedPeak = mpc.PredictedStates.Max(s => math.length(s.vel));

            Assert.That(basePeak, Is.LessThanOrEqualTo(baseDynamics.maxSpeed + 0.01f), "the base plan respects the base cap");
            Assert.That(raisedPeak, Is.GreaterThan(baseDynamics.maxSpeed + 1f), "the overdrive plan uses the raised cap");
            Assert.That(raisedPeak, Is.LessThanOrEqualTo(raised.maxSpeed + 0.01f), "and stays under it");
        }
    }
}
#endif
