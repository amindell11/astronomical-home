#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AI;
using AI.Research;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using Ships;
using Substrate;
using Substrate.Services;
using Substrate.Services.Projectiles;
using Substrate.Services.Units;
using RL.Arena;
using RL.Hosts;
using RL.Opponents;
using RL.Probes;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Research
{
    /// <summary>Research rig (branch research/ai-gun-strategies, never merged): AI duels driven by research/gun/config.json in the slot root, one JSONL row per episode under results/gun-research.</summary>
    public class GunResearchPlayModeTests
    {
        [Serializable]
        public class SideConfig
        {
            public string pilot = "AggressorPilot";
            public string doctrine = "";
        }

        [Serializable]
        public class Config
        {
            public string tag = "run";
            public string mode = "matrix";
            public string[] weapons = Array.Empty<string>();
            public string[] opponents = Array.Empty<string>();
            public bool ordered;
            public int episodes = 6;
            public int seed = 7001;
            public float timeoutSec = 60f;
            public float spawnMin = 30f;
            public float spawnMax = 40f;
            public bool field;
            public float fieldDensity = 1f;
            public SideConfig a = new();
            public SideConfig b = new();
        }

        [Serializable]
        public class Batch
        {
            public Config[] runs = Array.Empty<Config>();
        }

        [Serializable]
        public struct Shot
        {
            public float t;
            public float err;
            public float leadErr;
            public float dist;
            public float cross;
            public float selfBank;
            public float targetBank;
            public int hit;
        }

        [Serializable]
        public class SideStats
        {
            public float firstShot = -1f;
            public List<Shot> shots = new();
            public string weapon;
            public string doctrine;
            public MarksmanshipTally tally;
            public int steps;
            public float meanDist;
            public float meanAimErr;
            public float aimOkFrac;
            public float readyFrac;
            public float inRangeFrac;
            public float overheatFrac;
            public float chargingFrac;
            public float inRangeAimErr;
            public float inRangeAimOkFrac;
            public float hullEnd;
            public float shieldEnd;
            public float damageTaken;
            public float selfDamage;
        }

        [Serializable]
        public class Row
        {
            public string tag;
            public string mode;
            public string pairing;
            public int episode;
            public int poseSeed;
            public bool swapped;
            public string winner;
            public float duration;
            public SideStats a;
            public SideStats b;
        }

        private sealed class StepStats
        {
            private readonly Ship self;
            private readonly Ship other;
            private readonly WeaponComponent weapon;
            private readonly Heat heat;
            private readonly IChargeReadout charge;
            private readonly float tolerance;
            private int steps;
            private double dist, aimErr, inRangeErr;
            private int aimOk, ready, inRange, overheated, charging, inRangeOk;
            public float damageTaken;
            public float selfDamage;
            private int firstFireStep = -1;
            private readonly List<Shot> shots = new();
            private readonly Combat.Weapons.Arsenal.Railguns rail;
            private bool pendingRailHit;

            public StepStats(Ship self, Ship other, float tolerance)
            {
                this.self = self;
                this.other = other;
                this.tolerance = tolerance;
                weapon = self.Weapons ? self.Weapons.Primary : null;
                if (weapon)
                {
                    heat = weapon.GetComponent<Heat>();
                    foreach (var r in weapon.Readouts)
                        if (r is IChargeReadout c) charge = c;
                }
                self.Damage.OnDamaged += OnDamaged;
                if (weapon)
                {
                    weapon.OnFire += OnFired;
                    rail = weapon as Combat.Weapons.Arsenal.Railguns;
                    if (rail) rail.Hit += OnRailHit;
                }
            }

            private void OnFired()
            {
                if (firstFireStep < 0) firstFireStep = steps;
                var me = self.Kinematics;
                var them = other.Kinematics;
                var aim = Gunner.AimPoint(in me, them.pos, them.vel, weapon.ProjectileSpeed);
                var los = (them.pos - me.pos).normalized;
                shots.Add(new Shot
                {
                    t = steps * Time.fixedDeltaTime,
                    err = Vector2.Angle(me.Forward, them.pos - me.pos),
                    leadErr = Vector2.Angle(me.Forward, aim - me.pos),
                    dist = Vector2.Distance(me.pos, them.pos),
                    cross = Mathf.Abs(los.x * them.vel.y - los.y * them.vel.x),
                    selfBank = me.bank,
                    targetBank = them.bank,
                    hit = rail ? (pendingRailHit ? 1 : 0) : -1,
                });
                pendingRailHit = false;
            }

            private void OnRailHit(Vector3 point, Damage.IDamageable target)
            {
                // The beam's Hit fires before OnFire, so the flag rides into the shot about to be logged.
                pendingRailHit = target is Component c && c.GetComponentInParent<Ship>() == other;
            }

            private void OnDamaged(Damage.DamageInfo hit)
            {
                if (hit.AttackerId == other.Id) damageTaken += hit.Amount;
                else if (hit.AttackerId == self.Id && hit.Kind != Damage.DamageKind.Collision) selfDamage += hit.Amount;
            }

            public void Dispose()
            {
                self.Damage.OnDamaged -= OnDamaged;
                if (weapon) weapon.OnFire -= OnFired;
                if (rail) rail.Hit -= OnRailHit;
            }

            public void Sample()
            {
                steps++;
                var me = self.Kinematics;
                var them = other.Kinematics;
                var d = Vector2.Distance(me.pos, them.pos);
                dist += d;
                if (!weapon) return;
                var aim = Gunner.AimPoint(in me, them.pos, them.vel, weapon.ProjectileSpeed);
                var err = Vector2.Angle(me.Forward, aim - me.pos);
                aimErr += err;
                if (err <= tolerance) aimOk++;
                if (weapon.CanFire()) ready++;
                var range = weapon.FireRange > 0f ? weapon.FireRange : 50f;
                if (d <= range)
                {
                    inRange++;
                    inRangeErr += err;
                    if (err <= tolerance) inRangeOk++;
                }
                if (heat && heat.Overheated) overheated++;
                if (charge != null && charge.ChargePct > 0f) charging++;
            }

            public void Fill(SideStats s)
            {
                var n = Mathf.Max(1, steps);
                s.steps = steps;
                s.meanDist = (float)(dist / n);
                s.meanAimErr = (float)(aimErr / n);
                s.aimOkFrac = aimOk / (float)n;
                s.readyFrac = ready / (float)n;
                s.inRangeFrac = inRange / (float)n;
                s.overheatFrac = overheated / (float)n;
                s.chargingFrac = charging / (float)n;
                s.inRangeAimErr = inRange > 0 ? (float)(inRangeErr / inRange) : -1f;
                s.inRangeAimOkFrac = inRange > 0 ? inRangeOk / (float)inRange : 0f;
                s.hullEnd = self.Damage.Health.CurrentValue;
                s.shieldEnd = self.Damage.Shield.CurrentValue;
                s.damageTaken = damageTaken;
                s.selfDamage = selfDamage;
                s.firstShot = firstFireStep >= 0 ? firstFireStep * Time.fixedDeltaTime : -1f;
                s.shots = shots.Count <= 60 ? new List<Shot>(shots) : shots.GetRange(0, 60);
            }
        }

        private GameObject arenaHost;
        private UnitService units;
        private ProjectileService projectiles;
        private HarnessAssets assets;
        private readonly WaitForFixedUpdate waitFixed = new();
        private static bool loggedBounds;
        private static bool loggedMpc;

        private static string SlotRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            arenaHost = new GameObject("[GunResearchArena]");
            units = arenaHost.AddComponent<UnitService>();
            projectiles = ShipServices.Compose(units, arenaHost.transform, presentationEnabled: false);
            assets = AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            PacingContract.Apply();
            Time.maximumDeltaTime = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            projectiles?.ReturnAllToPool();
            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            AudioListener.pause = false;
        }

        [UnityTest]
        [Timeout(int.MaxValue)]
        public IEnumerator Run()
        {
            var configPath = Environment.GetEnvironmentVariable("GUN_RESEARCH_CONFIG");
            if (string.IsNullOrEmpty(configPath)) configPath = Path.Combine(SlotRoot, "research", "gun", "config.json");
            if (!File.Exists(configPath)) Assert.Ignore($"no gun research config at {configPath}");
            var batch = JsonUtility.FromJson<Batch>(File.ReadAllText(configPath));
            var outDir = Path.Combine(SlotRoot, "results", "gun-research");
            Directory.CreateDirectory(outDir);
            File.Copy(configPath, Path.Combine(outDir, $"{batch.runs[0].tag}.batch.json"), true);
            foreach (var config in batch.runs)
                yield return RunConfig(config, outDir);
        }

        private IEnumerator RunConfig(Config config, string outDir)
        {
            var outPath = Path.Combine(outDir, $"{config.tag}.jsonl");
            File.WriteAllText(outPath, string.Empty);

            var pairings = new List<(string a, string b)>();
            if (config.mode == "marks")
            {
                foreach (var w in config.weapons)
                foreach (var t in config.opponents)
                    pairings.Add((w, t));
            }
            else
            {
                var opp = config.opponents != null && config.opponents.Length > 0 ? config.opponents : config.weapons;
                var same = opp == config.weapons;
                for (var i = 0; i < config.weapons.Length; i++)
                for (var j = 0; j < opp.Length; j++)
                {
                    if (same && !config.ordered && j < i) continue;
                    pairings.Add((config.weapons[i], opp[j]));
                }
            }

            var started = DateTime.UtcNow;
            var simSeconds = 0f;
            foreach (var (aName, bName) in pairings)
            {
                var rows = new List<Row>();
                yield return RunPairing(config, aName, bName, rows);
                using (var w = new StreamWriter(outPath, append: true, new UTF8Encoding(false)))
                    foreach (var row in rows)
                    {
                        w.WriteLine(JsonUtility.ToJson(row));
                        simSeconds += row.duration;
                    }
                Debug.Log($"[gun-research] {config.tag} {aName} vs {bName}: {rows.Count} episodes, wall {(DateTime.UtcNow - started).TotalSeconds:F0}s, sim {simSeconds:F0}s");
            }
        }

        private IEnumerator RunPairing(Config config, string aName, string bName, List<Row> rows)
        {
            var marks = config.mode == "marks";
            var weaponA = LoadWeapon(aName);
            var weaponB = marks ? null : LoadWeapon(bName);
            var shipA = Spawn(weaponA, LoadPilot(config.a.pilot), 0, config.seed, config.a.doctrine);
            var shipB = Spawn(weaponB, LoadPilot(marks ? "ArchetypePilot" : config.b.pilot), 1, config.seed + 1,
                config.b.doctrine);
            var harnessField = config.field
                ? HarnessField.Spawn(Vector2.zero, assets, config.fieldDensity, arenaHost.transform, presentationEnabled: false)
                : null;
            if (!loggedBounds)
            {
                loggedBounds = true;
                foreach (var col in shipA.GetComponentsInChildren<Collider>())
                    Debug.Log($"[gun-research] collider {col.name} {col.GetType().Name} trigger={col.isTrigger} extents={col.bounds.extents} shipRadius={shipA.Dynamics.shipRadius}");
            }
            units.WireShipDependencies(shipA, harnessField?.Field);
            units.WireShipDependencies(shipB, harnessField?.Field);

            ArchetypeBrain targetBrain = null;
            var targetArchetype = OpponentArchetype.Dummy;
            if (marks)
            {
                targetArchetype = (OpponentArchetype)Enum.Parse(typeof(OpponentArchetype), bName);
                targetBrain = shipB.GetComponentInChildren<AICommander>().InstallBrain<ArchetypeBrain>();
            }

            var maxSteps = Mathf.RoundToInt(config.timeoutSec / Time.fixedDeltaTime);
            try
            {
                for (var e = 0; e < config.episodes; e++)
                {
                    var poseSeed = config.seed * 1000 + e / 2;
                    var swapped = (e & 1) == 1;
                    var rng = new System.Random(poseSeed);
                    var dist = Mathf.Lerp(config.spawnMin, config.spawnMax, (float)rng.NextDouble());
                    var bearing = (float)rng.NextDouble() * 360f;
                    var dir = new Vector2(Mathf.Cos(bearing * Mathf.Deg2Rad), Mathf.Sin(bearing * Mathf.Deg2Rad));
                    var p1 = -dir * dist * 0.5f;
                    var p2 = dir * dist * 0.5f;
                    var r1 = (float)rng.NextDouble() * 360f;
                    var r2 = (float)rng.NextDouble() * 360f;
                    var (posA, rotA, posB, rotB) = swapped ? (p2, r2, p1, r1) : (p1, r1, p2, r2);

                    if (targetBrain)
                        targetBrain.Configure(shipA, targetArchetype,
                            new OpponentDraw { speedFraction = 0.85f, jukePeriod = 1.2f, orbitRadius = 16f, orbitDirection = swapped ? -1 : 1, desiredRange = 10f },
                            poseSeed, Vector2.zero, 500f);
                    harnessField?.Rebuild(HarnessField.DeriveLayoutSeed(config.seed, e / 2), posA, posB);
                    units.RespawnShip(shipA.Id, posA, rotA);
                    units.RespawnShip(shipB.Id, posB, rotB);
                    projectiles.ReturnAllToPool();

                    var sa = new MarksmanshipSampler(shipA, shipB);
                    var sb = marks ? null : new MarksmanshipSampler(shipB, shipA);
                    var tolA = Tolerance(weaponA);
                    var stA = new StepStats(shipA, shipB, tolA);
                    var stB = new StepStats(shipB, shipA, marks ? 0f : Tolerance(weaponB));
                    var steps = 0;
                    try
                    {
                        while (steps < maxSteps)
                        {
                            yield return waitFixed;
                            steps++;
                            if (!Alive(shipA) || !Alive(shipB)) break;
                            sa.Sample();
                            sb?.Sample();
                            stA.Sample();
                            stB.Sample();
                        }

                        var aAlive = Alive(shipA);
                        var bAlive = Alive(shipB);
                        var row = new Row
                        {
                            tag = config.tag,
                            mode = config.mode,
                            pairing = $"{aName}|{bName}",
                            episode = e,
                            poseSeed = poseSeed,
                            swapped = swapped,
                            winner = aAlive && bAlive ? "draw" : aAlive ? "A" : bAlive ? "B" : "both",
                            duration = steps * Time.fixedDeltaTime,
                            a = new SideStats { weapon = aName, doctrine = config.a.doctrine, tally = sa.Tally },
                            b = new SideStats { weapon = bName, doctrine = config.b.doctrine, tally = sb?.Tally ?? default },
                        };
                        stA.Fill(row.a);
                        stB.Fill(row.b);
                        rows.Add(row);
                    }
                    finally
                    {
                        sa.Dispose();
                        sb?.Dispose();
                        stA.Dispose();
                        stB.Dispose();
                    }
                }
            }
            finally
            {
                harnessField?.Dispose();
                Remove(shipA);
                Remove(shipB);
                projectiles.ReturnAllToPool();
            }
        }

        private static float Tolerance(WeaponComponent weapon)
        {
            if (!weapon) return 0f;
            var so = new SerializedObject(weapon);
            var p = so.FindProperty("fireAngleTolerance") ?? so.FindProperty("fallbackAngleTolerance");
            return p != null ? p.floatValue : 5f;
        }

        private Ship Spawn(WeaponComponent weapon, AICommander pilot, int team, int seed, string doctrineJson)
        {
            var ship = Factory.CreateShip(assets.ShipPrefab, pilot, team, seed, projectiles,
                GamePlane.PlanePointToWorld(Vector2.zero), GamePlane.Rotation);
            ship.transform.SetParent(units.UnitsRoot, true);
            units.ActiveRegistry.ActiveShips.Add(ship);
            ship.Reequip(ship.Engine, ship.Shield, weapon, null);
            var doctrine = ship.gameObject.AddComponent<GunDoctrine>();
            if (!string.IsNullOrEmpty(doctrineJson)) JsonUtility.FromJsonOverwrite(doctrineJson, doctrine);
            doctrine.ApplySkill();
            var nav = ship.GetComponentInChildren<AICommander>()?.Navigator;
            if (nav && nav.mpcSettings && (doctrine.mpcFacingWidth > 0f || doctrine.mpcYawRate >= 0f))
            {
                // The next respawn rebuilds the solver from this per-ship clone; the asset is never written.
                var clone = UnityEngine.Object.Instantiate(nav.mpcSettings);
                if (doctrine.mpcFacingWidth > 0f) clone.facingWidth = doctrine.mpcFacingWidth;
                if (doctrine.mpcYawRate >= 0f) clone.wYawRate = doctrine.mpcYawRate;
                nav.mpcSettings = clone;
                if (!loggedMpc)
                {
                    loggedMpc = true;
                    Debug.Log($"[gun-research] mpc override facingWidth={clone.facingWidth} wYawRate={clone.wYawRate} wFacing={clone.wFacing}");
                }
            }
            return ship;
        }

        private void Remove(Ship ship)
        {
            if (!ship) return;
            units.ActiveRegistry.ActiveShips.Remove(ship);
            UnityEngine.Object.DestroyImmediate(ship.gameObject);
        }

        private static bool Alive(Ship ship) => ship.Damage.Health.CurrentValue > 0f;

        private static WeaponComponent LoadWeapon(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Weapons/{name}.prefab");
            var weapon = go ? go.GetComponent<WeaponComponent>() : null;
            if (!weapon) throw new ArgumentException($"no weapon prefab named {name}");
            return weapon;
        }

        private static AICommander LoadPilot(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Pilots/{name}.prefab");
            var pilot = go ? go.GetComponent<AICommander>() : null;
            if (!pilot) throw new ArgumentException($"no pilot prefab named {name}");
            return pilot;
        }
    }
}
#endif
