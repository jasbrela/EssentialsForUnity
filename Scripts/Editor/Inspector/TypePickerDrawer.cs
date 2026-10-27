using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InspectorTools
{
    // Draws a [SerializeReference] field as usual, plus a dropdown in the value area to pick its type.
    // On lists Unity runs this drawer once per element, so every element gets its own dropdown.
    [CustomPropertyDrawer(typeof(TypePickerAttribute))]
    public class TypePickerDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, Type[]> TypesCache = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.LabelField(position, label.text, "[TypePicker] needs [SerializeReference]");
                return;
            }

            SeparateSharedReference(property);

            // Drawn before the field so it gets the click instead of the foldout.
            var buttonRect = new Rect(position.x + EditorGUIUtility.labelWidth + 2f, position.y,
                position.width - EditorGUIUtility.labelWidth - 2f, EditorGUIUtility.singleLineHeight);
            var current = property.managedReferenceValue?.GetType();
            var buttonLabel = new GUIContent(current == null ? "None" : ObjectNames.NicifyVariableName(current.Name));

            if (EditorGUI.DropdownButton(buttonRect, buttonLabel, FocusType.Keyboard))
            {
                var baseType = GetFieldType(property);
                if (baseType != null) ShowDropdown(buttonRect, property, baseType);
            }

            EditorGUI.PropertyField(position, property, label, true);
        }

        private static void ShowDropdown(Rect rect, SerializedProperty property, Type baseType)
        {
            // The dropdown answers after this GUI pass, so look the property up again on each target then.
            var targets = property.serializedObject.targetObjects;
            var path = property.propertyPath;

            new TypeDropdown(baseType, GetTypes(baseType), type =>
            {
                foreach (var target in targets)
                {
                    var serializedObject = new SerializedObject(target);
                    var targetProperty = serializedObject.FindProperty(path);
                    targetProperty.managedReferenceValue = type == null ? null : CreateInstance(type, targetProperty.managedReferenceValue);
                    serializedObject.ApplyModifiedProperties();
                }
            }).Show(rect);
        }

        // When "+" adds a list element, Unity points it to the same object as the element before it.
        // Give it its own copy so editing one doesn't change the other.
        private static void SeparateSharedReference(SerializedProperty property)
        {
            var path = property.propertyPath;
            if (property.serializedObject.isEditingMultipleObjects || !path.EndsWith("]")) return;

            var open = path.LastIndexOf('[');
            if (!int.TryParse(path.Substring(open + 1, path.Length - open - 2), out var index) || index == 0) return;

            var value = property.managedReferenceValue;
            var previous = property.serializedObject.FindProperty($"{path.Substring(0, open)}[{index - 1}]");
            if (value == null || previous == null || previous.managedReferenceId != property.managedReferenceId) return;

            property.managedReferenceValue = CreateInstance(value.GetType(), value);
            property.serializedObject.ApplyModifiedProperties();
        }

        // Keeps the values of fields with the same name, so switching type doesn't lose everything.
        private static object CreateInstance(Type type, object copyFrom)
        {
            var instance = Activator.CreateInstance(type, true);
            if (copyFrom != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(copyFrom), instance);
            return instance;
        }

        // managedReferenceFieldTypename is "Assembly Namespace.Type", with nested types as "Outer/Inner".
        private static Type GetFieldType(SerializedProperty property)
        {
            var parts = property.managedReferenceFieldTypename.Split(' ');
            return parts.Length == 2 ? Type.GetType($"{parts[1].Replace('/', '+')}, {parts[0]}") : null;
        }

        private static Type[] GetTypes(Type baseType)
        {
            if (TypesCache.TryGetValue(baseType, out var types)) return types;

            types = TypeCache.GetTypesDerivedFrom(baseType)
                .Append(baseType)
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition &&
                            !typeof(Object).IsAssignableFrom(t) && t.IsDefined(typeof(SerializableAttribute), false) &&
                            t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null)
                .OrderBy(t => t.Name)
                .ToArray();

            TypesCache[baseType] = types;
            return types;
        }

        private class TypeDropdown : AdvancedDropdown
        {
            private readonly string _title;
            private readonly Type[] _types;
            private readonly Action<Type> _onSelected;

            public TypeDropdown(Type baseType, Type[] types, Action<Type> onSelected) : base(new AdvancedDropdownState())
            {
                _title = ObjectNames.NicifyVariableName(baseType.Name);
                _types = types;
                _onSelected = onSelected;
                minimumSize = new Vector2(200f, 250f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(_title);
                root.AddChild(new TypeItem("None", null));
                foreach (var type in _types) root.AddChild(new TypeItem(ObjectNames.NicifyVariableName(type.Name), type));
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                _onSelected(((TypeItem)item).Type);
            }
        }

        private class TypeItem : AdvancedDropdownItem
        {
            public readonly Type Type;

            public TypeItem(string name, Type type) : base(name)
            {
                Type = type;
            }
        }
    }
}
