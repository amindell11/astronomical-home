using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Custom inspector for all <see cref="Sector"/> types: the edit-time Sync/reconcile button (bakes the manifest from placed children), a drift badge, and the derived signal-graph causal tree with validation findings.</summary>
    [CustomEditor(typeof(Sector), editorForChildClasses: true)]
    public class SectorEditor : Editor
    {
        private static bool showSignalGraph = true;

        public override void OnInspectorGUI()
        {
            var sector = (Sector)target;

            DrawDriftBadge(sector);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync Manifest", GUILayout.Height(24)))
                    DoSync(sector);
            }

            DrawSignalGraph(sector);

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

        private static void DrawSignalGraph(Sector sector)
        {
            var model = SectorSignalGraph.Build(sector);
            if (model.Ports.Count == 0 && model.Findings.Count == 0) return;

            EditorGUILayout.Space();
            showSignalGraph = EditorGUILayout.Foldout(showSignalGraph, "Signal Graph", toggleOnLabelClick: true);
            if (!showSignalGraph) return;

            foreach (var finding in model.Findings)
                EditorGUILayout.HelpBox(finding.Message,
                    finding.Severity == SectorSignalGraph.Severity.Error ? MessageType.Error : MessageType.Info);

            var visited = new HashSet<Component>();
            foreach (var node in model.Ports)
            {
                if (!node.Publisher || IsDownstream(model, node.Publisher)) continue;
                DrawPublisher(model, node.Publisher, visited);
            }

            foreach (var node in model.Ports)
                if (!node.Publisher)
                    EditorGUILayout.LabelField($"◌ SignalPort on '{node.Port.name}' ({node.Port.Kind}) — unowned");
        }

        /// <summary>A publisher is downstream when it consumes any owned port — it renders under its source, not as a root.</summary>
        private static bool IsDownstream(SectorSignalGraph.Model model, Component publisher)
        {
            foreach (var node in model.Ports)
                if (node.Publisher && node.Consumers.Contains(publisher))
                    return true;
            return false;
        }

        private static void DrawPublisher(SectorSignalGraph.Model model, Component publisher, HashSet<Component> visited)
        {
            if (!visited.Add(publisher))
            {
                EditorGUILayout.LabelField($"↺ {publisher.GetType().Name} '{publisher.name}' (already shown)");
                return;
            }

            EditorGUILayout.LabelField($"● {publisher.GetType().Name} '{publisher.name}'");
            EditorGUI.indentLevel++;
            foreach (var node in model.Ports)
            {
                if (node.Publisher != publisher) continue;
                var unconsumed = node.Consumers.Count == 0 ? " — unconsumed" : "";
                EditorGUILayout.LabelField($"◇ {node.Role} ({node.Port.Kind}){unconsumed}");
                EditorGUI.indentLevel++;
                foreach (var consumer in node.Consumers)
                {
                    if (PublishesAnything(model, consumer)) DrawPublisher(model, consumer, visited);
                    else EditorGUILayout.LabelField($"→ {consumer.GetType().Name} '{consumer.name}'");
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }

        private static bool PublishesAnything(SectorSignalGraph.Model model, Component component)
        {
            foreach (var node in model.Ports)
                if (node.Publisher == component)
                    return true;
            return false;
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
