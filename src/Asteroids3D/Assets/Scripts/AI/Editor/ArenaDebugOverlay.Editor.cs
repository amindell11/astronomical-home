#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using AI.States;
using AI.Utility;
using Game.Services;
using Ships;
using UnityEngine;

namespace AI.Debug
{
    /// <summary>
    /// Debug overlay for the AI Arena: state labels, utility bars, and target lines.
    /// Attach to the ArenaSectorManager prefab or any active GameObject in the arena scene.
    /// </summary>
    public class ArenaDebugOverlay : MonoBehaviour
    {
        private AIDebugSettings debugSettings;
        private IUnitService boundUnits;

        /// <summary>
        /// Initialise the overlay. When a unit service is supplied, the overlay self-subscribes to
        /// <see cref="IUnitService.OnShipSpawned"/> and auto-tracks every ship (existing + future) —
        /// no per-sector RegisterShip wiring needed.
        /// </summary>
        public void Initialize(AIDebugSettings settings, IUnitService units = null)
        {
            debugSettings = settings;
            if (units == null) return;

            foreach (var ship in units.ActiveRegistry.ActiveShips)
                RegisterShip(ship);
            units.OnShipSpawned += RegisterShip;
            boundUnits = units;
        }

        [Header("Visual Settings")]
        [SerializeField] private Vector2 labelOffset = new(0, 40);
        [SerializeField] private float barWidth = 120f;
        [SerializeField] private float barHeight = 14f;

        private static readonly Color TeamAColor = new(0.3f, 0.6f, 1f, 0.8f);
        private static readonly Color TeamBColor = new(1f, 0.4f, 0.3f, 0.8f);
        private static readonly Color DefaultStateColor = new(0.5f, 0.5f, 0.5f);

        private Camera mainCam;
        private Material lineMaterial;
        private readonly List<Ship> trackedShips = new();

        private GUIStyle labelStyle;
        private GUIStyle barBgStyle;
        private GUIStyle barFillStyle;
        private GUIStyle scoreStyle;
        private bool stylesInitialized;

        public void RegisterShip(Ship ship)
        {
            if (ship && !trackedShips.Contains(ship))
                trackedShips.Add(ship);
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            barBgStyle = new GUIStyle(GUI.skin.box);
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.5f));
            bgTex.Apply();
            barBgStyle.normal.background = bgTex;

            barFillStyle = new GUIStyle();
            var fillTex = new Texture2D(1, 1);
            fillTex.SetPixel(0, 0, Color.white);
            fillTex.Apply();
            barFillStyle.normal.background = fillTex;

            scoreStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 10,
                normal = { textColor = Color.white }
            };

            stylesInitialized = true;
        }

        private void Update()
        {
            trackedShips.RemoveAll(s => !s);

            if (!mainCam)
                mainCam = Camera.main;
        }

        private void OnGUI()
        {
            if (!mainCam) return;
            var showStateLabels = debugSettings != null && debugSettings.IsActive(AIDebugChannel.Info);
            var showUtilityScores = debugSettings != null && debugSettings.IsActive(AIDebugChannel.Utility);
            if (!showStateLabels && !showUtilityScores) return;

            InitStyles();

            foreach (var ship in trackedShips)
            {
                if (!ship || !ship.gameObject.activeInHierarchy) continue;

                var aiCommander = ship.Commander as AICommander;
                if (!aiCommander) continue;

                var screenPos = mainCam.WorldToScreenPoint(ship.transform.position);
                if (screenPos.z < 0) continue;

                // Unity OnGUI has Y inverted
                var guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);

                if (showStateLabels)
                    DrawStateLabel(aiCommander, guiPos);

                if (showUtilityScores)
                    DrawUtilityBars(aiCommander, guiPos);
            }
        }

        private void DrawStateLabel(AICommander commander, Vector2 guiPos)
        {
            var stateName = commander.CurrentStateName;
            var color = GetCurrentStateColor(commander);
            labelStyle.normal.textColor = color;

            var pos = new Vector2(guiPos.x - 60 + labelOffset.x, guiPos.y - 25 + labelOffset.y);
            GUI.Label(new Rect(pos.x, pos.y, 120, 20), stateName, labelStyle);
        }

        private static Color GetCurrentStateColor(AICommander commander)
        {
            var name = commander.UtilityChooser?.CurrentAIState?.ProfileName;
            return name != null ? ColorFromName(name) : DefaultStateColor;
        }

        private static Color GetStateColor(AI.States.AIState aiState)
        {
            return aiState != null ? ColorFromName(aiState.ProfileName) : DefaultStateColor;
        }

        private static Color ColorFromName(string name)
        {
            var hash = (uint)name.GetHashCode();
            var h = (hash % 360) / 360f;
            return Color.HSVToRGB(h, 0.7f, 0.9f);
        }

        private void DrawUtilityBars(AICommander commander, Vector2 guiPos)
        {
            var scores = commander.UtilityChooser?.UtilityScores;
            if (scores == null || scores.Count == 0) return;

            var currentName = commander.UtilityChooser.CurrentAIState?.ProfileName;
            var sorted = scores.OrderByDescending(kv => kv.Value).ToList();
            var maxScore = sorted.Count > 0 ? Mathf.Max(sorted[0].Value, 1f) : 1f;

            var startY = guiPos.y - 25 + labelOffset.y + 20;
            var startX = guiPos.x - barWidth * 0.5f + labelOffset.x;

            // Background panel
            var panelHeight = sorted.Count * (barHeight + 2) + 4;
            GUI.Box(new Rect(startX - 4, startY - 2, barWidth + 58, panelHeight),
                GUIContent.none, barBgStyle);

            // Build a name→color map from registered states
            var stateColorMap = new Dictionary<string, Color>();
            var registeredStates = commander.UtilityChooser?.RegisteredStates;
            if (registeredStates != null)
            {
                foreach (var state in registeredStates)
                    stateColorMap[state.ProfileName] = GetStateColor(state);
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                var kv = sorted[i];
                var y = startY + i * (barHeight + 2);
                var fill = kv.Value / maxScore;
                var color = stateColorMap.TryGetValue(kv.Key, out var sc) ? sc : Color.grey;

                if (currentName != null && kv.Key == currentName)
                    color = Color.Lerp(color, Color.white, 0.3f);

                // Bar background
                GUI.Box(new Rect(startX, y, barWidth, barHeight), GUIContent.none, barBgStyle);

                // Bar fill
                var prevColor = GUI.color;
                GUI.color = color;
                GUI.Box(new Rect(startX, y, barWidth * fill, barHeight), GUIContent.none, barFillStyle);
                GUI.color = prevColor;

                // Label
                var shortName = kv.Key.Substring(0, Mathf.Min(4, kv.Key.Length));
                scoreStyle.normal.textColor = kv.Key == currentName ? Color.white : new Color(0.8f, 0.8f, 0.8f);
                GUI.Label(new Rect(startX + 2, y, barWidth, barHeight),
                    shortName, scoreStyle);
                GUI.Label(new Rect(startX + barWidth + 4, y, 50, barHeight),
                    kv.Value.ToString("F2"), scoreStyle);
            }
        }

        private void CreateLineMaterial()
        {
            if (lineMaterial) return;
            var shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }

        private void OnRenderObject()
        {
            if (debugSettings == null || !debugSettings.IsActive(AIDebugChannel.Targeting)) return;

            CreateLineMaterial();
            lineMaterial.SetPass(0);

            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

            foreach (var ship in trackedShips)
            {
                if (!ship || !ship.gameObject.activeInHierarchy) continue;

                var aiCommander = ship.Commander as AICommander;
                if (!aiCommander) continue;

                var context = aiCommander.UtilityChooser?.Context;
                if (context == null) continue;
                var enemy = context.Combat?.Enemy;
                if (!enemy || !enemy.gameObject.activeInHierarchy) continue;

                var color = ship.teamNumber == 0 ? TeamAColor : TeamBColor;
                GL.Color(color);
                GL.Vertex(ship.transform.position);
                GL.Vertex(enemy.transform.position);
            }

            GL.End();
            GL.PopMatrix();
        }

        private void OnDestroy()
        {
            if (boundUnits != null) boundUnits.OnShipSpawned -= RegisterShip;
            boundUnits = null;
            if (lineMaterial)
                DestroyImmediate(lineMaterial);
            trackedShips.Clear();
        }
    }
}
#endif
