using UnityEditor;
using UnityEngine;

namespace InspectorTools
{
    // Renames fields inside nested classes. Top level fields (including arrays) are renamed by EssentialsInspector.
    [CustomPropertyDrawer(typeof(LabelAttribute))]
    public class LabelDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Unity runs this drawer for each array element too; keep their "Element 0" labels.
            if (!property.propertyPath.EndsWith("]")) label.text = ((LabelAttribute)attribute).Label;
            EditorGUI.PropertyField(position, property, label, true);
        }
    }
}
