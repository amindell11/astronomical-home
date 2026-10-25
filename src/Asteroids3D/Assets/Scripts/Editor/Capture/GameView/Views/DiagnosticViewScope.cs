using System;
using System.Collections.Generic;
using Capture.GameView.Recovery;
using Capture.Views;
using Diagnostics;
using Ships;
using Substrate.Services.Projectiles;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capture.GameView.Views
{
    internal sealed class DiagnosticViewScope : IDisposable
    {
        private readonly GameObject root;
        private readonly DiagnosticViewTransaction transaction;
        private readonly Action released;
        private bool disposed;

        public DiagnosticViewScope(DiagnosticViewConfig config, GizmoCaptureProfile profile,
            IReadOnlyList<Ship> subjects, IProjectileService projectiles, Action released)
        {
            this.released = released;
            root = new GameObject("[Capture] Diagnostic view");
            try
            {
                var view = root.AddComponent<DiagnosticView>();
                transaction = new DiagnosticViewTransaction(GizmoCaptureProfiles.Resolve(profile), Array.Empty<Object>(),
                    null, MapScope(config.gizmoScope), config.gizmoScopeTeam);
                view.Initialize(config, subjects, projectiles, transaction.SetSelection);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static GizmoScope MapScope(CaptureGizmoScope scope) => scope switch
        {
            CaptureGizmoScope.All => GizmoScope.All,
            CaptureGizmoScope.Selected => GizmoScope.Selected,
            CaptureGizmoScope.Team => GizmoScope.Team,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unmapped diagnostic gizmo scope."),
        };

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var failures = new List<Exception>();
            CaptureRecoveryJournal.Attempt(() => Object.DestroyImmediate(root), failures);
            CaptureRecoveryJournal.Attempt(() => transaction?.Restore(), failures);
            released();
            if (failures.Count > 0) throw new AggregateException("Diagnostic view cleanup failed.", failures);
        }
    }
}
