// UltEvents // https://kybernetik.com.au/ultevents // Copyright 2021-2026 Kybernetik //

using System;

namespace UltEvents
{
    /// <summary>Prevents the attributed member from being shown in the member selection menu.</summary>
    [AttributeUsage(
        AttributeTargets.Class |
        AttributeTargets.Struct |
        AttributeTargets.Method |
        AttributeTargets.Property |
        AttributeTargets.Field)]
    public sealed class HideAttribute : Attribute { }
}
