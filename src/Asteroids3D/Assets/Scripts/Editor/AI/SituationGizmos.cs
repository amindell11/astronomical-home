using System;
using System.Globalization;
using AI.Pilots;
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
                "that ship's firing lane to the target (red, with a count, when allies are in it); exposed (red) or " +
                "clear (green) ring; dotted line to the target's open side; the temper and tactic it flies " +
                "(orange while wounded), and whether it is waiting, retreating, cautious, or committed", "Combat");

        private const float MarkRadius = 2.5f;
        private const float OpenSideRadius = 1.2f;
        private const float DottedScreenSize = 4f;

        private static readonly Color LaneClear = new(1f, 1f, 1f, 0.45f);
        private static readonly Color LaneBlocked = new(1f, 0.25f, 0.2f, 0.9f);
        private static readonly Color Exposed = new(1f, 0.2f, 0.15f);
        private static readonly Color Clear = new(0.3f, 1f, 0.35f);
        private static readonly Color OpenSide = new(0.2f, 0.85f, 1f);
        private static readonly Color TacticLabel = new(1f, 0.9f, 0.4f);
        private static readonly Color WoundedLabel = new(1f, 0.55f, 0.08f);
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

            var archetype = commander.Brain as ArchetypeBrain;
            var temper = archetype && archetype.AuthoredTactics ? archetype.AuthoredTactics : null;
            var tuned = temper ? temper : Settings;
            var situation = Situation.Read(commander.context, tuned);
            if (!situation.hasTarget) return;

            var me = commander.context.Self.Kinematics.pos;
            var target = commander.context.Combat.EnemyPos;
            DrawLane(me, target, tuned.laneWidth, situation.alliesInLane);
            Ring(me, MarkRadius, situation.exposed ? Exposed : Clear, 3f);
            if (commander.Brain && commander.Brain.CurrentTactic is { } tactic)
            {
                var wounded = temper && archetype.Wounded;
                Handles.Label(GamePlane.PlanePointToWorld(me + Vector2.up * (MarkRadius + 1f)),
                    Label(temper ? temper.name : null, tactic, temper, wounded,
                        temper ? archetype.RespondingSeconds : null,
                        temper ? archetype.RetreatsTaken : 0),
                    new GUIStyle { normal = { textColor = wounded ? WoundedLabel : TacticLabel }, fontSize = 12 });
            }
            if (situation.openSide == Vector2.zero) return;

            var flank = target + situation.openSide * situation.distance;
            Handles.color = OpenSide;
            Handles.DrawDottedLine(GamePlane.PlanePointToWorld(me), GamePlane.PlanePointToWorld(flank), DottedScreenSize);
            Ring(flank, OpenSideRadius, OpenSide, 2f);
        }

        internal static string Label(string temperName, Tactic tactic, TacticsSettings temper, bool wounded,
            float? respondingSeconds, int retreatsTaken)
        {
            var tacticName = ObjectNames.NicifyVariableName(tactic.ToString());
            var shown = ShownTemper(temperName);
            var head = shown == null ? tacticName : $"{shown} · {tacticName}";
            if (!temper) return head;

            var second = SecondLine(temper, wounded, respondingSeconds, retreatsTaken);
            return second == null ? head : head + "\n" + second;
        }

        private static string ShownTemper(string temperName)
        {
            if (string.IsNullOrEmpty(temperName)) return null;
            const string suffix = "Temper";
            if (!temperName.EndsWith(suffix, StringComparison.Ordinal)) return temperName;
            var stem = temperName.Substring(0, temperName.Length - suffix.Length);
            return stem.Length == 0 ? temperName : stem;
        }

        private static string SecondLine(TacticsSettings temper, bool wounded, float? respondingSeconds,
            int retreatsTaken)
        {
            if (!wounded)
                return temper.commits && retreatsTaken >= temper.retreats ? "committed" : null;

            var line = WoundedLine(temper.wounded, respondingSeconds);
            if (line == null || !temper.commits) return line;
            return $"{line} · {temper.retreats - retreatsTaken} of {temper.retreats} left";
        }

        private static string WoundedLine(WoundedResponse response, float? respondingSeconds)
        {
            if (response == WoundedResponse.TurnCautious)
                return $"wounded · cautious {OneDecimal(respondingSeconds ?? 0f)} s";
            if (response != WoundedResponse.BreakOff && response != WoundedResponse.FallBack) return null;
            if (!respondingSeconds.HasValue) return "wounded · waiting to retreat";
            return $"wounded · retreating {OneDecimal(respondingSeconds.Value)} s";
        }

        private static string OneDecimal(float seconds) => seconds.ToString("0.0", CultureInfo.InvariantCulture);

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
