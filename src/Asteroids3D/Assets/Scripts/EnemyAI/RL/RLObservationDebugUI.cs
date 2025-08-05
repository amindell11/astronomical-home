using System.Collections.Generic;
using UnityEngine;

namespace EnemyAI.RL
{
    /// <summary>
    /// Renders an IMGUI window to display the real-time observations of an
    /// <see cref="RLCommanderAgent"/> for debugging purposes.
    /// Attach this component to the same GameObject as the <see cref="RLCommanderAgent"/>.
    /// </summary>
    [RequireComponent(typeof(RLCommanderAgent))]
    public class RLObservationDebugUI : MonoBehaviour
    {
        private RLCommanderAgent agent;
        private RLObserver observer;

        // --- GUI Style ---
        private GUIStyle labelStyle;
        private GUIStyle backgroundStyle;
        private const int windowWidth = 280;
        private const int windowHeight = 380;
        private Rect windowRect;

        void Start()
        {
            agent = GetComponent<RLCommanderAgent>();
        
            // Initial position of the debug window
            windowRect = new Rect(10, 10, windowWidth, windowHeight);
        }

        void OnGUI()
        {
            // Only render if the agent exists and the debug flag is enabled
            if (agent == null || !agent.ShowObservationUI)
            {
                return;
            }

            // Get fresh observer reference each frame in case it was recreated during episode reset
            observer = agent.Observer;
            if (observer == null)
            {
                return;
            }

            // Lazy-initialize GUI styles
            if (labelStyle == null)
            {
                InitializeStyles();
            }

            // Draw the window
            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawDebugWindow, "Agent Observations", backgroundStyle);
        }
    
        private void InitializeStyles()
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            labelStyle.normal.textColor = Color.white;

            backgroundStyle = new GUIStyle(GUI.skin.box);
            Texture2D bgTexture = new Texture2D(1, 1);
            // A dark, semi-transparent background color
            bgTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.2f, 0.8f));
            bgTexture.Apply();
            backgroundStyle.normal.background = bgTexture;
        }

        /// <summary>
        /// Renders the content of the debug window.
        /// </summary>
        private void DrawDebugWindow(int windowID)
        {
            List<float> lastObservations = observer.LastObservations;
        
            if (lastObservations == null || lastObservations.Count == 0)
            {
                GUILayout.Label("No observations available yet.", labelStyle);
                return;
            }

            // Sanity check to ensure labels and data match
            if (lastObservations.Count != RLObserver.ObservationLabels.Length)
            {
                GUILayout.Label(
                    $"Error: Mismatch!\nObs count ({lastObservations.Count}) != Labels count ({RLObserver.ObservationLabels.Length})", 
                    labelStyle);
                return;
            }

            // --- Render each observation's Label, Value, and a Visual Bar ---
            for (int i = 0; i < RLObserver.ObservationLabels.Length; i++)
            {
                GUILayout.BeginHorizontal();
            
                // 1. Label
                GUILayout.Label($"{RLObserver.ObservationLabels[i]}:", labelStyle, GUILayout.Width(150));
            
                // 2. Value
                float value = lastObservations[i];
                GUILayout.Label(value.ToString("F2"), labelStyle, GUILayout.Width(40));
            
                // 3. Visualization Bar
                // Use a slider for a simple visual representation of the value in the [-1, 1] range
                GUILayout.HorizontalSlider(value, -1f, 1f, GUILayout.ExpandWidth(true));
            
                GUILayout.EndHorizontal();
            }
        
            // Allow the user to drag the window around the screen
            GUI.DragWindow();
        }
    }
} 