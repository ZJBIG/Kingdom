using UnityEditor;
using UnityEngine;

namespace Kingdom.EditorTools
{
    [CustomPropertyDrawer(typeof(ExpantaNum))]
    public sealed class ExpantaNumDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            ExpantaNum value = property.boxedValue is ExpantaNum number ? number : ExpantaNum.Zero;
            string entered = EditorGUI.DelayedTextField(position, label, value.ToString());
            if (entered != value.ToString())
            {
                if (ExpantaNum.TryParse(entered, out ExpantaNum parsed))
                    property.boxedValue = parsed;
                else
                    Debug.LogWarning($"'{entered}' is not a valid ExpantaNum value at {property.propertyPath}.");
            }
            EditorGUI.EndProperty();
        }
    }
}
