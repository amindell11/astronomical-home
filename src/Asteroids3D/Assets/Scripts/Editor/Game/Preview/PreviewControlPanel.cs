using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// The preview host's control panel, and nothing more: it enters the preview from edit mode, then
    /// drives the live host's transport (fixture, phase, scrub, play, pause, loop, speed), and edits the
    /// subject's feel profile in place with an explicit save. Everything it shows renders in the Game View.
    /// </summary>
    internal sealed class PreviewControlPanel : EditorWindow
    {
        private PreviewHost host;
        private SerializedObject knobs;
        [SerializeField] private PreviewSubjectKind selectedSubject;
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
            if (host.Subject == null)
            {
                EditorGUILayout.LabelField("Composing the session…");
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
            EditorGUILayout.HelpBox("Opens an empty scene, builds the preview host over the game's first sector " +
                                    "with the real player rig, camera and HUD, and enters play mode.", MessageType.None);
            selectedSubject = (PreviewSubjectKind)EditorGUILayout.EnumPopup("Subject", selectedSubject);
            if (GUILayout.Button("Enter preview")) PreviewHostBootstrap.EnterPlayMode(selectedSubject);
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
