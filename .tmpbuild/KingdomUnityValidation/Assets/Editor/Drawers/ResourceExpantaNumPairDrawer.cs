using UnityEditor;
using UnityEngine;

namespace Kingdom.EditorTools
{
    [CustomPropertyDrawer(typeof(Pair<Resource, ExpantaNum>))]
    public sealed class ResourceExpantaNumPairDrawer : PropertyDrawer
    {
        private const float Gap = 6f;
        private const float ResourceWidthRatio = 0.55f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty resourceProperty = property.FindPropertyRelative("first");
            SerializedProperty amountProperty = property.FindPropertyRelative("second");

            float resourceWidth = (position.width - Gap) * ResourceWidthRatio;
            var resourceRect = new Rect(position.x, position.y, resourceWidth, position.height);
            var amountRect = new Rect(
                resourceRect.xMax + Gap,
                position.y,
                position.width - resourceWidth - Gap,
                position.height);

            EditorGUI.PropertyField(resourceRect, resourceProperty, GUIContent.none);

            ExpantaNum amount = amountProperty.boxedValue is ExpantaNum value
                ? value
                : ExpantaNum.Zero;
            string entered = EditorGUI.DelayedTextField(amountRect, amount.ToString());
            if (entered != amount.ToString())
            {
                if (ExpantaNum.TryParse(entered, out ExpantaNum parsed))
                    amountProperty.boxedValue = parsed;
                else
                    Debug.LogWarning($"'{entered}' is not a valid ExpantaNum value at {property.propertyPath}.");
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;
    }
}
