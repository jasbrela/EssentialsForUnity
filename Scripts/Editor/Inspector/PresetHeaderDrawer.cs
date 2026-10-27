using UnityEditor;
using UnityEngine;

namespace InspectorTools
{
    // Fallback for editors that don't group fields (e.g. other custom editors): draws a plain colored header.
    // Mirrors Unity's built in HeaderDrawer so it looks like [Header], tinted with the attribute's color.
    [CustomPropertyDrawer(typeof(PresetHeaderAttribute), true)]
    public class PresetHeaderDrawer : DecoratorDrawer
    {
        // Set while EssentialsInspector draws, since it draws the headers as foldouts itself.
        internal static bool Suppressed;

        public override float GetHeight()
        {
            return Suppressed ? 0f : EditorGUIUtility.singleLineHeight * 1.5f;
        }

        public override void OnGUI(Rect position)
        {
            if (Suppressed) return;

            var header = (PresetHeaderAttribute)attribute;
            position.yMin += EditorGUIUtility.singleLineHeight * 0.5f;
            position = EditorGUI.IndentedRect(position);
            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = header.Color } };
            GUI.Label(position, header.Label, style);
        }
    }
}
