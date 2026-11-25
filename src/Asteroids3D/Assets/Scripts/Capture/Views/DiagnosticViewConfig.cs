namespace Capture.Views
{
    public enum CaptureGizmoScope
    {
        All,
        Selected,
        Team,
    }

    public sealed class DiagnosticViewConfig
    {
        public float minHalfHeight = 22f;
        public float padding = 12f;
        public CaptureGizmoScope gizmoScope = CaptureGizmoScope.All;
        public int gizmoScopeTeam;
    }
}
