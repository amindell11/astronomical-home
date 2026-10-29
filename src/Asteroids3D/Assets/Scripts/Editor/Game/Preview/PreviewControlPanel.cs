using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Enters and switches the preview host's modes. Inspection drives a subject's timeline;
    /// Playtest runs the real game and can restart it. Both edit working copies of the feel assets,
    /// which change only on Save. Everything shown by either mode renders in the Game View.
    /// </summary>
    internal sealed class PreviewControlPanel : EditorWindow
    {
        private PreviewHost host;
        private SerializedObject knobs;
        private SerializedObject jumpKnobs;
        [SerializeField] private PreviewSubjectKind selectedSubject;
        [SerializeField] private PreviewMode selectedMode;
        [SerializeField] private PreviewPlaytestSetup selectedSetup;
        private Vector2 scroll;

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
            using (new EditorGUI.DisabledScope(host.IsReplacing))
            {
                var modeChoice = (PreviewMode)EditorGUILayout.EnumPopup("Mode", host.Mode);
                selectedMode = modeChoice;
                if (modeChoice != host.Mode)
                {
                    host.SelectMode(modeChoice);
                    return;
                }
            }
            if (!host.IsReady)
            {
                EditorGUILayout.LabelField(host.IsReplacing ? "Replacing the preview…" : "Loading the preview…");
                return;
            }
            if (host.Mode == PreviewMode.Playtest)
            {
                DrawPlaytest();
                return;
            }
            var selected = (PreviewSubjectKind)EditorGUILayout.EnumPopup("Subject", host.SubjectKind);
            selectedSubject = selected;
            if (selected != host.SubjectKind)
            {
                host.SelectSubject(selected);
                return;
            }
            DrawTransport(host.Subject, host.Transport);
            var profile = PickProfile("Feel profile", host.Subject.Knobs);
            if (profile)
            {
                host.Subject.Knobs = profile;
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawKnobs(host.Subject.Knobs, ref knobs);
            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry()
        {
            EditorGUILayout.HelpBox("Inspection shows a presentation with timeline controls. " +
                                    "Playtest starts the real game with separate preview run records.", MessageType.None);
            selectedMode = (PreviewMode)EditorGUILayout.EnumPopup("Mode", selectedMode);
            if (selectedMode == PreviewMode.Inspection)
                selectedSubject = (PreviewSubjectKind)EditorGUILayout.EnumPopup("Subject", selectedSubject);
            else
                selectedSetup = (PreviewPlaytestSetup)EditorGUILayout.EnumPopup("Setup", selectedSetup);
            if (GUILayout.Button("Enter preview"))
                PreviewHostBootstrap.EnterPlayMode(selectedSubject, selectedMode, selectedSetup);
        }

        private void DrawPlaytest()
        {
            var setup = (PreviewPlaytestSetup)EditorGUILayout.EnumPopup("Setup", host.PlaytestSetup);
            selectedSetup = setup;
            if (setup != host.PlaytestSetup)
            {
                host.SelectPlaytestSetup(setup);
                return;
            }
            if (GUILayout.Button("Restart Playtest"))
            {
                host.RestartPlaytest();
                return;
            }
            var recap = PickProfile("Recap feel profile", host.RecapFeel);
            if (recap)
            {
                host.RestartPlaytest(recap);
                return;
            }
            if (host.PlaytestSetup == PreviewPlaytestSetup.CrowdedJumpGate)
                EditorGUILayout.HelpBox("Press F inside the jump gate. Continue completes arrival. " +
                    "Feel changes apply to the next jump; Restart Playtest tries again.", MessageType.None);
            var jump = PickProfile("Jump feel profile", host.JumpFeel);
            if (jump)
            {
                host.RestartPlaytest(jump);
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawKnobs(host.JumpFeel, ref jumpKnobs);
            DrawKnobs(host.RecapFeel, ref knobs);
            EditorGUILayout.EndScrollView();
        }

        private static T PickProfile<T>(string label, T copy) where T : ScriptableObject
        {
            var copies = FeelWorkingCopies.Current;
            var asset = copies.AssetOf(copy);
            var picked = (T)EditorGUILayout.ObjectField(label, asset, asset.GetType(), false);
            if (!picked || picked == asset) return null;
            copies.Resolve(copy, FeelWorkingCopies.AskToSave);
            return copies.For(picked);
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

        private static void DrawKnobs(ScriptableObject copy, ref SerializedObject serialized)
        {
            if (serialized == null || serialized.targetObject != copy) serialized = new SerializedObject(copy);
            var copies = FeelWorkingCopies.Current;
            var unsaved = copies.HasUnsavedEdits(copy);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Feel · " + copy.name + (unsaved ? " · unsaved edits" : ""), EditorStyles.boldLabel);
            serialized.Update();
            var property = serialized.GetIterator();
            for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
                if (property.name != "m_Script") EditorGUILayout.PropertyField(property, true);
            serialized.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(!unsaved))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save feel profile")) copies.Save(copy);
                if (GUILayout.Button("Revert")) copies.Revert(copy);
            }
        }
    }
}
