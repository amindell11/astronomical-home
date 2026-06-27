using Combat.Conditions;
using Combat.Weapons;
using Ships;
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
            var combatShip = player as CombatShip;

            if (lockOnAudio && combatShip?.Targeting)
                lockOnAudio.Initialize(combatShip.Targeting);

            if (healthAudio && player.Damage)
                healthAudio.Initialize(player.Damage);

            var laser = combatShip?.Weapons?.Primary as Lasers;
            var heat = laser ? laser.Heat : null;
            if (heat)
            {
                if (laserAudio) laserAudio.Initialize(heat);
                if (laserHeatUI) laserHeatUI.Initialize(heat);
            }

            var missiles = combatShip?.Weapons?.Secondary as Missiles;
            var rounds = missiles ? missiles.Rounds : null;
            var targeting = missiles ? missiles.Targeting : null;
            if (missileAmmoUI && rounds && targeting)
                missileAmmoUI.Initialize(rounds, targeting);
        }
    }
}
