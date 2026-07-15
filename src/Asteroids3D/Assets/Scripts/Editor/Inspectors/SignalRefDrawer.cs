using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Sectors.Inspectors
{
    /// <summary>Draws a <see cref="SignalRef"/> as one popup of the owning component's same-<see cref="Sector"/> publishers × their code-declared outputs, labelled "Owner ∕ output (Kind)". UI-only — Setup re-checks ownership and declaration, since code seams can inject anything.</summary>
    [CustomPropertyDrawer(typeof(SignalRef))]
    public class SignalRefDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var sourceProp = property.FindPropertyRelative("source");
            var outputProp = property.FindPropertyRelative("output");
            var component = property.serializedObject.targetObject as Component;
            var sector = component ? component.GetComponentInParent<Sector>(true) : null;
            if (!sector)
            {
                EditorGUI.PropertyField(position, sourceProp, label);
                EditorGUI.EndProperty();
                return;
            }

            var current = new SignalRef(sourceProp.objectReferenceValue as Component, outputProp.stringValue);
            var options = new List<GUIContent> { new GUIContent("None") };
            var values = new List<SignalRef> { default };
            foreach (var source in sector.GetComponentsInChildren<ISignalSource>(true))
            {
                var publisher = (Component)source;
                foreach (var output in source.Outputs)
                {
                    options.Add(new GUIContent($"{publisher.name} ∕ {output.Id} ({output.Kind})"));
                    values.Add(new SignalRef(publisher, output.Id));
                }
            }

            var index = current.IsAssigned ? values.IndexOf(current) : 0;
            if (index < 0)
            {
                var name = current.source ? current.source.name : "(missing)";
                options.Add(new GUIContent($"(invalid) {name} ∕ {current.output}"));
                values.Add(current);
                index = values.Count - 1;
            }

            var next = EditorGUI.Popup(position, label, index, options.ToArray());
            if (next != index)
            {
                sourceProp.objectReferenceValue = values[next].source;
                outputProp.stringValue = values[next].output;
            }

            EditorGUI.EndProperty();
        }
    }
}
