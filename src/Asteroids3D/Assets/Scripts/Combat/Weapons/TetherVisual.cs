using Combat.Projectiles.Visual;
using Combat.Weapons.Arsenal;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Weapons
{
    [RequireComponent(typeof(Harpoons), typeof(LineRenderer))]
    public sealed class TetherVisual : MonoBehaviour, IPresentationPart
    {
        private Harpoons weapon;
        private LineRenderer line;

        private void Awake()
        {
            weapon = GetComponent<Harpoons>();
            line = GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.enabled = false;
        }

        public void ApplyPresentation(bool visible)
        {
            enabled = visible;
            // Presentation can be applied before Awake.
            if (line) line.enabled = false;
        }

        private void LateUpdate()
        {
            var tether = weapon.Tether;
            if (tether != null)
                Draw(tether.NosePoint, tether.HookPoint);
            else if (weapon.InFlight)
                Draw(weapon.FireOrigin, weapon.InFlight.transform.position);
            else
                line.enabled = false;
        }

        private void Draw(Vector3 from, Vector3 to)
        {
            line.enabled = true;
            line.SetPosition(0, from + ShotDepth.Offset);
            line.SetPosition(1, to + ShotDepth.Offset);
        }
    }
}
