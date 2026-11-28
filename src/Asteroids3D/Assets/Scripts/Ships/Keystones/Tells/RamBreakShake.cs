using System;
using Cameras.Shake;
using Ships.Keystones.Kits;
using Substrate;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones.Tells
{
    public sealed class RamBreakShake : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private RamDash ram;

        [Tooltip("Shake played on the ship each time Battering ram breaks through a rock.")]
        [SerializeField] private CameraShakeProfile profile;

        private Ship ship;

        private void Awake()
        {
            if (!ram || !profile) throw new InvalidOperationException($"{nameof(RamBreakShake)} on '{name}' needs its ram and shake.");
            ship = GetComponentInParent<Ship>();
        }

        private void OnEnable() => ram.BatteredThrough += Shake;

        private void OnDisable() => ram.BatteredThrough -= Shake;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void Shake(Vector3 point)
        {
            // A kit instantiated without a ship, as tests do, has nothing to shake.
            if (!ship) return;
            ship.Shake.Raise(this, profile, GamePlane.WorldDirToPlane(point - ship.transform.position));
        }
    }
}
