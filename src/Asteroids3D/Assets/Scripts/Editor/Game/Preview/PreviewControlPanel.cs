using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Opens on a tuning target, offers that target's Replay and Playtest entries, and draws only its
    /// knobs. Replay drives a subject's timeline; Playtest runs the real game and can restart it. Both
    /// edit the feel assets the game ships, in place under the preview's snapshot. Everything either
    /// shows renders in the Game View.
    /// </summary>
    internal sealed class PreviewControlPanel : EditorWindow
    {
        private readonly Dictionary<ScriptableObject, SerializedObject> knobs = new();
        private PreviewHost host;
        [SerializeField] private int targetIndex;
        [SerializeField] private PreviewEntry selectedEntry;
        private Vector2 scroll;

        private TuningTarget Target => TuningTarget.All[targetIndex];

        [MenuItem("Window/Preview Host")]
        public static void Open() => GetWindow<PreviewControlPanel>("Preview Host");

        private void Update()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                DrawEntry();
                return;
            }
            if (!host) host = FindFirstObjectByType<PreviewHost>();
            if (!host)
            {
                EditorGUILayout.HelpBox("Play mode without a preview host. Stop, then enter the preview from here.",
                    MessageType.Info);
                return;
            }
            if (host.Fault != null)
            {
                EditorGUILayout.HelpBox($"The preview faulted: {host.Fault.Message}\nThe console has the stack. " +
                                        "Exit play mode to recover.", MessageType.Error);
                return;
            }
            if (!Target.Offers(host.Entry))
                targetIndex = Array.FindIndex(TuningTarget.All, target => target.Offers(host.Entry));
            selectedEntry = host.Entry;
            // Not ready, not just replacing: a Replay-to-Replay switch refuses while a compose is in flight.
            using (new EditorGUI.DisabledScope(!host.IsReady))
            {
                var game = host.gameHostPrefab;
                var names = TuningTarget.All.Select(target => target.Name +
                    (target.Profiles(game).Any(FeelSnapshot.instance.HasUnsavedEdits) ? " · unsaved" : "")).ToArray();
                var picked = EditorGUILayout.Popup("Target", targetIndex, names);
                if (picked != targetIndex)
                {
                    targetIndex = picked;
                    var next = Target.EntryAfterSwitch(host.Entry);
                    if (next != host.Entry) host.Select(next);
                    return;
                }
                var chosen = DrawSections(host.Entry);
                if (chosen != host.Entry)
                {
                    host.Select(chosen);
                    return;
                }
            }
            if (!host.IsReady)
            {
                EditorGUILayout.LabelField(host.IsReplacing ? "Replacing the preview…" : "Loading the preview…");
                return;
            }
            if (PreviewHost.IsPlaytest(host.Entry))
            {
                if (GUILayout.Button("Restart Playtest"))
                {
                    host.RestartPlaytest();
                    return;
                }
                if (host.Entry == PreviewEntry.CrowdedJumpGate)
                    EditorGUILayout.HelpBox("Press F inside the jump gate. Continue completes arrival. " +
                        "Feel changes apply to the next jump; Restart Playtest tries again.", MessageType.None);
            }
            else
                DrawTransport(host.Subject, host.Transport);
            Target.DrawControls(host);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var profile in Target.Profiles(host.gameHostPrefab)) DrawKnobs(profile);
            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry()
        {
            EditorGUILayout.HelpBox("Pick what you're tuning, then how to preview it. Replay shows a moment with " +
                                    "timeline controls; Playtest starts the real game with separate preview run records.",
                MessageType.None);
            targetIndex = EditorGUILayout.Popup("Target", targetIndex, TuningTarget.All.Select(target => target.Name).ToArray());
            selectedEntry = DrawSections(Target.EntryAfterSwitch(selectedEntry));
            if (GUILayout.Button("Enter preview"))
                PreviewHostBootstrap.EnterPlayMode(selectedEntry);
        }

        private PreviewEntry DrawSections(PreviewEntry current)
        {
            var chosen = current;
            foreach (var playtest in new[] { false, true })
            {
                var entries = Target.Entries.Where(entry => PreviewHost.IsPlaytest(entry) == playtest).ToArray();
                if (entries.Length == 0) continue;
                EditorGUILayout.LabelField(playtest ? "Playtest" : "Replay", EditorStyles.boldLabel);
                foreach (var entry in entries)
                    if (GUILayout.Toggle(entry == current, ObjectNames.NicifyVariableName(entry.ToString()),
                            EditorStyles.radioButton) && entry != current)
                        chosen = entry;
            }
            return chosen;
        }

        private static void DrawTransport(IPreviewSubject subject, PreviewTransport transport)
        {
            EditorGUILayout.LabelField("Subject", subject.Name);
            var fixture = EditorGUILayout.Popup("Fixture", subject.Fixture, subject.Fixtures.ToArray());
            if (fixture != subject.Fixture)
            {
                subject.SelectFixture(fixture);
                transport.Restart();
            }

            var names = new string[subject.SpanCount];
            for (var i = 0; i < names.Length; i++) names[i] = subject.Span(i).Name;
            var span = EditorGUILayout.Popup("Phase", Mathf.Min(transport.Span, names.Length - 1), names);
            if (span != transport.Span) transport.Scrub(span, 0f);

            var length = transport.Length(subject.Span(span));
            var seconds = EditorGUILayout.Slider("Time", transport.Seconds, 0f, Mathf.Max(length, .001f));
            if (!Mathf.Approximately(seconds, transport.Seconds)) transport.Scrub(span, seconds);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(transport.Playing ? "Pause" : "Play")) transport.Playing = !transport.Playing;
                if (GUILayout.Button("Restart")) transport.Restart();
                transport.Loop = GUILayout.Toggle(transport.Loop, "Loop");
            }
            transport.Speed = EditorGUILayout.FloatField("Speed", transport.Speed);
            transport.HoldSeconds = EditorGUILayout.FloatField("Hold seconds", transport.HoldSeconds);
        }

        private void DrawKnobs(ScriptableObject profile)
        {
            if (!knobs.TryGetValue(profile, out var serialized)) knobs.Add(profile, serialized = new SerializedObject(profile));
            var snapshot = FeelSnapshot.instance;
            var unsaved = snapshot.HasUnsavedEdits(profile);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Feel · " + profile.name + (unsaved ? " · unsaved edits" : ""), EditorStyles.boldLabel);
            serialized.Update();
            var property = serialized.GetIterator();
            for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
                if (property.name != "m_Script") EditorGUILayout.PropertyField(property, true);
            serialized.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(!unsaved))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save feel profile")) snapshot.Save(profile);
                if (GUILayout.Button("Discard")) snapshot.Discard(profile);
            }
        }
    }
}
