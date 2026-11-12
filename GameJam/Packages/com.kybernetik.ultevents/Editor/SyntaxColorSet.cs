// UltEvents // https://kybernetik.com.au/ultevents // Copyright 2021-2026 Kybernetik //

#if UNITY_EDITOR

using System.Text;
using UnityEditor;

namespace UltEvents.Editor
{
    /// <summary>[Editor-Only] A set of colors for syntax highlighting.</summary>
    public class SyntaxColorSet
    {
        /************************************************************************************************************************/

        public static SyntaxColorSet
            DarkMode = new()
            {
                MainType = "4CF",
                ParameterType = "3AF",
            },
            LightMode = new()
            {
                MainType = "06F",
                ParameterType = "08D",
            };

        /************************************************************************************************************************/

        public static SyntaxColorSet CurrentSkin
            => EditorGUIUtility.isProSkin
            ? DarkMode
            : LightMode;

        /************************************************************************************************************************/

        public string MainType;
        public string ParameterType;

        /************************************************************************************************************************/

        public void StartRichTextColor(StringBuilder text, string color)
            => text.Append("<color=#")
            .Append(color)
            .Append('>');

        public void FinishRichTextColor(StringBuilder text)
            => text.Append("</color>");

        /************************************************************************************************************************/
    }
}

#endif
