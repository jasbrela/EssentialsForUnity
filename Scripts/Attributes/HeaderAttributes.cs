using System;
using UnityEngine;

// Base for all preset headers. Add new ones by subclassing and passing a label (and optionally a color).
// Each header is a foldout that groups every field below it, up to the next preset header.
// With warnIfMissing, the header shows a warning icon while any object reference in its group is None.
[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public abstract class PresetHeaderAttribute : PropertyAttribute
{
    public readonly string Label;
    public readonly Color Color;
    public readonly bool WarnIfMissing;

    protected PresetHeaderAttribute(string label) : this(label, Color.white) { }

    protected PresetHeaderAttribute(string label, Color color, bool warnIfMissing = false)
    {
        Label = label;
        Color = color;
        WarnIfMissing = warnIfMissing;
        order = -1; // draw above other decorators on the same field
    }
}

public static class PresetHeaderColors
{
    public static readonly Color Red  = new(1f, 0.42f, 0.42f);
    public static readonly Color Blue = new(0.42f, 0.66f, 1f);
}

public sealed class HeaderRequiredAttribute   : PresetHeaderAttribute { public HeaderRequiredAttribute()   : base("Required", PresetHeaderColors.Red, warnIfMissing: true) { } }
public sealed class HeaderOptionalAttribute   : PresetHeaderAttribute { public HeaderOptionalAttribute()   : base("Optional", PresetHeaderColors.Blue) { } }
public sealed class HeaderSettingsAttribute   : PresetHeaderAttribute { public HeaderSettingsAttribute()   : base("Settings") { } }
public sealed class HeaderDebugAttribute      : PresetHeaderAttribute { public HeaderDebugAttribute()      : base("Debug") { } }
