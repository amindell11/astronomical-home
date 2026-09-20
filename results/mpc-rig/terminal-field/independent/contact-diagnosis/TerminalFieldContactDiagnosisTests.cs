using System;
using System.Collections;
using System.IO;
using System.Linq;
using Asteroids;
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
                var freshFields = Environment.GetEnvironmentVariable("MPC_CONTACT_DIAG_FRESH_FIELDS") == "1";
                using var sharedField = freshFields ? null : HarnessField.Spawn(Vector2.zero, assets, 2f, root.transform, presentationEnabled: false);
                yield return new WaitForFixedUpdate();
                for (var trial = 0; trial < 4; trial++)
                {
                    using var freshField = freshFields ? HarnessField.Spawn(Vector2.zero, assets, 2f, root.transform, presentationEnabled: false) : null;
                    var field = freshField ?? sharedField;
                    var spec = EvalProtocol.EvalSpec(2f);
                    spec.runSeed = 3204;
                    using var composition = host.NewSentenceComposition(spec, field, SentenceRow.DriftHold);
                    var nav = composition.Pair.Agent.GetComponentInChildren<Navigator>();
                    var settings = UnityEngine.Object.Instantiate(asset);
                    settings.wTerminalField = Environment.GetEnvironmentVariable("MPC_CONTACT_DIAG_ALL_OFF") == "1" || trial < 2 ? 0f : 3f;
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
                        AsteroidController[] rocks = null;
                        Action<int> snapshot = sample => {
                            using var writer = new StreamWriter(Path.Combine(directory, $"trial{trial}-rocks-{sample}.csv"));
                            writer.WriteLine("index,x,y,z,qx,qy,qz,qw,vx,vy,vz,wx,wy,wz,mass,radius,ix,iy,iz,cx,cy,cz,mesh,meshEnabled");
                            for (var i = 0; i < rocks.Length; i++) {
                                var a = rocks[i]; var b = a.Rb; var p = b.position; var q = b.rotation; var v = b.linearVelocity; var w = b.angularVelocity; var inertia = b.inertiaTensor; var center = b.centerOfMass;
                                writer.WriteLine(FormattableString.Invariant($"{i},{p.x:R},{p.y:R},{p.z:R},{q.x:R},{q.y:R},{q.z:R},{q.w:R},{v.x:R},{v.y:R},{v.z:R},{w.x:R},{w.y:R},{w.z:R},{a.Mass:R},{a.Radius:R},{inertia.x:R},{inertia.y:R},{inertia.z:R},{center.x:R},{center.y:R},{center.z:R},{a.CurrentMesh.name},{a.GetComponent<MeshCollider>().enabled}"));
                            }
                        };
                        yield return composition.Driver.RunEpisode(spec, 0, onBegin: () =>
                        {
                            rocks = field.Field.GetComponentsInChildren<AsteroidController>().OrderBy(a => a.Rb.position.x).ThenBy(a => a.Rb.position.y).ToArray();
                            snapshot(0);
                            var ship = composition.Pair.Agent;
                            var body = ship.Rigidbody;
                            File.WriteAllText(Path.Combine(directory, $"trial{trial}-initial.csv"),
                                "seed,weight,radius,x,y,rotation,inertiaZ,fixedTime\n" +
                                FormattableString.Invariant($"{ship.DecisionSeed},{nav.mpc.Settings.wTerminalField},{ship.Dynamics.shipRadius},{body.position.x},{body.position.y},{body.rotation.eulerAngles.z},{body.inertiaTensor.z},{Time.fixedTimeAsDouble}\n"));
                        }, onFixedStep: () =>
                        {
                            probe.Step = ++step;
                            if (step == 1 || step == 2 || step == 10 || step == 100 || step == 1000 || step == 4500) snapshot(step);
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
