using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InspectorTools
{
    // Default inspector for every object. Adds:
    // - preset headers ([HeaderRequired] etc.) as colored foldouts that group the fields below them
    // - a warning icon on headers with warnIfMissing while a reference in their group is None
    // - a warning icon on [Required] fields while their reference is None, also shown on their header
    // - [ShowIf] / [HideIf] / [ReadOnly] on fields and [Button] on methods
    // Objects without any of these attributes are drawn with Unity's default inspector.
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Object), true)]
    public class EssentialsInspector : Editor
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
                                           BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private class Entry
        {
            public SerializedProperty Property;
            public GUIContent Label; // null keeps Unity's label
            public bool ReadOnly;
            public bool Required;
            public readonly List<(ShowIfAttribute Attribute, MemberInfo Member)> Conditions = new();
        }

        private class Group
        {
            public PresetHeaderAttribute Header; // null for the fields above the first header
            public readonly List<Entry> Entries = new();
        }

        private class ButtonEntry
        {
            public MethodInfo Method;
            public string Label;
            public ParameterInfo[] Parameters;
            public object[] Values; // current value typed into each parameter field
            public string UnsupportedReason;
        }

        private static readonly Dictionary<Color, GUIStyle> FoldoutStyles = new();

        private readonly List<Group> _groups = new();
        private readonly List<ButtonEntry> _buttons = new();
        private bool _useDefaultInspector;

        private void OnEnable()
        {
            if (target == null) return;

            BuildButtons();
            BuildGroups();
        }

        public override void OnInspectorGUI()
        {
            if (_useDefaultInspector) DrawDefaultInspector();
            else DrawGroups();
        }

        // Fields under headers with the same label are merged into one group, placed where that header first appears.
        // Buttons always go at the end of the Debug group, which is added at the bottom if no field uses [HeaderDebug].
        private void BuildGroups()
        {
            _groups.Clear();
            var current = new Group();
            _groups.Add(current);
            var groupsByLabel = new Dictionary<string, Group>();

            using (var iterator = serializedObject.GetIterator())
            {
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        var property = serializedObject.FindProperty(iterator.propertyPath);
                        var field = FindField(target.GetType(), property.name);
                        var header = field?.GetCustomAttribute<PresetHeaderAttribute>(true);

                        if (header != null && !groupsByLabel.TryGetValue(header.Label, out current))
                        {
                            current = new Group { Header = header };
                            groupsByLabel[header.Label] = current;
                            _groups.Add(current);
                        }

                        current.Entries.Add(CreateEntry(property, field));
                    }
                    while (iterator.NextVisible(false));
                }
            }

            if (_buttons.Count > 0 && !_groups.Any(g => g.Header is HeaderDebugAttribute))
                _groups.Add(new Group { Header = new HeaderDebugAttribute() });

            _useDefaultInspector = _buttons.Count == 0 &&
                                   _groups.All(g => g.Header == null && g.Entries.All(e => e.Conditions.Count == 0 && e.Label == null && !e.Required));
        }

        private Entry CreateEntry(SerializedProperty property, FieldInfo field)
        {
            var entry = new Entry
            {
                Property = property,
                ReadOnly = property.propertyPath == "m_Script" || field?.GetCustomAttribute<ReadOnlyAttribute>(true) != null
            };

            if (field == null) return entry;

            entry.Required = field.GetCustomAttribute<RequiredAttribute>(true) != null;

            var label = field.GetCustomAttribute<LabelAttribute>(true);
            if (label != null) entry.Label = new GUIContent(label.Label, property.tooltip);

            foreach (var condition in field.GetCustomAttributes<ShowIfAttribute>(true))
            {
                var member = FindMember(target.GetType(), condition.Condition);
                if (member == null)
                {
                    Debug.LogWarning($"[{condition.GetType().Name}] on '{field.Name}': no field, property or method named '{condition.Condition}' in {target.GetType().Name}.", target);
                    continue;
                }

                entry.Conditions.Add((condition, member));
            }

            return entry;
        }

        private void BuildButtons()
        {
            _buttons.Clear();
            var seen = new HashSet<MethodInfo>();

            for (var type = target.GetType(); type != null && type != typeof(MonoBehaviour) && type != typeof(ScriptableObject); type = type.BaseType)
            {
                foreach (var method in type.GetMethods(Flags))
                {
                    var button = method.GetCustomAttribute<ButtonAttribute>(true);
                    if (button == null || !seen.Add(method.GetBaseDefinition())) continue;

                    var parameters = method.GetParameters();
                    var unsupported = parameters.FirstOrDefault(p => !IsSupportedParameter(p.ParameterType));

                    _buttons.Add(new ButtonEntry
                    {
                        Method = method,
                        Label = button.Label ?? ObjectNames.NicifyVariableName(method.Name),
                        Parameters = parameters,
                        Values = parameters.Select(GetDefaultValue).ToArray(),
                        UnsupportedReason = unsupported == null ? null : $"Parameter '{unsupported.Name}' of type {unsupported.ParameterType.Name} can't be edited in the inspector"
                    });
                }
            }
        }

        private void DrawGroups()
        {
            serializedObject.UpdateIfRequiredOrScript();
            PresetHeaderDrawer.Suppressed = true;

            try
            {
                foreach (var group in _groups)
                {
                    var visible = group.Entries.Where(IsVisible).ToList();
                    var hasButtons = group.Header is HeaderDebugAttribute && _buttons.Count > 0;
                    if (visible.Count == 0 && !hasButtons) continue;

                    if (group.Header == null)
                    {
                        foreach (var entry in visible) DrawEntry(entry);
                        continue;
                    }

                    if (!DrawHeader(group.Header, visible)) continue;

                    EditorGUI.indentLevel++;
                    foreach (var entry in visible) DrawEntry(entry);
                    if (hasButtons) DrawButtons(visible.Count > 0);
                    EditorGUI.indentLevel--;
                }
            }
            finally
            {
                PresetHeaderDrawer.Suppressed = false;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawEntry(Entry entry)
        {
            using (new EditorGUI.DisabledScope(entry.ReadOnly))
            {
                if (entry.Label != null) EditorGUILayout.PropertyField(entry.Property, entry.Label, true);
                else EditorGUILayout.PropertyField(entry.Property, true);
            }

            if (!entry.Required) return;

            var missing = CountMissing(entry.Property);
            if (missing == 0) return;

            // Drawn at the right edge of the label, on the field's first line (the foldout line for arrays).
            var rect = GUILayoutUtility.GetLastRect();
            var iconRect = new Rect(rect.x + EditorGUIUtility.labelWidth - 18f, rect.y, 18f, EditorGUIUtility.singleLineHeight);
            DrawWarningIcon(iconRect, missing);
        }

        private static void DrawWarningIcon(Rect rect, int missing)
        {
            var icon = EditorGUIUtility.IconContent("console.warnicon.sml");
            var content = new GUIContent(icon.image, missing == 1 ? "1 reference is missing" : $"{missing} references are missing");
            GUI.Label(rect, content);
        }

        // Returns whether the group is expanded.
        private bool DrawHeader(PresetHeaderAttribute header, List<Entry> entries)
        {
            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
            var rect = EditorGUILayout.GetControlRect();

            var key = $"{nameof(EssentialsInspector)}.{target.GetType().FullName}.{header.Label}";
            var expanded = EditorPrefs.GetBool(key, true);
            var newExpanded = EditorGUI.Foldout(rect, expanded, header.Label, true, GetFoldoutStyle(header.Color));
            if (newExpanded != expanded) EditorPrefs.SetBool(key, newExpanded);

            var missing = entries.Where(e => header.WarnIfMissing || e.Required).Sum(e => CountMissing(e.Property));
            if (missing > 0) DrawWarningIcon(new Rect(rect.xMax - 18f, rect.y, 18f, rect.height), missing);

            return newExpanded;
        }

        private void DrawButtons(bool spaceAbove)
        {
            if (spaceAbove) EditorGUILayout.Space();

            // GUILayout buttons ignore indentLevel, so indent each row by hand and draw its fields unindented.
            var indent = EditorGUI.indentLevel;
            try
            {
                foreach (var button in _buttons)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(indent * 15f);
                        EditorGUI.indentLevel = 0;
                        DrawButtonRow(button);
                        EditorGUI.indentLevel = indent;
                    }
                }
            }
            finally
            {
                EditorGUI.indentLevel = indent;
            }
        }

        // [ Button ] Param [ value ] Param [ value ]
        private void DrawButtonRow(ButtonEntry button)
        {
            if (button.Parameters.Length == 0 || button.UnsupportedReason != null)
            {
                DrawButton(button);
                return;
            }

            var buttonWidth = Mathf.Max(80f, GUI.skin.button.CalcSize(new GUIContent(button.Label)).x + 12f);
            DrawButton(button, GUILayout.Width(buttonWidth));

            var labelWidth = EditorGUIUtility.labelWidth;
            for (var i = 0; i < button.Parameters.Length; i++)
            {
                var parameter = button.Parameters[i];
                var label = ObjectNames.NicifyVariableName(parameter.Name);
                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(new GUIContent(label)).x + 4f;
                button.Values[i] = DrawParameterField(label, parameter.ParameterType, button.Values[i]);
            }

            EditorGUIUtility.labelWidth = labelWidth;
        }

        private void DrawButton(ButtonEntry button, params GUILayoutOption[] options)
        {
            using (new EditorGUI.DisabledScope(button.UnsupportedReason != null))
            {
                if (!GUILayout.Button(new GUIContent(button.Label, button.UnsupportedReason), options)) return;
            }

            if (button.Method.IsStatic)
            {
                button.Method.Invoke(null, button.Values);
                return;
            }

            foreach (var t in targets)
            {
                Undo.RecordObject(t, button.Label);
                button.Method.Invoke(t, button.Values);

                if (!Application.isPlaying && t is Component component && component.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }
        }

        private static bool IsSupportedParameter(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double) ||
                   type == typeof(bool) || type == typeof(string) || type.IsEnum ||
                   type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) ||
                   type == typeof(Vector2Int) || type == typeof(Vector3Int) || type == typeof(Color) ||
                   typeof(Object).IsAssignableFrom(type);
        }

        private static object GetDefaultValue(ParameterInfo parameter)
        {
            var type = parameter.ParameterType;
            if (parameter.HasDefaultValue && parameter.DefaultValue != null) return parameter.DefaultValue;
            if (type == typeof(string)) return string.Empty;
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static object DrawParameterField(string label, Type type, object value)
        {
            if (type == typeof(int)) return EditorGUILayout.IntField(label, (int)value);
            if (type == typeof(long)) return EditorGUILayout.LongField(label, (long)value);
            if (type == typeof(float)) return EditorGUILayout.FloatField(label, (float)value);
            if (type == typeof(double)) return EditorGUILayout.DoubleField(label, (double)value);
            if (type == typeof(bool)) return EditorGUILayout.Toggle(label, (bool)value);
            if (type == typeof(string)) return EditorGUILayout.TextField(label, (string)value);
            if (type.IsEnum)
            {
                return type.IsDefined(typeof(FlagsAttribute), false)
                    ? EditorGUILayout.EnumFlagsField(label, (Enum)value)
                    : EditorGUILayout.EnumPopup(label, (Enum)value);
            }
            if (type == typeof(Vector2)) return EditorGUILayout.Vector2Field(label, (Vector2)value);
            if (type == typeof(Vector3)) return EditorGUILayout.Vector3Field(label, (Vector3)value);
            if (type == typeof(Vector4)) return EditorGUILayout.Vector4Field(label, (Vector4)value);
            if (type == typeof(Vector2Int)) return EditorGUILayout.Vector2IntField(label, (Vector2Int)value);
            if (type == typeof(Vector3Int)) return EditorGUILayout.Vector3IntField(label, (Vector3Int)value);
            if (type == typeof(Color)) return EditorGUILayout.ColorField(label, (Color)value);
            return EditorGUILayout.ObjectField(label, (Object)value, type, true);
        }

        private bool IsVisible(Entry entry)
        {
            foreach (var (attribute, member) in entry.Conditions)
            {
                var value = GetValue(member, target);
                var met = attribute.HasValue ? Equals(value, attribute.Value) : value is true;
                if (met == attribute.Inverted) return false;
            }

            return true;
        }

        private static int CountMissing(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference)
                return !property.hasMultipleDifferentValues && property.objectReferenceValue == null ? 1 : 0;

            if (!property.isArray || property.propertyType == SerializedPropertyType.String) return 0;

            var missing = 0;
            for (var i = 0; i < property.arraySize; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                if (element.propertyType == SerializedPropertyType.ObjectReference && element.objectReferenceValue == null) missing++;
            }

            return missing;
        }

        private static GUIStyle GetFoldoutStyle(Color color)
        {
            if (FoldoutStyles.TryGetValue(color, out var style)) return style;

            style = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            style.normal.textColor = style.onNormal.textColor = color;
            style.hover.textColor = style.onHover.textColor = color;
            style.focused.textColor = style.onFocused.textColor = color;
            style.active.textColor = style.onActive.textColor = color;
            FoldoutStyles[color] = style;
            return style;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, Flags);
                if (field != null) return field;
            }

            return null;
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, Flags);
                if (field != null) return field;

                var property = type.GetProperty(name, Flags);
                if (property != null && property.GetMethod != null && property.GetIndexParameters().Length == 0) return property;

                var method = type.GetMethod(name, Flags, null, Type.EmptyTypes, null);
                if (method != null) return method;
            }

            return null;
        }

        private static object GetValue(MemberInfo member, object target)
        {
            return member switch
            {
                FieldInfo field => field.GetValue(field.IsStatic ? null : target),
                PropertyInfo property => property.GetValue(property.GetMethod.IsStatic ? null : target),
                MethodInfo method => method.Invoke(method.IsStatic ? null : target, null),
                _ => null
            };
        }
    }
}
