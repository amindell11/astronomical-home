using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AI.States.Editor
{
    /// <summary>
    /// Draws [SerializeReference] fields for GoalStrategy and UtilityFactor
    /// with an explicit type picker dropdown.
    /// </summary>
    [CustomPropertyDrawer(typeof(GoalStrategy), true)]
    [CustomPropertyDrawer(typeof(UtilityFactor), true)]
    public class SerializeReferenceTypeDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, Type[]> typeCache = new();
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight; // dropdown

            if (property.managedReferenceValue != null)
            {
                var iter = property.Copy();
                var end = iter.GetEndProperty();
                if (iter.NextVisible(true))
                {
                    while (!SerializedProperty.EqualContents(iter, end))
                    {
                        height += EditorGUI.GetPropertyHeight(iter, true) + Spacing;
                        if (!iter.NextVisible(false)) break;
                    }
                }
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var baseType = fieldInfo.FieldType;
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(List<>))
                baseType = baseType.GetGenericArguments()[0];
            else if (baseType.IsArray)
                baseType = baseType.GetElementType();

            var concreteTypes = GetConcreteTypes(baseType);
            var typeNames = concreteTypes.Select(t => t.Name).ToArray();

            var currentType = property.managedReferenceValue?.GetType();
            var currentIndex = currentType != null ? Array.IndexOf(concreteTypes, currentType) : -1;

            // Type picker dropdown
            var dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var newIndex = EditorGUI.Popup(dropdownRect, label.text, currentIndex, typeNames);

            if (newIndex != currentIndex && newIndex >= 0)
            {
                property.managedReferenceValue = Activator.CreateInstance(concreteTypes[newIndex]);
                property.serializedObject.ApplyModifiedProperties();
            }

            // Draw child fields indented below using a copy to avoid corrupting the iterator
            if (property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                var iter = property.Copy();
                var end = property.GetEndProperty();
                var y = position.y + EditorGUIUtility.singleLineHeight + Spacing;

                if (iter.NextVisible(true))
                {
                    while (!SerializedProperty.EqualContents(iter, end))
                    {
                        var h = EditorGUI.GetPropertyHeight(iter, true);
                        var fieldRect = new Rect(position.x, y, position.width, h);
                        EditorGUI.PropertyField(fieldRect, iter, true);
                        y += h + Spacing;
                        if (!iter.NextVisible(false)) break;
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private static Type[] GetConcreteTypes(Type baseType)
        {
            if (typeCache.TryGetValue(baseType, out var cached))
                return cached;

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => t.IsClass && !t.IsAbstract && baseType.IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToArray();

            typeCache[baseType] = types;
            return types;
        }
    }
}
