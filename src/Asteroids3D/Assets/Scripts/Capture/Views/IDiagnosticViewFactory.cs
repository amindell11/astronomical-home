using System;
using System.Collections.Generic;
using Ships;
using Substrate.Services.Projectiles;

namespace Capture.Views
{
    public interface IDiagnosticViewFactory
    {
        IDisposable PrepareView(DiagnosticViewConfig config, GizmoCaptureProfile profile,
            IReadOnlyList<Ship> subjects, IProjectileService projectiles);
    }
}
