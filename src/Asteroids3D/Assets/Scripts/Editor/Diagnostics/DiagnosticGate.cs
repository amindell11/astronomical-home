using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>EditorPrefs-backed gate for the live scene-view painter shims: which painter names draw, and whether unselected ships draw too. Keys carry the project path so pooled worktrees keep independent state.</summary>
    public static class DiagnosticGate
    {
        private static readonly string ActiveKey = Key("active");
        private static readonly string DrawUnselectedKey = Key("draw-unselected");

        private static readonly HashSet<string> active = new(Load());

        public static bool DrawUnselected
        {
            get => EditorPrefs.GetBool(DrawUnselectedKey, false);
            set => EditorPrefs.SetBool(DrawUnselectedKey, value);
        }

        public static int ActiveCount => active.Count;

        public static bool IsActive(string name) => active.Contains(name);

        public static bool ShouldDraw(string name, GizmoType gizmoType) =>
            IsActive(name) && ((gizmoType & GizmoType.Selected) != 0 || DrawUnselected);

        public static void Toggle(string name)
        {
            if (!active.Remove(name)) active.Add(name);
            Save();
        }

        public static void Replace(IEnumerable<string> names)
        {
            active.Clear();
            active.UnionWith(names);
            Save();
        }

        public static void Clear()
        {
            active.Clear();
            Save();
        }

        private static string[] Load()
        {
            var stored = EditorPrefs.GetString(ActiveKey, "");
            return stored.Length == 0 ? Array.Empty<string>() : stored.Split(',');
        }

        private static void Save() => EditorPrefs.SetString(ActiveKey, string.Join(",", active));

        private static string Key(string suffix) => $"DiagnosticGate:{Application.dataPath}:{suffix}";
    }
}
