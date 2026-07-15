using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Sectors.Inspectors
{
    /// <summary>Draws a <see cref="SignalPort"/> reference as a picker restricted to ports under the owning component's own <see cref="Sector"/>, labelled "Owner / Role (Kind)". UI-only — Setup re-checks ownership, since code seams can inject cross-sector refs.</summary>
    [CustomPropertyDrawer(typeof(SignalPort))]
    public class SignalPortDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var component = property.serializedObject.targetObject as Component;
            var sector = component ? component.GetComponentInParent<Sector>(true) : null;
            if (!sector)
            {
                EditorGUI.PropertyField(position, property, label);
                EditorGUI.EndProperty();
                return;
            }

            var model = SectorSignalGraph.Build(sector);
            var current = property.objectReferenceValue as SignalPort;
            var options = new List<GUIContent> { new GUIContent("None") };
            var values = new List<SignalPort> { null };
            foreach (var port in sector.GetComponentsInChildren<SignalPort>(true))
            {
                options.Add(new GUIContent(LabelFor(model, port)));
                values.Add(port);
            }

            var index = values.IndexOf(current);
            if (index < 0)
            {
                options.Add(new GUIContent($"(outside sector) {current.name}"));
                values.Add(current);
                index = values.Count - 1;
            }

            var next = EditorGUI.Popup(position, label, index, options.ToArray());
            if (next != index) property.objectReferenceValue = values[next];

            EditorGUI.EndProperty();
        }

        private static string LabelFor(SectorSignalGraph.Model model, SignalPort port)
        {
            var node = model.NodeFor(port);
            var owned = node != null && node.Publisher;
            var owner = owned ? node.Publisher.name : port.name;
            var role = owned ? node.Role : "unowned";
            return $"{owner} ∕ {role} ({port.Kind})";
        }
    }
}
