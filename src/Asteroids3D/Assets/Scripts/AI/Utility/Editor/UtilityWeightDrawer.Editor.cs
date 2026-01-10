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
            var typeProp = property.FindPropertyRelative("type");
            var weightProp = property.FindPropertyRelative("weight");

            var stateType = (StateType)typeProp.enumValueIndex;
            var labelRect = new Rect(position.x, position.y, 100, position.height);
            var sliderRect = new Rect(position.x + 105, position.y, position.width - 105, position.height);

            EditorGUI.LabelField(labelRect, stateType.ToString());
            weightProp.floatValue = EditorGUI.Slider(sliderRect, weightProp.floatValue, 0f, 2f);
        }
    }
}
#endif
