// UltEvents // https://kybernetik.com.au/ultevents // Copyright 2021-2026 Kybernetik //

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;

namespace UltEvents.Editor
{
    /// <summary>[Editor-Only]
    /// Stored details about the contents of a type for the <see cref="MemberSelectionMenu"/>.
    /// </summary>
    public class MemberCache
    {
        /************************************************************************************************************************/
        #region Instances
        /************************************************************************************************************************/

        public readonly List<string> FavouritePaths = new();
        public readonly List<MemberInfo> Favourites = new();
        public readonly List<MemberInfo> Others = new();

        /************************************************************************************************************************/

        public MemberCache(int capacity)
        {
            Others.Capacity = capacity;
        }

        /************************************************************************************************************************/

        public void AddRange(MemberCache other)
        {
            FavouritePaths.AddRange(other.FavouritePaths);
            Favourites.AddRange(other.Favourites);
            Others.AddRange(other.Others);
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
        #region Static
        /************************************************************************************************************************/

        private static readonly Dictionary<BindingFlags, Dictionary<Type, MemberCache>>
            MemberCaches = new();

        private static readonly List<string>
            TemporarySortedFullNames = new();

        internal static void Clear()
            => MemberCaches.Clear();

        /************************************************************************************************************************/

        public static MemberCache Get(Type type, BindingFlags bindings)
        {
            // Get the cache for the specified bindings.
            if (!MemberCaches.TryGetValue(bindings, out var memberCache))
            {
                memberCache = new();
                MemberCaches.Add(bindings, memberCache);
            }

            // If the members for the specified type aren't cached for those bindings, gather and sort them.
            if (memberCache.TryGetValue(type, out var members))
                return members;

            var fields = type.GetFields(bindings);
            var properties = type.GetProperties(bindings);
            var methods = type.GetMethods(bindings);

            // When gathering static members, also include constructors.
            var isStatic = (bindings & BindingFlags.Static) == BindingFlags.Static;
            var constructors = isStatic ?
                type.GetConstructors((bindings & ~BindingFlags.Static) | BindingFlags.Instance) :
                null;

            var capacity = fields.Length + properties.Length + methods.Length;
            if (constructors != null)
                capacity += constructors.Length;

            members = new(capacity);
            members.Others.AddRange(fields);
            members.Others.AddRange(properties);
            if (constructors != null)
                members.Others.AddRange(constructors);
            members.Others.AddRange(methods);

            TemporarySortedFullNames.Clear();

            for (int i = members.Others.Count - 1; i >= 0; i--)
            {
                var member = members.Others[i];
                if (member.IsDefined(typeof(HideAttribute), false))
                {
                    members.Others.RemoveAt(i);
                }
                else if (member.IsDefined(typeof(FavouriteAttribute), false))
                {
                    members.Others.RemoveAt(i);

                    var favourite = member.GetCustomAttribute<FavouriteAttribute>(false);

                    var path = favourite.Group;
                    if (path != null && path[^1] != '/')
                        path += "/";

                    var fullPath = path + member.Name;

                    // Insert into the favourites list in alphabetical order.
                    var index = TemporarySortedFullNames.BinarySearch(fullPath);
                    if (index < 0)
                        index = ~index;

                    TemporarySortedFullNames.Insert(index, fullPath);

                    members.Favourites.Insert(index, member);

                    members.FavouritePaths.Insert(index, path);
                }
            }

            TemporarySortedFullNames.Clear();

            // If the bindings include static, add static members from each base type.
            if (isStatic && type.BaseType != null)
            {
                var baseMembers = Get(type.BaseType, bindings & ~BindingFlags.Instance);
                members.AddRange(baseMembers);
            }

            UltEventUtils.StableInsertionSort(members.Others, CompareMembers);

            memberCache.Add(type, members);

            return members;
        }

        /************************************************************************************************************************/

        private static int CompareMembers(MemberInfo a, MemberInfo b)
        {
            if (BoolPref.SubMenuForEachBaseType)
            {
                var result = CompareChildBeforeBase(a.DeclaringType, b.DeclaringType);
                if (result != 0)
                    return result;
            }

            // Fields.
            if (a is FieldInfo)
            {
                if (b is not FieldInfo)
                    return -1;
            }
            else
            {
                if (b is FieldInfo)
                    return 1;
            }

            // Properties.
            if (a is PropertyInfo)
            {
                if (b is not PropertyInfo)
                    return -1;
            }
            else
            {
                if (b is PropertyInfo)
                    return 1;
            }

            // Methods.

            // Non-Public Sub-Menu.
            if (BoolPref.GroupNonPublicMethods)
            {
                if (IsPublic(a))
                {
                    if (!IsPublic(b))
                        return -1;
                }
                else
                {
                    if (IsPublic(b))
                        return 1;
                }
            }

            // Compare names.
            return a.Name.CompareTo(b.Name);
        }

        /************************************************************************************************************************/

        private static int CompareChildBeforeBase(Type a, Type b)
        {
            if (a == b)
                return 0;

            while (true)
            {
                a = a.BaseType;

                if (a == null)
                    return 1;

                if (a == b)
                    return -1;
            }
        }

        /************************************************************************************************************************/

        private static readonly Dictionary<MemberInfo, bool>
            MemberToIsPublic = new();

        public static bool IsPublic(MemberInfo member)
        {
            if (MemberToIsPublic.TryGetValue(member, out var isPublic))
                return isPublic;

            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    isPublic = (member as FieldInfo).IsPublic;
                    break;

                case MemberTypes.Property:
                    isPublic =
                        (member as PropertyInfo).GetGetMethod() != null ||
                        (member as PropertyInfo).GetSetMethod() != null;
                    break;

                case MemberTypes.Constructor:
                case MemberTypes.Method:
                    isPublic = (member as MethodBase).IsPublic;
                    break;

                default:
                    throw new ArgumentException("Unhandled member type", "member");
            }

            MemberToIsPublic.Add(member, isPublic);

            return isPublic;
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
    }
}

#endif
