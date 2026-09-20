using System;
using System.Collections;
using System.IO;
using AI;
using AI.Navigation.MPC;
using Game.Services.Units;
using NUnit.Framework;
using RL.Arena;
using RL.Episodes;
using RL.Hosts;
using RL.Opponents;
using Tests.PlayMode.Common;
using Tests.PlayMode.Scenarios;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode.TerminalField
{
    [Category("MPC")]
    public sealed class TerminalFieldContactDiagnosisTests : PlayModeWorldFixture
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator SameSeedDriftContactProbe()
        {
            if (Environment.GetEnvironmentVariable("MPC_CONTACT_DIAG") != "1") Assert.Ignore("Opt-in contact diagnosis.");
            var output = Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            var directory = Path.Combine(output, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            var asset = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var assets = AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            var root = new GameObject("contact-diagnosis");
            var units = root.AddComponent<UnitService>();
            units.SetProjectiles(Projectiles);
            var hostObject = new GameObject("contact-diagnosis-host");
            hostObject.transform.SetParent(root.transform, false);
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<HarnessSessionHost>();
            host.Initialize(new SessionSpec { probes = Array.Empty<ProbeSpec>() }, assets, units, Projectiles);
            var previousPresentation = GameSettings.PresentationEnabled;
            var previousScale = Time.timeScale;
            var previousCapture = Time.captureDeltaTime;
            GameSettings.SetPresentationEnabled(false);
            PacingContract.Apply();
            var counts = new int[4];
            try
            {
                using var field = HarnessField.Spawn(Vector2.zero, assets, 2f, root.transform, presentationEnabled: false);
                yield return new WaitForFixedUpdate();
                for (var trial = 0; trial < 4; trial++)
                {
                    var spec = EvalProtocol.EvalSpec(2f);
                    spec.runSeed = 3204;
                    using var composition = host.NewSentenceComposition(spec, field, SentenceRow.DriftHold);
                    var nav = composition.Pair.Agent.GetComponentInChildren<Navigator>();
                    var settings = UnityEngine.Object.Instantiate(asset);
                    settings.wTerminalField = trial < 2 ? 0f : 3f;
                    using var trace = new StreamWriter(Path.Combine(directory, $"trial{trial}-states.csv"));
                    using var collisions = new StreamWriter(Path.Combine(directory, $"trial{trial}-contacts.csv"));
                    trace.WriteLine("step,x,y,vx,vy,targetX,targetY,thrust,strafe,yawTorque,bestCost,contacts,bodyX,bodyY");
                    collisions.WriteLine("step,kind,other,layer,points,impulse,relativeSpeed,x,y,otherX,otherY");
                    try
                    {
                        nav.mpcSettings = settings;
                        composition.InstallOpponent(SentenceRows.Block(SentenceRow.DriftHold), spec, 0, Vector2.zero);
                        var contacts = composition.Pair.Agent.gameObject.AddComponent<TerminalFieldContactCounter>();
                        var probe = composition.Pair.Agent.gameObject.AddComponent<TerminalFieldContactProbe>();
                        probe.Emit = line => collisions.WriteLine(line);
                        var step = 0;
                        yield return composition.Driver.RunEpisode(spec, 0, onBegin: () =>
                        {
                            var ship = composition.Pair.Agent;
                            var body = ship.Rigidbody;
                            File.WriteAllText(Path.Combine(directory, $"trial{trial}-initial.csv"),
                                "seed,weight,radius,x,y,rotation,inertiaZ,fixedTime\n" +
                                FormattableString.Invariant($"{ship.DecisionSeed},{nav.mpc.Settings.wTerminalField},{ship.Dynamics.shipRadius},{body.position.x},{body.position.y},{body.rotation.eulerAngles.z},{body.inertiaTensor.z},{Time.fixedTimeAsDouble}\n"));
                        }, onFixedStep: () =>
                        {
                            probe.Step = ++step;
                            var ship = composition.Pair.Agent.Kinematics;
                            var target = composition.Pair.Baseline.Kinematics;
                            var control = nav.mpc.LastControl;
                            var body = composition.Pair.Agent.transform.position;
                            trace.WriteLine(FormattableString.Invariant($"{step},{ship.pos.x},{ship.pos.y},{ship.vel.x},{ship.vel.y},{target.pos.x},{target.pos.y},{control.thrust},{control.strafe},{control.yawTorque},{nav.mpc.LastBestCost},{contacts.Steps},{body.x},{body.y}"));
                        });
                        counts[trial] = contacts.Steps;
                        File.AppendAllText(Path.Combine(directory, "summary.csv"), FormattableString.Invariant($"{trial},{settings.wTerminalField},{step},{contacts.Steps}\n"));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(settings); }
                    Projectiles.ReturnAllToPool();
                }
                Assert.That(counts[1], Is.EqualTo(counts[0]), "Identical field-off trials must agree before attributing contact differences to terminal weight.");
                Assert.That(counts[3], Is.EqualTo(counts[2]), "Identical field-on trials must agree before attributing contact differences to terminal weight.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Time.timeScale = previousScale;
                Time.captureDeltaTime = previousCapture;
                GameSettings.SetPresentationEnabled(previousPresentation);
            }
        }
    }
}
