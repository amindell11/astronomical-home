using Combat.Conditions;
using Combat.Weapons;
using Ships;
using Ships.Weapons;
using UnityEngine;
using UI.Audio;

namespace UI
{
    [RequireComponent(typeof(Canvas))]
    public class Overlay : MonoBehaviour
    {
        [Header("Minimap")]
        [SerializeField] private RectTransform minimapRect;

        private Canvas canvas;
        private UILockOnAudio lockOnAudio;
        private UIHealthAudio healthAudio;
        private UILaserAudio laserAudio;
        private LaserHeatUI laserHeatUI;
        private MissileAmmoUI missileAmmoUI;

        public MinimapObjectiveMarker ObjectiveMarker { get; private set; }
        public RectTransform MinimapRect => minimapRect;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            lockOnAudio = GetComponentInChildren<UILockOnAudio>();
            healthAudio = GetComponentInChildren<UIHealthAudio>();
            laserAudio = GetComponentInChildren<UILaserAudio>();
            laserHeatUI = GetComponentInChildren<LaserHeatUI>();
            missileAmmoUI = GetComponentInChildren<MissileAmmoUI>();
            ObjectiveMarker = GetComponentInChildren<MinimapObjectiveMarker>(true);
        }

        public void SetCanvasWorldCamera(Camera uicam)
        {
            canvas.worldCamera = uicam;
        }

        public void Initialize(Ship player)
        {
            if (lockOnAudio && player.Targeting)
                lockOnAudio.Initialize(player.Targeting);

            if (healthAudio && player.Damage)
                healthAudio.Initialize(player.Damage);

            BindWeaponReadouts(player.Weapons);
        }

        /// <summary>
        /// Binds each weapon readout to the first equipped mount that carries its condition,
        /// scanning slots in order — no assumption about which weapon type sits in which slot.
        /// A readout with no matching condition anywhere is cleared (widgets hide themselves).
        /// One widget instance exists per condition type today; if two mounts carry the same
        /// condition, the earlier slot wins.
        /// </summary>
        private void BindWeaponReadouts(WeaponsController weapons)
        {
            var heat = FindCondition<Heat>(weapons, out _);
            if (laserAudio) laserAudio.Initialize(heat);
            if (laserHeatUI) laserHeatUI.Initialize(heat);

            var rounds = FindCondition<Rounds>(weapons, out var roundsWeapon);
            if (missileAmmoUI) missileAmmoUI.Initialize(rounds, roundsWeapon ? roundsWeapon.LockSource : null);
        }

        /// <summary>The first mount's condition of type <typeparamref name="T"/>, in slot order.</summary>
        internal static T FindCondition<T>(WeaponsController weapons, out WeaponComponent owner) where T : WeaponCondition
        {
            owner = null;
            if (!weapons) return null;

            foreach (var mount in new[] { weapons.Primary, weapons.Secondary })
            {
                var condition = mount ? mount.GetCondition<T>() : null;
                if (!condition) continue;
                owner = mount;
                return condition;
            }
            return null;
        }
    }
}
