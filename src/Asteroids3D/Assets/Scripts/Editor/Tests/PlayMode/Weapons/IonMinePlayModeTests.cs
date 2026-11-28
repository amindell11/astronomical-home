#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Projectiles;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;
using NUnit.Framework;
using Ships;
using Ships.Status;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Weapons
{
    [Category("Weapons")]
    public class IonMinePlayModeTests : PlayModeWorldFixture
    {
        private const string PrefabPath = "Assets/Prefabs/Weapons/IonMines.prefab";
        private const string VirusModPath = "Assets/Settings/Weapons/Mods/Behaviours/Virus.asset";
        private const string FieldModPath = "Assets/Settings/Weapons/Mods/Behaviours/StaticField.asset";
        private const float Pad = 0.25f;

        private readonly List<Ship> ships = new();

        public override void TearDown()
        {
            foreach (var ship in ships)
                if (ship) Object.DestroyImmediate(ship.gameObject);
            ships.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Drop_FromAMovingCarrier_SitsBehindTheHull_AtRest()
        {
            var (carrier, mines) = Arm(moving: true);
            var forward = mines.FireDirection;
            Assert.That(Vector3.Dot(forward, carrier.transform.up), Is.GreaterThan(0.99f));
            carrier.Rigidbody.linearVelocity = forward * 12f;
            var origin = carrier.transform.position;
            var mine = Fire(mines);
            var body = mine.GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.None;
            Assert.That(Vector3.Distance(mine.transform.position, Drop(carrier, mines, forward)), Is.LessThan(0.05f));
            Assert.That(Vector3.Distance(origin, mine.transform.position), Is.GreaterThan(carrier.ShipRadius));
            Assert.That(Vector3.Dot(mine.transform.position - origin, forward), Is.LessThan(0f));
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(0.01f));
            var dropped = mine.transform.position;
            for (var i = 0; i < 8; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(mine.transform.position, dropped), Is.LessThan(0.05f));
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(0.01f));
            Assert.That(Vector3.Dot(carrier.transform.position - origin, forward), Is.GreaterThan(0.2f));
        }

        [Test]
        public void Drop_WithTheMuzzleAimedBackward_StillSitsBehindTheHull()
        {
            var (carrier, mines) = Arm();
            var heading = carrier.transform.up;
            mines.transform.rotation = Quaternion.AngleAxis(180f, GamePlane.Normal) * mines.transform.rotation;
            Assert.That(Vector3.Dot(mines.FireDirection, heading), Is.LessThan(-0.99f));
            var mine = Fire(mines);
            Assert.That(Vector3.Distance(mine.transform.position, Drop(carrier, mines, heading)), Is.LessThan(0.05f));
        }

        [Test]
        public void DamageMods_DoNotFitTheMine()
        {
            var (_, mines) = Arm();
            Assert.IsFalse(mines.Supports(Combat.Weapons.Mods.WeaponModField.Damage));
        }

        [UnityTest]
        public IEnumerator SecondFire_WhileTheMineIsLive_SpendsNothing_AndFiresAgainOnceItIsGone()
        {
            var (_, mines) = Arm();
            var before = mines.Rounds.AmmoCount;
            var mine = Fire(mines);
            Assert.That(mines.Rounds.AmmoCount, Is.EqualTo(before - 1));
            Assert.IsNull(mines.Fire(Vector3.zero, Projectiles));
            Assert.That(mines.Rounds.AmmoCount, Is.EqualTo(before - 1));
            var cooldown = mines.GetComponent<Cooldown>();
            var budget = Mathf.CeilToInt((mine.FuseTime + cooldown.SecondsBetweenShots) / Time.fixedDeltaTime) + 5;
            yield return Until(() => !mine.gameObject.activeSelf, budget);
            Assert.IsFalse(mine.gameObject.activeSelf);
            yield return Until(() => mines.CanFire(), budget);
            Assert.IsNotNull(mines.Fire(Vector3.zero, Projectiles));
        }

        [UnityTest]
        public IEnumerator Proximity_WaitsUntilArmed_ThenStallsTheEnemy()
        {
            var (carrier, mines) = Arm();
            var forward = mines.FireDirection;
            var spec = mines.projectilePrefab;
            var enemy = Ship(1, Drop(carrier, mines, forward) + Beside(carrier, spec));
            Physics.SyncTransforms();
            var mine = Fire(mines);
            for (var i = 0; i < StepsBefore(spec.ArmingTime); i++)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(mine.gameObject.activeSelf, "an enemy in proximity before arming does not set it off");
            Assert.IsFalse(enemy.Status.Has(StatusEffect.Stalled));
            for (var i = 0; i < 3 && mine.gameObject.activeSelf; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(mine.gameObject.activeSelf);
            yield return Until(() => enemy.Status.Has(StatusEffect.Stalled), 50);
            Assert.IsTrue(enemy.Status.Has(StatusEffect.Stalled), "the blast front reaches it");
        }

        [UnityTest]
        public IEnumerator Fuse_GoesOffWithNoEnemyNear()
        {
            var (_, mines) = Arm();
            var mine = Fire(mines);
            for (var i = 0; i < StepsBefore(mine.FuseTime); i++)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(mine.gameObject.activeSelf, "it waits out the fuse");
            for (var i = 0; i < 3 && mine.gameObject.activeSelf; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(mine.gameObject.activeSelf, "it goes off at the fuse");
        }

        [UnityTest]
        public IEnumerator Blast_StallsEnemiesOnly_AndDealsNoDamage()
        {
            var (carrier, mines) = Arm();
            var forward = mines.FireDirection;
            var spec = mines.projectilePrefab;
            var drop = Drop(carrier, mines, forward);
            var beside = Beside(carrier, spec);
            Assert.That(Vector3.Distance(drop, carrier.transform.position), Is.LessThan(mines.BlastRadius));
            Assert.That(beside.magnitude, Is.LessThan(mines.BlastRadius));
            var enemy = Ship(1, drop + beside);
            var ally = Ship(0, drop - beside);
            Physics.SyncTransforms();
            var enemyHealth = enemy.Damage.Health.CurrentValue;
            var enemyShield = enemy.Damage.Shield.CurrentValue;
            var allyHealth = ally.Damage.Health.CurrentValue;
            IonBlast blast = null;
            var mine = Fire(mines);
            mine.Spawned += (spawned, _) => blast = spawned as IonBlast;
            yield return Until(() => blast && !blast.gameObject.activeSelf, 600);
            Assert.That(enemy.Status.Has(StatusEffect.Stalled), Is.True);
            Assert.That(carrier.Status.Has(StatusEffect.Stalled), Is.False);
            Assert.That(ally.Status.Has(StatusEffect.Stalled), Is.False);
            Assert.That(enemy.Damage.Health.CurrentValue, Is.EqualTo(enemyHealth));
            Assert.That(enemy.Damage.Shield.CurrentValue, Is.EqualTo(enemyShield));
            Assert.That(ally.Damage.Health.CurrentValue, Is.EqualTo(allyHealth));
        }

        [UnityTest]
        public IEnumerator StalledShield_DoesNotRegenerate_UntilTheStallEnds()
        {
            var (carrier, mines) = Arm();
            var enemy = Ship(1, Drop(carrier, mines, mines.FireDirection) + Beside(carrier, mines.projectilePrefab));
            Physics.SyncTransforms();
            var shield = enemy.Damage.Shield;
            Assert.That(shield.MaxValue, Is.GreaterThan(0f));
            Assert.That(enemy.Damage.shieldRegenRate, Is.GreaterThan(0f));
            shield.ApplyDamage(shield.MaxValue * 0.8f);
            var damaged = shield.CurrentValue;
            var cap = Mathf.CeilToInt((enemy.Damage.shieldRegenDelay + 1f) / Time.fixedDeltaTime) + 10;
            for (var i = 0; i < cap && shield.CurrentValue <= damaged + 0.01f; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(shield.CurrentValue, Is.GreaterThan(damaged));
            Fire(mines);
            for (var i = 0; i < cap && !enemy.Status.Has(StatusEffect.Stalled); i++)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(enemy.Status.Has(StatusEffect.Stalled));
            var held = shield.CurrentValue;
            var stallSteps = Mathf.Max(1, Mathf.FloorToInt(enemy.Status.Remaining(StatusEffect.Stalled) / Time.fixedDeltaTime) - 2);
            for (var i = 0; i < stallSteps; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(shield.IsRegenerating, Is.False);
                Assert.That(shield.CurrentValue, Is.EqualTo(held).Within(0.001f));
            }
            for (var i = 0; i < cap && enemy.Status.Has(StatusEffect.Stalled); i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(enemy.Status.Has(StatusEffect.Stalled));
            var paused = shield.CurrentValue;
            for (var i = 0; i < 20; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(shield.CurrentValue, Is.GreaterThan(paused));
        }

        [UnityTest]
        public IEnumerator RepeatStall_WithinTheWindow_IsShorter()
        {
            var (carrier, mines) = Arm();
            var spec = mines.projectilePrefab;
            var blast = spec.BlastPrefab;
            var cooldown = mines.GetComponent<Cooldown>().SecondsBetweenShots;
            Assert.That(Mathf.Max(blast.StallSeconds, cooldown) + spec.ArmingTime, Is.LessThan(blast.RepeatWindow));
            var enemy = Ship(1, Drop(carrier, mines, mines.FireDirection) + Beside(carrier, spec));
            Physics.SyncTransforms();
            Fire(mines);
            yield return Until(() => enemy.Status.Has(StatusEffect.Stalled), 300);
            Assert.That(enemy.Status.Remaining(StatusEffect.Stalled), Is.EqualTo(blast.StallSeconds).Within(0.15f));
            yield return Until(() => !enemy.Status.Has(StatusEffect.Stalled), 400);
            yield return Until(() => mines.CanFire(), 400);
            Fire(mines);
            yield return Until(() => enemy.Status.Has(StatusEffect.Stalled), 300);
            Assert.That(enemy.Status.Remaining(StatusEffect.Stalled),
                Is.EqualTo(blast.StallSeconds * blast.RepeatScale).Within(0.15f));
        }

        [UnityTest]
        public IEnumerator Virus_PassesTheStallToAnAllyOutsideTheBlast_AndSkipsTheCarriersTeam()
        {
            var (carrier, mines) = Arm();
            Hold(mines, VirusModPath);
            var spec = mines.projectilePrefab;
            var blast = spec.BlastPrefab;
            var virus = spec.VirusPrefab;
            var axis = GamePlane.Right;
            var source = Ship(1, axis * 200f);
            var hull = HullAlong(source, axis);
            var inside = blast.MaxRadius - hull - Pad;
            var outside = BlastReach(blast) + hull + Pad;
            Assert.That(inside - hull, Is.GreaterThan(spec.ProximityRadius),
                "test premise: the stalled ship is outside proximity");
            Assert.That(outside - inside - hull, Is.LessThan(virus.SpreadRadius),
                "test premise: spread reaches an ally parked outside the blast");

            var ally = Ship(1, axis * 220f);
            var friend = Ship(0, axis * 240f);
            var mine = Fire(mines);
            var origin = mine.transform.position;
            var sourceAt = origin + axis * inside;
            var allyAt = origin + axis * outside;
            var friendAt = allyAt + Vector3.Cross(axis, GamePlane.Normal).normalized * (hull + Pad);
            Park(source, sourceAt);
            Park(ally, allyAt);
            Park(friend, friendAt);
            Assert.That(Vector3.Distance(allyAt, origin) - hull, Is.GreaterThan(BlastReach(blast)));
            Assert.That(Vector3.Distance(friendAt, origin) - hull, Is.GreaterThan(BlastReach(blast)));
            Assert.That(Vector3.Distance(allyAt, sourceAt) - hull, Is.LessThan(virus.SpreadRadius));
            Assert.That(Vector3.Distance(friendAt, sourceAt) - hull, Is.LessThan(virus.SpreadRadius));

            yield return Until(() => source.Status.Has(StatusEffect.Stalled), 600);
            Assert.IsTrue(source.Status.Has(StatusEffect.Stalled), "the blast stalls the enemy");
            yield return Until(() => ally.Status.Has(StatusEffect.Stalled), 20);
            Assert.IsTrue(ally.Status.Has(StatusEffect.Stalled), "the stall passes to its ally");
            Assert.IsFalse(friend.Status.Has(StatusEffect.Stalled), "a ship of the carrier's team in that spot is never stalled");
            Assert.IsFalse(carrier.Status.Has(StatusEffect.Stalled));
        }

        [UnityTest]
        public IEnumerator Virus_StopsAtMaxShips_AndStallsNoneTwice()
        {
            var (_, mines) = Arm();
            Hold(mines, VirusModPath);
            var spec = mines.projectilePrefab;
            var blast = spec.BlastPrefab;
            var virus = spec.VirusPrefab;
            var axis = GamePlane.Right;
            var sample = Ship(1, axis * 200f);
            var hull = HullAlong(sample, axis);
            var inside = blast.MaxRadius - hull - Pad;
            var hop = virus.SpreadRadius - hull - Pad;
            Assert.That(hop, Is.GreaterThan(0f), "test premise: spread clears a hull");
            Assert.That(inside - hull, Is.GreaterThan(spec.ProximityRadius),
                "test premise: the first ship is outside proximity");
            Assert.That(inside + hop - hull, Is.GreaterThan(BlastReach(blast)),
                "test premise: the next ally is outside the blast");

            var line = new Ship[virus.MaxShips + 1];
            line[0] = sample;
            for (var i = 1; i < line.Length; i++)
                line[i] = Ship(1, axis * (220f + i * 20f));
            var mine = Fire(mines);
            var origin = mine.transform.position;
            for (var i = 0; i < line.Length; i++)
                Park(line[i], origin + axis * (inside + i * hop));
            for (var i = 1; i < line.Length; i++)
                Assert.That(Vector3.Distance(line[i].transform.position, line[i - 1].transform.position) - hull,
                    Is.LessThan(virus.SpreadRadius), "test premise: each ally is inside the spread of the one before it");

            for (var i = 0; i < virus.MaxShips; i++)
            {
                yield return Until(() => line[i].Status.Has(StatusEffect.Stalled), i == 0 ? 600 : 20);
                Assert.That(line[i].Status.Remaining(StatusEffect.Stalled),
                    Is.EqualTo(blast.StallSeconds).Within(0.15f), $"ship {i} caught once");
            }
            for (var i = 0; i < 8; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(line[virus.MaxShips].Status.Has(StatusEffect.Stalled), "the cap stops the pass");
        }

        [UnityTest]
        public IEnumerator StaticField_StallsAnEnemyEnteringAfterTheBlast_AndIgnoresTheCarriersTeam()
        {
            var (_, mines) = Arm();
            Hold(mines, FieldModPath);
            var spec = mines.projectilePrefab;
            var blast = spec.BlastPrefab;
            var field = spec.FieldPrefab;
            Assert.That(blast.MaxRadius / blast.ExpandSpeed + 0.5f, Is.LessThan(field.Seconds),
                "test premise: the field outlasts the blast");

            var axis = GamePlane.Right;
            var friend = Ship(0, axis * 200f);
            var arriving = Ship(1, axis * 220f);
            var late = Ship(1, axis * 240f);
            var hull = HullAlong(friend, axis);
            var friendDist = hull + 1.5f;
            Assert.That(friendDist + hull, Is.LessThan(field.Radius),
                "test premise: the carrier-team ship sits inside the field");
            IonBlast wave = null;
            IonStaticField zone = null;
            var mine = Fire(mines);
            mine.Spawned += (spawned, _) =>
            {
                var asBlast = spawned as IonBlast;
                if (asBlast) wave = asBlast;
                var asField = spawned as IonStaticField;
                if (asField) zone = asField;
            };
            var origin = mine.transform.position;
            Park(friend, origin + axis * friendDist);

            yield return Until(() => wave && !wave.gameObject.activeSelf, 600);
            Assert.That(zone && zone.gameObject.activeSelf, Is.True, "the field is still up after the blast");
            Assert.IsFalse(arriving.Status.Has(StatusEffect.Stalled));
            Assert.IsFalse(friend.Status.Has(StatusEffect.Stalled), "a carrier-team ship inside is never stalled");

            Park(arriving, origin);
            yield return Until(() => arriving.Status.Has(StatusEffect.Stalled), 15);
            Assert.IsTrue(arriving.Status.Has(StatusEffect.Stalled), "flying into the field stalls it");
            Assert.IsFalse(friend.Status.Has(StatusEffect.Stalled), "a carrier-team ship inside is never stalled");

            var budget = Mathf.CeilToInt(field.Seconds / Time.fixedDeltaTime) + 10;
            yield return Until(() => zone && !zone.gameObject.activeSelf, budget);
            Assert.IsFalse(zone.gameObject.activeSelf, "the field ends");
            Park(late, origin);
            for (var i = 0; i < 8; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(late.Status.Has(StatusEffect.Stalled), "an enemy who enters after the field is not stalled");
        }

        [UnityTest]
        public IEnumerator StaticFieldAndVirus_AFieldStallPassesToAnAllyNearby()
        {
            var (_, mines) = Arm();
            Hold(mines, VirusModPath, FieldModPath);
            var spec = mines.projectilePrefab;
            var blast = spec.BlastPrefab;
            var field = spec.FieldPrefab;
            var virus = spec.VirusPrefab;
            Assert.That(blast.MaxRadius / blast.ExpandSpeed + 0.5f, Is.LessThan(field.Seconds),
                "test premise: the field outlasts the blast");

            var axis = GamePlane.Right;
            var source = Ship(1, axis * 200f);
            var ally = Ship(1, axis * 220f);
            var hull = HullAlong(source, axis);
            var sourceDist = field.Radius - hull - Pad;
            var allyDist = field.Radius + hull + Pad;
            Assert.That(sourceDist, Is.GreaterThan(0f));
            Assert.That(sourceDist + hull, Is.LessThan(field.Radius));
            Assert.That(allyDist - hull, Is.GreaterThan(field.Radius), "the ally stays outside the field");
            Assert.That(allyDist - sourceDist - hull, Is.LessThan(virus.SpreadRadius),
                "test premise: the ally outside the field is inside the spread");

            IonBlast wave = null;
            IonStaticField zone = null;
            var mine = Fire(mines);
            mine.Spawned += (spawned, _) =>
            {
                var asBlast = spawned as IonBlast;
                if (asBlast) wave = asBlast;
                var asField = spawned as IonStaticField;
                if (asField) zone = asField;
            };
            var origin = mine.transform.position;

            yield return Until(() => wave && !wave.gameObject.activeSelf, 600);
            Assert.That(zone && zone.gameObject.activeSelf, Is.True);
            Assert.IsFalse(source.Status.Has(StatusEffect.Stalled));
            Assert.IsFalse(ally.Status.Has(StatusEffect.Stalled));

            Park(source, origin + axis * sourceDist);
            Park(ally, origin + axis * allyDist);
            yield return Until(() => source.Status.Has(StatusEffect.Stalled), 15);
            Assert.IsTrue(source.Status.Has(StatusEffect.Stalled), "the field stalls the enemy");
            yield return Until(() => ally.Status.Has(StatusEffect.Stalled), 15);
            Assert.IsTrue(ally.Status.Has(StatusEffect.Stalled), "that stall passes to its ally");
        }

        [UnityTest]
        public IEnumerator WithoutEitherMod_TheMineLeavesNoFieldAndNoSpread()
        {
            var (_, mines) = Arm();
            var spec = mines.projectilePrefab;
            var blast = spec.BlastPrefab;
            var virus = spec.VirusPrefab;
            var axis = GamePlane.Right;
            var source = Ship(1, axis * 200f);
            var ally = Ship(1, axis * 220f);
            var late = Ship(1, axis * 240f);
            var hull = HullAlong(source, axis);
            var inside = blast.MaxRadius - hull - Pad;
            var outside = BlastReach(blast) + hull + Pad;
            Assert.That(inside - hull, Is.GreaterThan(spec.ProximityRadius));
            Assert.That(outside - inside - hull, Is.LessThan(virus.SpreadRadius),
                "test premise: an ally sits where the virus would pass the stall");

            var spawned = new List<MonoBehaviour>();
            IonBlast wave = null;
            var mine = Fire(mines);
            mine.Spawned += (child, _) =>
            {
                spawned.Add(child);
                var asBlast = child as IonBlast;
                if (asBlast) wave = asBlast;
            };
            var origin = mine.transform.position;
            Park(source, origin + axis * inside);
            Park(ally, origin + axis * outside);

            yield return Until(() => source.Status.Has(StatusEffect.Stalled), 600);
            for (var i = 0; i < 8; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(ally.Status.Has(StatusEffect.Stalled), "without Virus the stall does not spread");
            Assert.That(spawned, Has.Count.EqualTo(1));
            Assert.IsNotNull(spawned[0] as IonBlast, "the mine spawns only its blast");

            yield return Until(() => wave && !wave.gameObject.activeSelf, 200);
            Park(late, origin);
            for (var i = 0; i < 8; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(late.Status.Has(StatusEffect.Stalled),
                "without Static field an enemy who arrives after the blast is not stalled");
        }

        private (Ship carrier, IonMines mines) Arm(bool moving = false)
        {
            var carrier = Ship(0, Vector3.zero, moving);
            return (carrier, Mount(carrier));
        }

        private Ship Ship(int team, Vector3 at, bool moving = false)
        {
            var ship = ShipTestFactory.CreateShip(TestAssets.LoadCrimsonPrefab(), null, Projectiles, team, at, GamePlane.Rotation);
            ships.Add(ship);
            ship.Rigidbody.interpolation = RigidbodyInterpolation.None;
            ship.Movement.enabled = false;
            if (moving) return ship;
            ship.Rigidbody.linearVelocity = Vector3.zero;
            ship.Rigidbody.constraints |= RigidbodyConstraints.FreezePosition;
            return ship;
        }

        private static IonMines Mount(Ship carrier)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<IonMines>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var mines = Object.Instantiate(prefab, carrier.transform);
            mines.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(mines.projectilePrefab);
            Assert.IsNotNull(mines.projectilePrefab.BlastPrefab);
            Assert.IsNotNull(mines.projectilePrefab.VirusPrefab);
            Assert.IsNotNull(mines.projectilePrefab.FieldPrefab);
            Assert.IsNotNull(mines.Rounds);
            return mines;
        }

        private static void Hold(IonMines mines, params string[] paths)
        {
            var mods = new WeaponBehaviourMod[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                mods[i] = TestAssets.Load<WeaponBehaviourMod>(paths[i]);
                Assert.IsNotNull(mods[i], paths[i]);
            }
            mines.Behaviours.Set(mods);
        }

        private static void Park(Ship ship, Vector3 at)
        {
            var onPlane = GamePlane.WorldPointOnPlane(at);
            ship.transform.position = onPlane;
            ship.Rigidbody.position = onPlane;
            ship.Rigidbody.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private static float HullAlong(Ship ship, Vector3 axis)
        {
            var reach = 0f;
            foreach (var collider in ship.Colliders)
            {
                if (!collider || collider.gameObject.layer != LayerIds.Ship) continue;
                var bounds = collider.bounds;
                var along = Vector3.Dot(bounds.center - ship.transform.position, axis);
                var extent = bounds.extents.x * Mathf.Abs(axis.x)
                    + bounds.extents.y * Mathf.Abs(axis.y)
                    + bounds.extents.z * Mathf.Abs(axis.z);
                reach = Mathf.Max(reach, along + extent, extent - along);
            }
            Assert.That(reach, Is.GreaterThan(0f), "test premise: the ship has a hull on the ship layer");
            return reach;
        }

        private static float BlastReach(IonBlast blast)
        {
            var step = blast.ExpandSpeed * Time.fixedDeltaTime;
            var sweeps = Mathf.Max(1, Mathf.CeilToInt(blast.MaxRadius / step - 0.0001f));
            return sweeps * step;
        }

        private IonMine Fire(IonMines mines)
        {
            var mine = (IonMine)mines.Fire(Vector3.zero, Projectiles);
            Assert.IsNotNull(mine);
            return mine;
        }

        private static Vector3 Drop(Ship carrier, IonMines mines, Vector3 forward) =>
            GamePlane.WorldPointOnPlane(carrier.transform.position - forward * (carrier.ShipRadius + mines.projectilePrefab.DropGap));

        private static Vector3 Beside(Ship carrier, IonMine spec)
        {
            var gap = carrier.ShipRadius + 1f;
            var distance = gap < spec.ProximityRadius ? gap : spec.ProximityRadius * 0.5f;
            return GamePlane.Right * distance;
        }

        private static int StepsBefore(float seconds) =>
            Mathf.FloorToInt(seconds / Time.fixedDeltaTime - 0.001f);

        private static IEnumerator Until(Func<bool> done, int maxSteps)
        {
            for (var i = 0; i < maxSteps && !done(); i++)
                yield return new WaitForFixedUpdate();
        }
    }
}
#endif
