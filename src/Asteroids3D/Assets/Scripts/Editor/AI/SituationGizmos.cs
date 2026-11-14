using System;
using AI.Tactics;
using Diagnostics;
using Substrate;
using UnityEditor;
using UnityEngine;

namespace AI
{
    [InitializeOnLoad]
    internal static class SituationGizmos
    {
        private const string SettingsPath = "Assets/Settings/AI/Tactics/AggressorTemper.asset";

        static SituationGizmos() =>
            GizmoView.Register(typeof(AICommander), "situation", "Situation",
                "firing lane to the target (red, with a count, when allies are in it); exposed (red) or clear " +
                "(green) ring; dotted line to the target's open side; the tactic it flies", "Combat");

        private const float MarkRadius = 2.5f;
        private const float OpenSideRadius = 1.2f;
        private const float DottedScreenSize = 4f;

        private static readonly Color LaneClear = new(1f, 1f, 1f, 0.45f);
        private static readonly Color LaneBlocked = new(1f, 0.25f, 0.2f, 0.9f);
        private static readonly Color Exposed = new(1f, 0.2f, 0.15f);
        private static readonly Color Clear = new(0.3f, 1f, 0.35f);
        private static readonly Color OpenSide = new(0.2f, 0.85f, 1f);
        private static readonly Color TacticLabel = new(1f, 0.9f, 0.4f);
        private static readonly Vector3 PlaneNormal = GamePlane.Rotation * Vector3.forward;

        private static TacticsSettings settings;

        private static TacticsSettings Settings
        {
            get
            {
                if (settings) return settings;
                settings = AssetDatabase.LoadAssetAtPath<TacticsSettings>(SettingsPath);
                if (!settings) throw new InvalidOperationException($"The tactics settings '{SettingsPath}' are missing.");
                return settings;
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(AICommander))]
        private static void Draw(AICommander commander, GizmoType gizmoType)
        {
            if (!GizmoView.IsOn(typeof(AICommander), "situation") || !GizmoView.InScope(commander)) return;
            if (!Application.isPlaying || commander.context?.Combat == null) return;

            var tuned = Settings;
            var situation = Situation.Read(commander.context, tuned);
            if (!situation.hasTarget) return;

            var me = commander.context.Self.Kinematics.pos;
            var target = commander.context.Combat.EnemyPos;
            DrawLane(me, target, tuned.laneWidth, situation.alliesInLane);
            Ring(me, MarkRadius, situation.exposed ? Exposed : Clear, 3f);
            if (commander.Brain && commander.Brain.CurrentTactic is { } tactic)
                Handles.Label(GamePlane.PlanePointToWorld(me + Vector2.up * (MarkRadius + 1f)),
                    ObjectNames.NicifyVariableName(tactic.ToString()),
                    new GUIStyle { normal = { textColor = TacticLabel }, fontSize = 12 });
            if (situation.openSide == Vector2.zero) return;

            var flank = target + situation.openSide * situation.distance;
            Handles.color = OpenSide;
            Handles.DrawDottedLine(GamePlane.PlanePointToWorld(me), GamePlane.PlanePointToWorld(flank), DottedScreenSize);
            Ring(flank, OpenSideRadius, OpenSide, 2f);
        }

        private static void DrawLane(Vector2 me, Vector2 target, float width, int allies)
        {
            var color = allies > 0 ? LaneBlocked : LaneClear;
            var edge = Vector2.Perpendicular((target - me).normalized) * (width * 0.5f);
            Handles.color = color;
            Handles.DrawLine(GamePlane.PlanePointToWorld(me), GamePlane.PlanePointToWorld(target), 2f);
            Handles.DrawLine(GamePlane.PlanePointToWorld(me + edge), GamePlane.PlanePointToWorld(target + edge));
            Handles.DrawLine(GamePlane.PlanePointToWorld(me - edge), GamePlane.PlanePointToWorld(target - edge));
            if (allies > 0)
                Handles.Label(GamePlane.PlanePointToWorld((me + target) * 0.5f), allies.ToString(),
                    new GUIStyle { normal = { textColor = color }, fontSize = 12 });
        }

        private static void Ring(Vector2 center, float radius, Color color, float thickness)
        {
            Handles.color = color;
            Handles.DrawWireDisc(GamePlane.PlanePointToWorld(center), PlaneNormal, radius, thickness);
        }
    }
}
