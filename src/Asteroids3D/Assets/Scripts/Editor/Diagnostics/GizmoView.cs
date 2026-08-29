using System;
using System.Collections.Generic;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Game.Diagnostics
{
    internal enum GizmoScope { All, Selected, Team }

    /// <summary>
    /// Editor-only registry and read seam for the Gizmo View window. Drawers self-register their
    /// subviews (one row each) from an <c>[InitializeOnLoad]</c> static ctor, then at draw time gate
    /// on <see cref="IsOn"/> + <see cref="InScope"/> instead of <c>GizmoType.Selected</c>. Per-subview
    /// on/off flags and the global scope live in EditorPrefs under the "GizmoView." namespace, so they
    /// stay per-dev and out of git. The window is the only writer; drawers only read. One exception is
    /// unscoped: the global Colliders toggle proxies Unity's built-in collider gizmos via GizmoUtility.
    /// </summary>
    [InitializeOnLoad]
    internal static class GizmoView
    {
        internal readonly struct Subview
        {
            public Subview(Type componentType, string key, string displayName, string appearance, string category, bool defaultOn)
            {
                ComponentType = componentType;
                Key = key;
                DisplayName = displayName;
                Appearance = appearance;
                Category = category;
                DefaultOn = defaultOn;
            }

            public Type ComponentType { get; }
            public string Key { get; }
            public string DisplayName { get; }
            public string Appearance { get; }
            public string Category { get; }
            public bool DefaultOn { get; }
        }

        private const string ScopePref = "GizmoView.Scope";
        private const string ScopeTeamPref = "GizmoView.ScopeTeam";
        private const string CollidersPref = "GizmoView.Colliders";

        // Built-in collider gizmos, driven globally as the documented scope exception (Unity can't
        // team/selection-scope them). Reflected onto Unity's real display state via GizmoUtility.
        private static readonly Type[] ColliderTypes =
        {
            typeof(BoxCollider), typeof(SphereCollider), typeof(CapsuleCollider), typeof(MeshCollider),
        };

        private static readonly List<Subview> Registered = new();

        internal static IReadOnlyList<Subview> Subviews => Registered;

        // The Colliders pref outlives domain reloads but Unity's gizmo state does not, so re-apply on
        // load. delayCall defers past the AnnotationManager warmth window the native-gizmo arc hit.
        static GizmoView() => EditorApplication.delayCall += () => ApplyColliders(CollidersOn);

        // Drawers call this from their [InitializeOnLoad] static ctor; idempotent across domain reloads.
        internal static void Register(Type componentType, string key, string displayName, string appearance,
            string category, bool defaultOn = false)
        {
            foreach (var existing in Registered)
                if (existing.ComponentType == componentType && existing.Key == key) return;
            Registered.Add(new Subview(componentType, key, displayName, appearance, category, defaultOn));
        }

        internal static bool IsOn(Type componentType, string key) =>
            EditorPrefs.GetBool(PrefKey(componentType, key), DefaultOn(componentType, key));

        internal static void SetOn(Type componentType, string key, bool on) => EditorPrefs.SetBool(PrefKey(componentType, key), on);

        internal static GizmoScope Scope
        {
            get => (GizmoScope)EditorPrefs.GetInt(ScopePref, (int)GizmoScope.All);
            set => EditorPrefs.SetInt(ScopePref, (int)value);
        }

        internal static int ScopeTeam
        {
            get => EditorPrefs.GetInt(ScopeTeamPref, 0);
            set => EditorPrefs.SetInt(ScopeTeamPref, value);
        }

        // Draw-time scope predicate. Resolves the owning ship (the human player ship always reads
        // team 0, so "Team 0" scopes player + team-0 AI together). Components with no owning ship pass
        // only under All; Selected still honours a direct selection; Team excludes them.
        internal static bool InScope(Component component)
        {
            switch (Scope)
            {
                case GizmoScope.All:
                    return true;
                case GizmoScope.Selected:
                    var selectedShip = component.GetComponentInParent<Ship>();
                    return Selection.Contains(component.gameObject) || (selectedShip && Selection.Contains(selectedShip.gameObject));
                case GizmoScope.Team:
                    var teamShip = component.GetComponentInParent<Ship>();
                    return teamShip && teamShip.teamNumber == ScopeTeam;
                default:
                    return false;
            }
        }

        // Ships the current scope selects — backs the window's "N objects in scope" readout.
        internal static int ScopeCount()
        {
            var ships = UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None);
            if (Scope == GizmoScope.All) return ships.Length;

            var count = 0;
            foreach (var ship in ships)
                if (InScope(ship)) count++;
            return count;
        }

        // Global Colliders toggle: window-level, no owning drawer. Setter persists the pref and drives
        // Unity's native collider gizmo display so the window and the Editor agree.
        internal static bool CollidersOn
        {
            get => EditorPrefs.GetBool(CollidersPref, false);
            set
            {
                EditorPrefs.SetBool(CollidersPref, value);
                ApplyColliders(value);
            }
        }

        private static void ApplyColliders(bool on)
        {
            foreach (var type in ColliderTypes)
                if (GizmoUtility.TryGetGizmoInfo(type, out _))
                    GizmoUtility.SetGizmoEnabled(type, on, false);
        }

        private static bool DefaultOn(Type componentType, string key)
        {
            foreach (var subview in Registered)
                if (subview.ComponentType == componentType && subview.Key == key) return subview.DefaultOn;
            return false;
        }

        private static string PrefKey(Type componentType, string key) => $"GizmoView.{componentType.Name}.{key}";
    }
}
