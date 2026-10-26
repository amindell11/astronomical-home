using System.Collections.Generic;
using Ships.Presentation;
using UnityEngine;

namespace Capture.Views
{
    internal sealed class NeutralLineupView : MonoBehaviour
    {
        private void Awake()
        {
            var hulls = new HashSet<Renderer>();
            foreach (var rig in GetComponentsInChildren<ShipVisualRig>(true))
                foreach (var renderer in rig.Silhouette) hulls.Add(renderer);
            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != this) behaviour.enabled = false;
            foreach (var canvas in GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = hulls.Contains(renderer);

            var bounds = new Bounds();
            var first = true;
            foreach (var renderer in hulls)
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            var camera = new GameObject("Lineup camera").AddComponent<Camera>();
            camera.transform.SetParent(transform, false);
            camera.orthographic = true;
            camera.aspect = 4f;
            camera.orthographicSize = Mathf.Max(bounds.extents.x / camera.aspect, bounds.extents.y) * 1.18f;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -30);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .04f, .05f);
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 100;
            camera.allowHDR = false;
            camera.depth = 100;
            var key = new GameObject("Neutral key").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.type = LightType.Directional;
            key.color = Color.white;
            key.intensity = 1.6f;
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(20, -25, 0);
        }
    }
}
