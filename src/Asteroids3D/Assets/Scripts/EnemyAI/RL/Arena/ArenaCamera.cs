using System.Collections.Generic;
using System.Linq;
using Editor;
using UnityEngine;
using ShipMain;

namespace EnemyAI.RL.Arena
{
    /// <summary>
    /// A camera controller that can cycle through different arenas in a multi-arena setup.
    /// It inherits from CameraFollow to handle the framing and zooming logic, but
    /// overrides the target-finding mechanism to focus on ships within a single, selected arena.
    /// </summary>
    [RequireComponent(typeof(CameraFollow))]
    public class ArenaCamera : CameraFollow
    {
        [Header("Arena Targeting")]
        [Tooltip("Key to switch to the next arena.")]
        [SerializeField] private KeyCode nextArenaKey = KeyCode.RightArrow;
        [Tooltip("Key to switch to the previous arena.")]
        [SerializeField] private KeyCode prevArenaKey = KeyCode.LeftArrow;
        [Tooltip("Show on-screen display for current arena.")]
        [SerializeField] private bool showHUD = true;

        private ArenaManager arenaManager;
        private List<ArenaInstance> arenas;
        private int currentArenaIndex = -1;

        protected override void Awake()
        {
            // Must happen before base.Awake() so it doesn't try to find a "Player" tagged object
            keepPlayerInView = false;
        
            base.Awake(); // This will call our overridden RefreshTargets(), which will find no targets yet.
        }

        void Start()
        {
            arenaManager = ArenaManager.Instance;
            if (arenaManager == null || !arenaManager.IsMultiArenaMode)
            {
                RLog.RL("ArenaCamera: ArenaManager not found or not in multi-arena mode. Disabling component.");
                enabled = false;
                return;
            }
        
            // Subscribe to event and also try to initialize immediately in case arenas are already spawned.
            arenaManager.OnArenasSpawned += InitializeArenas;
            InitializeArenas();
        }

        protected override void Update()
        {
            // Handle input for switching arenas
            if (arenas != null && arenas.Count > 0)
            {
                if (Input.GetKeyDown(nextArenaKey))
                {
                    SelectArena((currentArenaIndex + 1) % arenas.Count);
                }
                else if (Input.GetKeyDown(prevArenaKey))
                {
                    int newIndex = currentArenaIndex - 1;
                    if (newIndex < 0) newIndex = arenas.Count - 1;
                    SelectArena(newIndex);
                }
            }
        
            // Let the base class handle periodic target list refreshing
            base.Update();
        }

        private void InitializeArenas()
        {
            // Guard against this being called multiple times
            if (arenas != null && arenas.Count > 0) return;

            arenas = arenaManager.GetAllArenas();
            if (arenas.Count > 0)
            {
                SelectArena(0);
            }
            else
            {
                RLog.RL("ArenaCamera: Waiting for arenas to be spawned by ArenaManager...");
            }
        }

        private void SelectArena(int index)
        {
            if (arenas == null || index < 0 || index >= arenas.Count) return;
            currentArenaIndex = index;
            RefreshTargets(); // Immediately refresh targets on switch
        }

        protected override void RefreshTargets()
        {
            if (_targets == null) return;
            _targets.Clear();
            if (currentArenaIndex < 0 || arenas == null || currentArenaIndex >= arenas.Count) return;

            var currentArena = arenas[currentArenaIndex];
            if (!currentArena) return;

            var shipTransforms = currentArena.ships
                .Where(s => s != null && s.gameObject.activeInHierarchy)
                .Select(s => s.transform);

            _targets.AddRange(shipTransforms);
        }
    
        void OnDestroy()
        {
            // Unsubscribe from events
            if (arenaManager != null)
            {
                arenaManager.OnArenasSpawned -= InitializeArenas;
            }
        }
    
        void OnGUI()
        {
            if (!showHUD || currentArenaIndex < 0 || !enabled || arenas == null || arenas.Count == 0) return;
        
            GUI.Box(new Rect(10, Screen.height - 40, 250, 30), "");
            GUI.Label(new Rect(15, Screen.height - 35, 240, 20), $"Viewing Arena {currentArenaIndex} / {arenas.Count - 1}");
        }
    }
} 