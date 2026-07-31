using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>The drawing-surface contract a painter renders onto, in GamePlane plane-space. Two backends implement it: offscreen-capture <c>CaptureDraw</c> (clips) and editor <c>GizmoCanvas</c> (live scene view). Editor Gizmos/Handles never appear in offscreen captures, so writing each diagnostic once against this contract is the only way one source feeds both.</summary>
    public interface IDiagnosticCanvas
    {
        float LineWidth { get; set; }
        void Line(Vector2 a, Vector2 b, Color color);
        void Vector(Vector2 origin, Vector2 v, Color color);
        void Ring(Vector2 center, float radius, Color color);
        void Trail(Vector2 head, Vector2 dir, float length, Color color);
        void Label(Vector2 pos, string text, Color color, float size = 4f);
    }

    /// <summary>A named diagnostic view bound to its subjects at construction; a backend paints the active set each captured frame.</summary>
    public interface IDiagnosticPainter
    {
        string Name { get; }
        void Paint(IDiagnosticCanvas canvas);
    }
}
