#if UNITY_EDITOR
using System;
using Capture.GameView;
using Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rendering
{
    [Category("Camera"), Category("RequiresGraphics")]
    public sealed class PlainCaptureViewEditModeTests
    {
        [Test]
        public void NoGizmoProfile_DisablesGameViewGuidesAndRestoresPriorState()
        {
            var view = new UnityGameViewAdapter();
            var prior = view.Snapshot();
            var priorColliders = GizmoView.CollidersOn;
            var subject = new GameObject("Plain capture subject");
            var transaction = new GameViewCaptureTransaction(Array.Empty<Type>(),
                new UnityEngine.Object[] { subject }, subject, 960, 540, GizmoScope.All, 0);
            try
            {
                Assert.That(view.Snapshot().drawGizmos, Is.False, "Plain footage must suppress selected collider guides.");
                Assert.That(GizmoView.CollidersOn, Is.False);
            }
            finally
            {
                transaction.Restore();
                UnityEngine.Object.DestroyImmediate(subject);
            }
            Assert.That(view.Snapshot().drawGizmos, Is.EqualTo(prior.drawGizmos));
            Assert.That(GizmoView.CollidersOn, Is.EqualTo(priorColliders));
        }
    }
}
#endif
