// UltEvents // https://kybernetik.com.au/ultevents // Copyright 2021-2026 Kybernetik //

using System;

namespace UltEvents
{
    /// <summary>Causes the attributed member to be displayed in a Favourites group in the member selection menu.</summary>
    /// <remarks>
    /// If the <see cref="Group"/> is empty, it will be displayed at the root level.
    /// <para></para>
    /// Favourites from a base class are displayed in the Favourites group of any derived classes as well.
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Method |
        AttributeTargets.Property |
        AttributeTargets.Field)]
    public sealed class FavouriteAttribute : Attribute
    {
        /************************************************************************************************************************/

        public readonly string Group;

        /************************************************************************************************************************/

        /// <summary>Causes the attributed member to be displayed more prominently in the member selection menu.</summary>
        /// <remarks>If the `group` is empty, it will be displayed at the root level</remarks>
        public FavouriteAttribute(string group = null)
        {
            Group = group;
        }

        /************************************************************************************************************************/
    }
}
