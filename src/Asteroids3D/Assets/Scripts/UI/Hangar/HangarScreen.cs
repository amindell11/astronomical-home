using System;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using Ships;
using Ships.Loadout;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.Hangar
{
    /// <summary>
    /// Between-run hangar screen: binds the prefab-authored option cyclers to the hangar's offer (a
    /// <see cref="ItemSubset"/>) and writes picks into the pending <see cref="ShipLoadout"/>. Nothing
    /// touches the live ship — the caller installs the selection when Launch fires.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class HangarScreen : MonoBehaviour
    {
        [Header("Option rows")]
        [SerializeField] internal OptionCycler shipRow;
        [SerializeField] internal OptionCycler engineRow;
        [SerializeField] internal OptionCycler shieldRow;
        [SerializeField] internal OptionCycler primaryWeaponRow;
        [SerializeField] internal OptionCycler secondaryWeaponRow;

        [Header("Commit")]
        [SerializeField] internal Button launchButton;

        [Header("Stats readout")]
        [Tooltip("Shows the stats of the hovered row's current pick.")]
        [SerializeField] private Text statsText;

        [Header("Ship preview")]
        [Tooltip("Displays the preview stage's render texture. Null → no 3D preview.")]
        [SerializeField] private RawImage previewImage;

        [Tooltip("Continue the turntable mid-rotation when switching ships; off = each ship enters at the canonical pose (nose screen-right).")]
        [SerializeField] private bool continueSpinOnSwitch;

        private HangarPreviewStage previewStage;
        private readonly List<Action> refreshers = new();
        private Func<string> hoveredStats;

        /// <summary>Mutates <paramref name="loadout"/> in place as options are picked.</summary>
        public void Show(ItemSubset offer, ShipLoadout loadout, Action onLaunch)
        {
            if (previewImage)
            {
                // Beside the screen, not under it: the overlay canvas's pixel-space transform would drag the stage.
                previewStage = HangarPreviewStage.Create(continueSpinOnSwitch, transform.parent);
                previewImage.texture = previewStage.Texture;
                previewStage.Show(loadout);
            }

            if (offer)
            {
                // Picking a ship reseeds the module slots to that ship's authored kit.
                BindRow(shipRow, offer.ships, () => loadout.Ship, s =>
                {
                    loadout.Ship = s;
                    loadout.Engine = s.Engine;
                    loadout.Shield = s.Shield;
                    var mounts = s.Weapons;
                    loadout.PrimaryWeapon = mounts ? mounts.PrimaryMountPrefab : null;
                    loadout.SecondaryWeapon = mounts ? mounts.SecondaryMountPrefab : null;
                    if (previewStage) previewStage.Show(loadout);
                }, Describe);
                BindRow(engineRow, offer.engines, () => loadout.Engine, m => loadout.Engine = m, Describe);
                BindRow(shieldRow, offer.shields, () => loadout.Shield, m => loadout.Shield = m, Describe);
                BindRow(primaryWeaponRow, offer.weapons, () => loadout.PrimaryWeapon,
                    w => loadout.PrimaryWeapon = w, Describe, WeaponLabel);
                BindRow(secondaryWeaponRow, offer.weapons, () => loadout.SecondaryWeapon,
                    w => loadout.SecondaryWeapon = w, Describe, WeaponLabel);
            }

            Refresh();

            if (launchButton)
            {
                launchButton.onClick.RemoveAllListeners();
                launchButton.onClick.AddListener(() => onLaunch?.Invoke());
            }
        }

        private void BindRow<T>(OptionCycler row, IReadOnlyList<T> offered, Func<T> getCurrent, Action<T> setCurrent,
            Func<T, string> describe, Func<T, string> label = null)
            where T : UnityEngine.Object
        {
            if (!row || offered == null) return;

            var options = new List<T>();
            foreach (var option in offered)
                if (option) options.Add(option);

            if (options.Count > 0)
                row.Stepped += step =>
                {
                    var i = options.IndexOf(getCurrent());
                    // A pick from outside the offer enters the cycle at whichever end the step points to.
                    if (i < 0 && step < 0) i = options.Count;
                    setCurrent(options[(i + step + options.Count) % options.Count]);
                    Refresh();
                };

            refreshers.Add(() =>
            {
                var current = getCurrent();
                row.Label = !current ? "None" : label != null ? label(current) : current.name;
            });
            AddHoverStats(row.gameObject, () =>
            {
                var current = getCurrent();
                return current ? describe(current) : "";
            });
        }

        private void AddHoverStats(GameObject row, Func<string> stats)
        {
            if (!statsText) return;
            var trigger = row.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                hoveredStats = stats;
                Refresh();
            });
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ =>
            {
                hoveredStats = null;
                Refresh();
            });
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        private static string Describe(Ship ship) =>
            $"Hull {ship.maxHealth:0}   |   Mass {ship.mass:0}   |   Bank {ship.maxBankAngle:0} deg   |   " +
            $"Kit: {(ship.Engine ? ship.Engine.name : "no engine")} + {(ship.Shield ? ship.Shield.name : "no shield")}";

        private static string Describe(EngineModule engine) =>
            $"Top speed {engine.maxSpeed:0}   |   Turn {engine.maxYawRate:0} deg/s   |   Thrust {engine.forwardForce:0}   |   " +
            $"Strafe {engine.maxStrafeForce:0}   |   Boost {engine.boostImpulse:0} ({engine.boostCooldown:0.#}s cooldown)";

        private static string Describe(ShieldModule shield) =>
            $"Capacity {shield.maxShield:0}   |   Regen {shield.shieldRegenRate:0.#}/s after {shield.shieldRegenDelay:0.#}s";

        // Reads offered prefab assets, where Awake never runs: every weapon property used must be serialized state.
        internal static string Describe(WeaponComponent weapon)
        {
            var lasers = weapon as Lasers;
            if (lasers)
                return $"Damage {lasers.Damage:0}{Rate(lasers.ShotsPerSecond)}   |   Speed {lasers.ProjectileSpeed:0}" +
                       (lasers.ShotsToOverheat is int shots ? $"   |   Overheats after {shots} shots" : "");

            var chargeLasers = weapon as ChargeLasers;
            if (chargeLasers)
                return $"Damage {chargeLasers.MinChargeDamage:0}-{chargeLasers.FullChargeDamage:0}{FullCharge(chargeLasers.Charge)}" +
                       $"   |   Speed {chargeLasers.ProjectileSpeed:0}";

            var railguns = weapon as Railguns;
            if (railguns)
                return $"Damage {railguns.Damage:0}   |   Range {railguns.BeamRange:0}{FullCharge(railguns.Charge)}   |   Hitscan";

            var rippers = weapon as Rippers;
            if (rippers)
                return $"Damage {rippers.Damage:0}{Rate(rippers.ShotsPerSecond)}" +
                       (rippers.Rounds ? $"   |   Mag {rippers.Rounds.MaxAmmo}{Refill(rippers.Rounds)}" : "") +
                       $"   |   Speed {rippers.ProjectileSpeed:0}";

            var missiles = weapon as Missiles;
            if (missiles)
                return $"Damage {missiles.Damage:0} + {missiles.SplashDamage:0} splash" +
                       (missiles.Rounds ? $"   |   {missiles.Rounds.MaxAmmo} rounds{Refill(missiles.Rounds)}" : "") +
                       "   |   Lock-on homing";

            var grenades = weapon as Grenades;
            if (grenades)
                return $"Blast {grenades.BlastDamage:0} to {grenades.BlastRadius:0}u, hits friend and foe" +
                       (grenades.Rounds ? $"   |   {grenades.Rounds.MaxAmmo} charges{Refill(grenades.Rounds)}" : "") +
                       $"   |   Range {grenades.Range:0}u";

            return weapon.DisplayName;
        }

        private static string Rate(float? shotsPerSecond) =>
            shotsPerSecond is float rate ? $"   |   Rate {rate:0.#}/s" : "";

        private static string FullCharge(ChargeTime charge) =>
            charge ? $"   |   Full charge {charge.FullChargeTime:0.#}s" : "";

        private static string Refill(Rounds rounds) =>
            rounds.ReloadTime <= 0f ? ""
            : rounds.Refill == Rounds.RefillMode.PerRound ? $" (regen {rounds.ReloadTime:0.#}s/round)"
            : $" (reload {rounds.ReloadTime:0.#}s)";

        private static string WeaponLabel(WeaponComponent weapon) => weapon.DisplayName;

        private void Refresh()
        {
            foreach (var refresh in refreshers) refresh();
            if (statsText) statsText.text = hoveredStats != null ? hoveredStats() : "";
        }

        private void OnDestroy()
        {
            if (previewStage)
                Destroy(previewStage.gameObject);
        }
    }
}
