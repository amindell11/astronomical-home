using System;
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
        private Rigidbody body;
        private IDamageEvents source;
        private bool subscribed;

        private void Awake()
        {
            body = GetComponentInParent<Rigidbody>();
            if (!hull || !debrisPrefab)
                throw new InvalidOperationException("Ship breakup requires an intact hull and debris prefab.");
            if (poseSources.Length != debrisPrefab.PoseCount)
                throw new InvalidOperationException("Ship breakup pose sources must match the debris pose roots.");
            foreach (var pose in poseSources)
                if (!pose)
                    throw new InvalidOperationException("Ship breakup requires every authored pose source.");
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
            debris.Initialize(body.linearVelocity, poseSources);
            hull.gameObject.SetActive(false);
        }
    }
}
