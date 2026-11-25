using System;
using System.Collections.Generic;
using Substrate;
using UnityEngine;

namespace Ships.Hitbox
{
    /// <summary>
    /// Sets the depth of each hitbox slab at spawn. Ships roll up to their bank angle and shots test
    /// only the slab's slice at the play plane, so depth decides how much of the width stays hittable
    /// at full bank: the shared share times this ship's body or wing scale. Hitboxes are the exported
    /// outline slabs on the Ship layer, each refit along the mesh axis it was extruded on, so a
    /// swinging wing's hitbox keeps its tilt.
    /// It runs in Start, after Ship.Initialize has derived the ship radius from the exported outlines,
    /// so tuning depth never moves AI spacing or ram reach.
    /// </summary>
    [RequireComponent(typeof(Ship))]
    public sealed class BankHitbox : MonoBehaviour
    {
        [Tooltip("The shared hitbox knob.")]
        [SerializeField] private HitboxFeelProfile feel;

        [Tooltip("Multiplies the shared hittable share for every hitbox not listed under Wings.")]
        [SerializeField, Min(.05f)] private float bodyScale = 1f;

        [Tooltip("Multiplies the shared hittable share for the Wings hitboxes.")]
        [SerializeField, Min(.05f)] private float wingScale = 1f;

        [Tooltip("Hitboxes that ride swinging wings. Their width is measured at the authored wing pose.")]
        [SerializeField] private MeshCollider[] wings = Array.Empty<MeshCollider>();

        private readonly List<Mesh> fitted = new();

        private void Start()
        {
            if (!feel)
                throw new InvalidOperationException($"{name}: BankHitbox has no HitboxFeelProfile.");
            var bank = GetComponent<Ship>().maxBankAngle * Mathf.Deg2Rad;
            var bankDepth = Mathf.Sin(bank) * Mathf.Cos(bank);
            if (bankDepth <= 0f)
                throw new InvalidOperationException($"{name}: a ship that never banks needs no BankHitbox.");
            foreach (var hitbox in GetComponentsInChildren<MeshCollider>(true))
            {
                if (hitbox.isTrigger || !hitbox.sharedMesh || hitbox.gameObject.layer != LayerIds.Ship) continue;
                var scale = Array.IndexOf(wings, hitbox) >= 0 ? wingScale : bodyScale;
                Fit(hitbox, feel.BankHittableShare * scale * bankDepth);
            }
        }

        private void OnDestroy()
        {
            foreach (var mesh in fitted) Destroy(mesh);
        }

        private void Fit(MeshCollider hitbox, float depthPerHalfWidth)
        {
            var toShip = transform.worldToLocalMatrix * hitbox.transform.localToWorldMatrix;
            var vertices = hitbox.sharedMesh.vertices;
            var axis = SlabAxis(vertices);
            if (axis < 0)
                throw new InvalidOperationException(
                    $"{name}: hitbox {hitbox.name} is not a slab across the play plane; re-export it with ship_export.");
            var halfWidth = 0f;
            foreach (var vertex in vertices)
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(toShip.MultiplyPoint3x4(vertex).x));
            var unit = Vector3.zero;
            unit[axis] = 1f;
            var depth = depthPerHalfWidth * halfWidth / toShip.MultiplyVector(unit).magnitude;
            for (var i = 0; i < vertices.Length; i++)
                vertices[i][axis] = Mathf.Sign(vertices[i][axis]) * depth;
            var mesh = Instantiate(hitbox.sharedMesh);
            mesh.name = hitbox.sharedMesh.name;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            hitbox.sharedMesh = mesh;
            fitted.Add(mesh);
        }

        private static int SlabAxis(Vector3[] vertices)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                var slab = Mathf.Abs(vertices[0][axis]);
                if (slab <= 0f) continue;
                var flat = true;
                foreach (var vertex in vertices)
                    flat &= Mathf.Abs(Mathf.Abs(vertex[axis]) - slab) <= slab * .01f;
                if (flat) return axis;
            }
            return -1;
        }
    }
}
