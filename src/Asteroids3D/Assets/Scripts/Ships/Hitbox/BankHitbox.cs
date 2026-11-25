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
    /// outline slabs on the Ship layer; the depth axis is the ship's local z, the plane normal.
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

        private void Awake()
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
            var fromShip = toShip.inverse;
            var vertices = hitbox.sharedMesh.vertices;
            var halfWidth = 0f;
            var slab = 0f;
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] = toShip.MultiplyPoint3x4(vertices[i]);
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(vertices[i].x));
                slab = Mathf.Max(slab, Mathf.Abs(vertices[i].z));
            }
            var depth = depthPerHalfWidth * halfWidth;
            for (var i = 0; i < vertices.Length; i++)
            {
                if (Mathf.Abs(Mathf.Abs(vertices[i].z) - slab) > slab * .01f)
                    throw new InvalidOperationException(
                        $"{name}: hitbox {hitbox.name} is not a slab across the play plane; re-export it with ship_export.");
                vertices[i].z = Mathf.Sign(vertices[i].z) * depth;
                vertices[i] = fromShip.MultiplyPoint3x4(vertices[i]);
            }
            var mesh = Instantiate(hitbox.sharedMesh);
            mesh.name = hitbox.sharedMesh.name;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            hitbox.sharedMesh = mesh;
            fitted.Add(mesh);
        }
    }
}
