using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>
    /// Scene-global gizmo control: a matrix of registered subview toggles grouped under capture-category
    /// headers, plus the global scope dropdown. The window is the sole writer of <see cref="GizmoView"/>
    /// state; drawers read it. Every edit repaints the scene views so toggles land immediately.
    /// </summary>
    internal sealed class GizmoViewWindow : EditorWindow
    {
        private Vector2 scroll;
        private readonly Dictionary<string, bool> categoryOpen = new();

        [MenuItem("Window/Gizmo View")]
        private static void Open() => GetWindow<GizmoViewWindow>("Gizmo View");

        private void OnGUI()
        {
            DrawScopeControls();
            EditorGUILayout.Space();
            DrawEnvironmentControls();
            EditorGUILayout.Space();
            DrawMatrix();
        }

        // Colliders is global (the documented scope exception) and drives Unity's native collider
        // gizmos, so it sits apart from the scoped, registered-subview matrix.
        private static void DrawEnvironmentControls()
        {
            EditorGUILayout.LabelField("Environment", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var on = EditorGUILayout.ToggleLeft(
                new GUIContent("    Colliders", "native Box/Sphere/Capsule/Mesh collider gizmos (global)"),
                GizmoView.CollidersOn);
            if (!EditorGUI.EndChangeCheck()) return;
            GizmoView.CollidersOn = on;
            SceneView.RepaintAll();
        }

        private void DrawScopeControls()
        {
            EditorGUI.BeginChangeCheck();
            GizmoView.Scope = (GizmoScope)EditorGUILayout.EnumPopup("Scope", GizmoView.Scope);
            if (GizmoView.Scope == GizmoScope.Team)
                GizmoView.ScopeTeam = EditorGUILayout.IntField("Team", GizmoView.ScopeTeam);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

            EditorGUILayout.LabelField("Objects in scope", GizmoView.ScopeCount().ToString());

            if (GUILayout.Button("All Off"))
                SetAll(GizmoView.Subviews, false);
        }

        private void DrawMatrix()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var group in GizmoView.Subviews.GroupBy(s => s.Category))
            {
                var open = !categoryOpen.TryGetValue(group.Key, out var v) || v;
                open = EditorGUILayout.BeginFoldoutHeaderGroup(open, group.Key);
                categoryOpen[group.Key] = open;
                if (open)
                {
                    DrawCategoryMasters(group);
                    foreach (var subview in group) DrawRow(subview);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawCategoryMasters(IEnumerable<GizmoView.Subview> group)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(14f);
                if (GUILayout.Button("All On", GUILayout.Width(60f))) SetAll(group, true);
                if (GUILayout.Button("All Off", GUILayout.Width(60f))) SetAll(group, false);
            }
        }

        private static void DrawRow(GizmoView.Subview subview)
        {
            EditorGUI.BeginChangeCheck();
            var on = EditorGUILayout.ToggleLeft(new GUIContent("    " + subview.DisplayName, subview.Appearance),
                GizmoView.IsOn(subview.ComponentType, subview.Key));
            if (!EditorGUI.EndChangeCheck()) return;
            GizmoView.SetOn(subview.ComponentType, subview.Key, on);
            SceneView.RepaintAll();
        }

        private static void SetAll(IEnumerable<GizmoView.Subview> subviews, bool on)
        {
            foreach (var subview in subviews) GizmoView.SetOn(subview.ComponentType, subview.Key, on);
            SceneView.RepaintAll();
        }
    }
}
