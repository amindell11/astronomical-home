using System.Linq;
using UI.Recap;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Enters and switches the preview host's modes. Inspection drives a subject's timeline;
    /// Playtest runs the real game and can restart it. Both edit feel assets with an explicit save.
    /// Everything shown by either mode renders in the Game View.
    /// </summary>
    internal sealed class PreviewControlPanel : EditorWindow
    {
        private PreviewHost host;
        private SerializedObject knobs;
        [SerializeField] private PreviewSubjectKind selectedSubject;
        [SerializeField] private PreviewMode selectedMode;
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
            var profile = (ScriptableObject)EditorGUILayout.ObjectField("Feel profile", host.Subject.Knobs,
                host.Subject.Knobs.GetType(), false);
            if (profile && profile != host.Subject.Knobs) host.Subject.Knobs = profile;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawKnobs(host.Subject.Knobs);
            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry()
        {
            EditorGUILayout.HelpBox("Inspection shows a presentation with timeline controls. " +
                                    "Playtest starts the real game with separate preview run records.", MessageType.None);
            selectedMode = (PreviewMode)EditorGUILayout.EnumPopup("Mode", selectedMode);
            if (selectedMode == PreviewMode.Inspection)
                selectedSubject = (PreviewSubjectKind)EditorGUILayout.EnumPopup("Subject", selectedSubject);
            if (GUILayout.Button("Enter preview")) PreviewHostBootstrap.EnterPlayMode(selectedSubject, selectedMode);
        }

        private void DrawPlaytest()
        {
            if (GUILayout.Button("Restart Playtest"))
            {
                host.RestartPlaytest();
                return;
            }
            var profile = (RecapFeelProfile)EditorGUILayout.ObjectField("Recap feel profile", host.RecapFeel,
                typeof(RecapFeelProfile), false);
            if (profile && profile != host.RecapFeel)
            {
                host.RestartPlaytest(profile);
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawKnobs(host.RecapFeel);
            EditorGUILayout.EndScrollView();
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
            if (subject.Busy) EditorGUILayout.LabelField("Staged moment playing…");
        }

        private void DrawKnobs(ScriptableObject target)
        {
            if (!target) return;
            if (knobs == null || knobs.targetObject != target) knobs = new SerializedObject(target);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Feel · " + target.name, EditorStyles.boldLabel);
            knobs.Update();
            var property = knobs.GetIterator();
            for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
                if (property.name != "m_Script") EditorGUILayout.PropertyField(property, true);
            knobs.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(!EditorUtility.IsDirty(target)))
                if (GUILayout.Button("Save feel profile")) AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}
