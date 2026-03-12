#if UNITY_EDITOR
using AI.States;
using UnityEditor;
using UnityEngine;

namespace AI.Utility
{
    [CustomPropertyDrawer(typeof(StateWeight))]
    public class UtilityWeightDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var profileProp = property.FindPropertyRelative("profile");
            var weightProp = property.FindPropertyRelative("weight");

            var profileRect = new Rect(position.x, position.y, position.width * 0.5f - 2, position.height);
            var sliderRect = new Rect(position.x + position.width * 0.5f + 2, position.y, position.width * 0.5f - 2, position.height);

            EditorGUI.PropertyField(profileRect, profileProp, GUIContent.none);
            weightProp.floatValue = EditorGUI.Slider(sliderRect, weightProp.floatValue, 0f, 2f);
        }
    }
}
#endif
