using System;
using System.Collections.Generic;
using Damage;
using Ships.Damage;
using Ships.Presentation;
using Ships.Registry;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ships.Visuals.Breakup
{
    public sealed class ShipBreakupVisual : MonoBehaviour, IShipVisual
    {
        [SerializeField] private Transform hull;
        [SerializeField] private ShipBreakupDebris debrisPrefab;
        [SerializeField] private Transform[] poseSources = Array.Empty<Transform>();
        [SerializeField] private DeathFeelProfile feel;
        private Rigidbody body;
        private ShipVisualRig rig;
        private Material[] authoredPaint;
        private IDamageEvents source;
        private bool subscribed;

        private void Awake()
        {
            body = GetComponentInParent<Rigidbody>();
            rig = GetComponentInParent<ShipVisualRig>(true);
            if (!hull || !debrisPrefab || !feel || !rig)
                throw new InvalidOperationException("Ship breakup requires an intact hull, debris prefab, death feel profile and visual rig.");
            if (poseSources.Length != debrisPrefab.PoseCount)
                throw new InvalidOperationException("Ship breakup pose sources must match the debris pose roots.");
            foreach (var pose in poseSources)
                if (!pose)
                    throw new InvalidOperationException("Ship breakup requires every authored pose source.");

            var count = 0;
            foreach (var surface in rig.Silhouette)
                count += surface.sharedMaterials.Length;
            authoredPaint = new Material[count];
            var next = 0;
            foreach (var surface in rig.Silhouette)
            {
                var materials = surface.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    authoredPaint[next++] = materials[i];
            }
        }

        public void Bind(in ShipView view)
        {
            if (!body)
                throw new InvalidOperationException("Ship breakup requires the ship Rigidbody above its visual rig.");
            Unsubscribe();
            source = view.Damage;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            hull.gameObject.SetActive(true);
            Subscribe();
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (subscribed || source == null) return;
            source.OnDeath += OnDeath;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            source.OnDeath -= OnDeath;
            subscribed = false;
        }

        private void OnDeath(ShipId victim, DamageInfo hit)
        {
            var debris = Instantiate(debrisPrefab, hull.position, hull.rotation);
            SceneManager.MoveGameObjectToScene(debris.gameObject, gameObject.scene);
            debris.transform.localScale = hull.lossyScale;
            debris.Initialize(body.linearVelocity, poseSources, feel, WornPaint());
            hull.gameObject.SetActive(false);
        }

        private Dictionary<Material, Material> WornPaint()
        {
            var worn = new Dictionary<Material, Material>();
            var next = 0;
            foreach (var surface in rig.Silhouette)
            {
                var materials = surface.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    worn[authoredPaint[next++]] = materials[i];
            }
            return worn;
        }
    }
}
