using UnityEditor;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>
    /// Custom inspector for all <see cref="Sector"/> types. Adds the edit-time Sync/reconcile
    /// button (bakes the manifest from placed children) and a drift badge showing unsynced
    /// children / orphaned entries. The runtime never crawls — this button is where discovery
    /// happens, and its output is the serialized manifest.
    /// </summary>
    [CustomEditor(typeof(Sector), editorForChildClasses: true)]
    public class SectorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var sector = (Sector)target;

            DrawDriftBadge(sector);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync Manifest", GUILayout.Height(24)))
                    DoSync(sector);
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private static void DrawDriftBadge(Sector sector)
        {
            var drift = sector.ComputeDrift();
            if (!drift.HasDrift)
            {
                EditorGUILayout.HelpBox("Manifest in sync with placed children.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                $"Manifest drift: {drift.UnsyncedChildren} unsynced child(ren), {drift.OrphanedEntries} orphaned entry(ies). Press Sync.",
                MessageType.Warning);
        }

        private void DoSync(Sector sector)
        {
            Undo.RegisterCompleteObjectUndo(sector, "Sync Sector Manifest");
            var result = sector.SyncManifest();
            EditorUtility.SetDirty(sector);
            serializedObject.Update();

            Debug.Log(
                $"[SectorEditor] Synced '{sector.name}': +{result.AppendedAdopt} adopt, +{result.AppendedSpawner} spawner, " +
                $"+{result.AppendedModule} module, -{result.OrphanedAdopt} orphaned adopt, " +
                $"-{result.OrphanedSpawner} orphaned spawner, -{result.OrphanedModule} orphaned module.",
                sector);
        }
    }
}
