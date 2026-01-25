using Audio;
using Combat.Conditions;
using Combat.Weapons;
using Ships;
using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(Canvas))]
    public class Overlay : MonoBehaviour
    {
        private Canvas canvas;
        private UILockOnAudio lockOnAudio;
        private UIHealthAudio healthAudio;
        private UILaserAudio laserAudio;
        private LaserHeatUI laserHeatUI;
        private MissileAmmoUI missileAmmoUI;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            lockOnAudio = GetComponentInChildren<UILockOnAudio>();
            healthAudio = GetComponentInChildren<UIHealthAudio>();
            laserAudio = GetComponentInChildren<UILaserAudio>();
            laserHeatUI = GetComponentInChildren<LaserHeatUI>();
            missileAmmoUI = GetComponentInChildren<MissileAmmoUI>();
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

            var laser = player.Weapons?.Primary as WeaponLaser;
            var heat = laser ? laser.Heat : null;
            if (heat)
            {
                if (laserAudio) laserAudio.Initialize(heat);
                if (laserHeatUI) laserHeatUI.Initialize(heat);
            }

            var missiles = player.Weapons?.Secondary as WeaponMissiles;
            var rounds = missiles ? missiles.Rounds : null;
            var targeting = missiles ? missiles.Targeting : null;
            if (missileAmmoUI && rounds && targeting)
                missileAmmoUI.Initialize(rounds, targeting);
        }
    }
}
