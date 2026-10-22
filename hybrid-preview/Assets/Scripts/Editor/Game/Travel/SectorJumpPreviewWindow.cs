using UnityEditor;
using UnityEngine;

namespace Game.Travel
{
    public sealed class SectorJumpPreviewWindow : EditorWindow
    {
        private const string Root = "Assets/Visuals/Travel/";
        private SectorJumpProfile profile;
        private UnityEditor.Editor profileEditor;
        private Material material;
        private RenderTexture target;
        private SectorJumpSkyPreview outgoingSky;
        private SectorJumpSkyPreview incomingSky;
        private Vector2 scroll;
        private float time;
        private float heading = -135f;
        private bool playing;
        private bool loop = true;
        private double previousTime;

        [MenuItem("Tools/Presentation/Sector Jump Preview")]
        public static void Open() => GetWindow<SectorJumpPreviewWindow>("Sector Jump Preview");

        private void OnEnable()
        {
            profile = AssetDatabase.LoadAssetAtPath<SectorJumpProfile>("Assets/Settings/Game/SectorJumpProfile.asset");
            material = new Material(Shader.Find("Astronomical/Travel/SectorJump")) { hideFlags = HideFlags.HideAndDontSave };
            outgoingSky = new SectorJumpSkyPreview("Assets/Scenes/Locales/Locale_3.unity");
            incomingSky = new SectorJumpSkyPreview("Assets/Scenes/Locales/Locale_1.unity");
            material.SetTexture("_ShipImage", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Ship.png"));
            target = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave };
            previousTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            outgoingSky.Dispose();
            incomingSky.Dispose();
            DestroyImmediate(profileEditor);
            DestroyImmediate(material);
            target.Release();
            DestroyImmediate(target);
        }

        private void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            if (playing && profile)
            {
                time += (float)(now - previousTime);
                if (time > profile.Duration + 0.7f)
                {
                    if (loop) time = -0.4f;
                    else playing = false;
                }
                Repaint();
            }
            previousTime = now;
        }

        private void OnGUI()
        {
            profile = (SectorJumpProfile)EditorGUILayout.ObjectField("Profile", profile, typeof(SectorJumpProfile), false);
            if (!profile) return;
            EditorGUILayout.LabelField("Locales", "Illustrated blue to copper/turquoise");
            if (Event.current.type == EventType.Repaint)
            {
                var angle = heading * Mathf.Deg2Rad;
                var direction = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
                var distance = SectorJumpPreview.TravelFraction(profile, time) * profile.skyTravelDistance;
                material.SetTexture("_FromSky", outgoingSky.Render(direction * distance));
                material.SetTexture("_ToSky", incomingSky.Render(direction * (distance - profile.skyTravelDistance)));
                SectorJumpPreview.Apply(material, profile, time, heading, 16f / 9f, 540f);
                Graphics.Blit(null, target, material);
            }
            var rect = GUILayoutUtility.GetAspectRect(16f / 9f, GUILayout.MaxHeight(position.height * 0.48f));
            GUI.DrawTexture(rect, target, ScaleMode.ScaleToFit);
            var hudAlpha = profile.HudAlpha(time);
            var bar = new Rect(rect.center.x - 40, rect.center.y - rect.height * 0.09f - 20, 80, 6);
            EditorGUI.DrawRect(bar, new Color(0.1f, 0.8f, 1f, hudAlpha));
            bar.y += 8;
            EditorGUI.DrawRect(bar, new Color(0.2f, 0.95f, 0.25f, hudAlpha));
            var previousColor = GUI.color;
            GUI.color = new Color(1, 1, 1, hudAlpha);
            GUI.Label(new Rect(rect.x + 8, rect.y + 8, 180, 20), "HUD fade preview", EditorStyles.whiteLabel);
            GUI.color = previousColor;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Replay")) { time = -0.4f; playing = true; }
                if (GUILayout.Button(playing ? "Pause" : "Play")) playing = !playing;
                loop = GUILayout.Toggle(loop, "Loop");
                if (GUILayout.Button("Save profile")) AssetDatabase.SaveAssetIfDirty(profile);
            }
            EditorGUI.BeginChangeCheck();
            var scrub = EditorGUILayout.Slider("Time", Mathf.Clamp(time, 0, profile.Duration), 0, profile.Duration);
            if (EditorGUI.EndChangeCheck()) time = scrub;
            heading = EditorGUILayout.Slider("Jump heading", heading, -180f, 180f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            UnityEditor.Editor.CreateCachedEditor(profile, null, ref profileEditor);
            profileEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
    }
}
