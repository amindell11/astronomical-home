using System;
using System.Collections.Generic;
using Ships;
using Substrate;
using Substrate.Services.Projectiles;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capture.Views
{
    internal sealed class DiagnosticView : MonoBehaviour
    {
        private readonly List<Object> selection = new();
        private Object[] appliedSelection = Array.Empty<Object>();
        private DiagnosticViewConfig config;
        private IReadOnlyList<Ship> subjects;
        private IProjectileService projectiles;
        private Vector2[] positions;
        private Camera cameraView;
        private Action<Object[], Object> select;

        public void Initialize(DiagnosticViewConfig config, IReadOnlyList<Ship> subjects,
            IProjectileService projectiles, Action<Object[], Object> select)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.subjects = subjects ?? throw new ArgumentNullException(nameof(subjects));
            this.projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
            this.select = select;
            if (subjects.Count == 0) throw new ArgumentException("Diagnostic view requires a subject.");
            foreach (var subject in subjects)
                if (!subject) throw new ArgumentException("Diagnostic view received a missing subject.");
            positions = new Vector2[subjects.Count];
            BuildSelection();
            select(appliedSelection, subjects[0].gameObject);
            FrameCamera();
        }

        private void Awake()
        {
            cameraView = gameObject.AddComponent<Camera>();
            cameraView.orthographic = true;
            cameraView.cullingMask &= ~LayerMask.GetMask("Minimap", "Minimap_Ship", "Minimap_Enemy");
            cameraView.clearFlags = CameraClearFlags.SolidColor;
            cameraView.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            cameraView.depth = 100f;
            var lightRig = new GameObject("light");
            lightRig.transform.SetParent(transform);
            var light = lightRig.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.LookRotation(GamePlane.Rotation * new Vector3(0.4f, -0.3f, 1f));
        }

        private void LateUpdate()
        {
            FrameCamera();
            BuildSelection();
            select(appliedSelection, subjects[0].gameObject);
        }

        private void FrameCamera()
        {
            for (var i = 0; i < subjects.Count; i++) positions[i] = subjects[i].Kinematics.pos;
            CaptureFraming.Apply(cameraView, config, positions);
        }

        private void BuildSelection()
        {
            selection.Clear();
            foreach (var subject in subjects) AddHierarchy(subject.transform);
            projectiles.ForEachLive(projectile => AddHierarchy(projectile.transform));
            selection.Sort((left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));
            if (SameSelection()) return;
            appliedSelection = selection.ToArray();
        }

        private void AddHierarchy(Transform root)
        {
            selection.Add(root.gameObject);
            for (var i = 0; i < root.childCount; i++) AddHierarchy(root.GetChild(i));
        }

        private bool SameSelection()
        {
            if (appliedSelection.Length != selection.Count) return false;
            for (var i = 0; i < appliedSelection.Length; i++)
                if (appliedSelection[i] != selection[i]) return false;
            return true;
        }

    }
}
