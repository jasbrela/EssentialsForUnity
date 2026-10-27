using System;
using UnityEngine;

// Draws a button at the bottom of the inspector that invokes this method.
// Parameters (numbers, bool, string, enums, vectors, Color, Unity objects) get a field above the button,
// starting at the parameter's default value if it has one.
[AttributeUsage(AttributeTargets.Method)]
public sealed class ButtonAttribute : Attribute
{
    public readonly string Label;

    public ButtonAttribute(string label = null)
    {
        Label = label;
    }
}

// Shows the field only while the condition (a bool field, property or parameterless method) is true.
// With a value, shows it while the condition member equals that value instead (e.g. an enum).
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class ShowIfAttribute : Attribute
{
    public readonly string Condition;
    public readonly object Value;
    public readonly bool HasValue;

    public virtual bool Inverted => false;

    public ShowIfAttribute(string condition)
    {
        Condition = condition;
    }

    public ShowIfAttribute(string condition, object value)
    {
        Condition = condition;
        Value = value;
        HasValue = true;
    }
}

// Same as ShowIf, but hides the field while the condition is met.
public sealed class HideIfAttribute : ShowIfAttribute
{
    public override bool Inverted => true;

    public HideIfAttribute(string condition) : base(condition) { }
    public HideIfAttribute(string condition, object value) : base(condition, value) { }
}

// Replaces the field's label in the inspector, e.g. [Label("Name")] on skillName.
[AttributeUsage(AttributeTargets.Field)]
public sealed class LabelAttribute : PropertyAttribute
{
    public readonly string Label;

    public LabelAttribute(string label)
    {
        Label = label;
    }
}

// For [SerializeReference] fields and lists: adds a dropdown to pick which subclass of the field's type to create.
// Only lists non-abstract [Serializable] classes with a parameterless constructor.
//   [SerializeReference, TypePicker] public List<SkillEffect> effects;
[AttributeUsage(AttributeTargets.Field)]
public sealed class TypePickerAttribute : PropertyAttribute { }

// Draws the field greyed out so it can be seen but not edited.
[AttributeUsage(AttributeTargets.Field)]
public sealed class ReadOnlyAttribute : PropertyAttribute { }
