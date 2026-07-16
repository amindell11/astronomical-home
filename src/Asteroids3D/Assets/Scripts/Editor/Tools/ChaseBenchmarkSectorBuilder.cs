using AI;
using Asteroids.Fields;
using Game.Benchmark;
using Game.Sectors;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Tools.Editor
{
    /// <summary>
    /// Rebuilds <c>Assets/Prefabs/Sectors/ChaseBenchmarkSector.prefab</c> programmatically
    /// through Unity's prefab API. The original was hand-authored YAML whose nested prefab
    /// instances did not survive import (the imported main asset degenerated to the pilot
    /// rig with no children); regenerating through PrefabUtility guarantees a well-formed
    /// asset and gives future edits a reproducible source of truth.
    /// Menu: Tools ▸ Chase Benchmark ▸ Rebuild Sector Prefab, or headless via
    /// <c>-executeMethod Tools.Editor.ChaseBenchmarkSectorBuilder.Build</c>.
    /// </summary>
    public static class ChaseBenchmarkSectorBuilder
    {
        private const string OutputPath = "Assets/Prefabs/Sectors/ChaseBenchmarkSector.prefab";
        private const string Ship1Path = "Assets/Prefabs/Ships/Ship_1.prefab";
        private const string Ship2Path = "Assets/Prefabs/Ships/Ship_2.prefab";
        private const string PilotPath = "Assets/Prefabs/Pilots/UtilityPilot.prefab";
        private const string FieldPath = "Assets/Prefabs/Asteroid/AsteroidController.prefab";
        private const string PursueProfilePath = "Assets/Settings/AI/StateProfiles/ChasePursue.asset";
        private const string FleeProfilePath = "Assets/Settings/AI/StateProfiles/ChaseFlee.asset";

        [MenuItem("Tools/Chase Benchmark/Rebuild Sector Prefab")]
        public static void Build()
        {
            var ship1 = Load<GameObject>(Ship1Path);
            var ship2 = Load<GameObject>(Ship2Path);
            var pilot = Load<GameObject>(PilotPath);
            var fieldPrefab = Load<GameObject>(FieldPath);
            var pursueProfile = Load<Object>(PursueProfilePath);
            var fleeProfile = Load<Object>(FleeProfilePath);

            var root = new GameObject("ChaseBenchmarkSector");
            try
            {
                // ── Content children (nested prefab instances) ──
                var fieldGo = Instantiate(fieldPrefab, root.transform, "AsteroidField", Vector3.zero);
                var pursuerGo = Instantiate(ship2, root.transform, "Pursuer", new Vector3(0f, -8f, 0f));
                var evaderGo = Instantiate(ship1, root.transform, "Evader", new Vector3(0f, 8f, 0f));
                var pursuerPilotGo = Instantiate(pilot, pursuerGo.transform, "UtilityPilot", Vector3.zero);
                var evaderPilotGo = Instantiate(pilot, evaderGo.transform, "UtilityPilot", Vector3.zero);

                ConfigurePilot(pursuerPilotGo, pursueProfile);
                ConfigurePilot(evaderPilotGo, fleeProfile);

                var pursuerShip = pursuerGo.GetComponent<Ship>();
                var evaderShip = evaderGo.GetComponent<Ship>();
                var field = fieldGo.GetComponentInChildren<UpdatingAsteroidField>();
                var fieldSpawner = fieldGo.GetComponentInChildren<AsteroidFieldSpawner>();

                // ── Sector manifest ──
                var sector = root.AddComponent<Sector>();
                var so = new SerializedObject(sector);
                var adopted = so.FindProperty("adopted");
                adopted.arraySize = 2;
                WriteAdoptEntry(adopted.GetArrayElementAtIndex(0), pursuerShip, team: 0);
                WriteAdoptEntry(adopted.GetArrayElementAtIndex(1), evaderShip, team: 1);
                var spawners = so.FindProperty("spawners");
                spawners.arraySize = 1;
                spawners.GetArrayElementAtIndex(0).objectReferenceValue = fieldSpawner;

                // ── Benchmark module ──
                var module = root.AddComponent<ChaseBenchmarkModule>();
                var mo = new SerializedObject(module);
                mo.FindProperty("pursuer").objectReferenceValue = pursuerShip;
                mo.FindProperty("evader").objectReferenceValue = evaderShip;
                mo.FindProperty("pursuerPilot").objectReferenceValue = pursuerPilotGo.GetComponent<AICommander>();
                mo.FindProperty("evaderPilot").objectReferenceValue = evaderPilotGo.GetComponent<AICommander>();
                mo.FindProperty("field").objectReferenceValue = field;
                mo.FindProperty("fieldSeed").intValue = 20260705;
                mo.FindProperty("fieldOffset").vector2Value = Vector2.zero;
                mo.FindProperty("episodeDuration").floatValue = 0f;
                mo.FindProperty("contactRadius").floatValue = 15f;
                mo.FindProperty("keepShipsAlive").boolValue = true;
                mo.ApplyModifiedPropertiesWithoutUndo();

                var modules = so.FindProperty("modules");
                modules.arraySize = 1;
                modules.GetArrayElementAtIndex(0).objectReferenceValue = module;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, OutputPath, out var ok);
                if (!ok) throw new System.InvalidOperationException($"SaveAsPrefabAsset failed for {OutputPath}");
                AssetDatabase.SaveAssets();
                Debug.Log($"[ChaseBenchmarkSectorBuilder] rebuilt {OutputPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigurePilot(GameObject pilotGo, Object stateProfile)
        {
            var brain = new SerializedObject(pilotGo.GetComponent<Brain>());
            var profiles = brain.FindProperty("chooser").FindPropertyRelative("stateProfiles");
            profiles.arraySize = 1;
            profiles.GetArrayElementAtIndex(0).objectReferenceValue = stateProfile;
            brain.ApplyModifiedPropertiesWithoutUndo();

            var scout = new SerializedObject(pilotGo.GetComponent<Scout>());
            scout.FindProperty("nearbyShipRadius").floatValue = 300f;
            scout.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteAdoptEntry(SerializedProperty entry, Component target, int team)
        {
            entry.FindPropertyRelative("target").objectReferenceValue = target;
            entry.FindPropertyRelative("team").intValue = team;
            entry.FindPropertyRelative("startActive").boolValue = true;
            var respawn = entry.FindPropertyRelative("respawn");
            respawn.FindPropertyRelative("origin").enumValueIndex = 0;
            respawn.FindPropertyRelative("point").vector2Value = Vector2.zero;
            respawn.FindPropertyRelative("radius").floatValue = 0f;
            respawn.FindPropertyRelative("delay").floatValue = 1f;
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent, string name, Vector3 localPos)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new System.InvalidOperationException($"Missing asset at {path}");
            return asset;
        }
    }
}
