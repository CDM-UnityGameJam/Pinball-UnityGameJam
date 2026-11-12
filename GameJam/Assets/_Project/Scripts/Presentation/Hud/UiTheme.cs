using UnityEngine;

namespace Pinball.Presentation
{
    // HUD colours that change at runtime (lucky gold, drag blue, danger red...). Colours of static text and
    // panels are set directly on the Canvas prefab.
    [CreateAssetMenu(fileName = "UiTheme", menuName = "Pinball/UI Theme", order = 20)]
    public sealed class UiTheme : ScriptableObject
    {
        public Color text = new Color32(232, 236, 242, 255);
        public Color dim = new Color32(151, 161, 178, 255);
        public Color green = new Color32(71, 214, 163, 255);
        public Color blue = new Color32(91, 141, 240, 255);
        public Color gold = new Color32(233, 185, 73, 255);
        public Color orange = new Color32(240, 162, 58, 255);
        public Color danger = new Color32(224, 80, 94, 255);
        public Color panelEdge = new Color32(43, 52, 72, 255);
        public Color panelFill = new Color32(17, 23, 37, 255);

        public static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
    }
}
