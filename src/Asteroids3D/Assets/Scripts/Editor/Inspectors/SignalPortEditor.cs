using UnityEditor;
using UnityEngine;

namespace Game.Sectors.Inspectors
{
    /// <summary>Header for <see cref="SignalPort"/> components: derived owner ∕ role and consumers, so otherwise-identical ports self-describe. Identity stays derived from the graph — never an authored name that could drift.</summary>
    [CustomEditor(typeof(SignalPort))]
    public class SignalPortEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var port = (SignalPort)target;
            var sector = port.GetComponentInParent<Sector>(true);
            var node = sector ? SectorSignalGraph.Build(sector).NodeFor(port) : null;

            if (node != null && node.Publisher)
            {
                EditorGUILayout.LabelField($"{node.Publisher.GetType().Name} ∕ {node.Role}", EditorStyles.boldLabel);
                foreach (var consumer in node.Consumers)
                    EditorGUILayout.LabelField($"→ {consumer.GetType().Name} '{consumer.name}'");
            }
            else if (sector)
            {
                EditorGUILayout.HelpBox("Unowned — no publisher field references this port.", MessageType.Warning);
            }

            DrawDefaultInspector();
        }
    }
}
