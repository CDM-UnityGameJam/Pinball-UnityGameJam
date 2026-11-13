using System.Collections.Generic;
using System.IO;
using Pinball.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Pinball.Editor
{
    // Imports the card faces in Art/Cards as UI sprites and assigns them to the elements by file name:
    // <Prefix>_<number>.png, with Fire = Spark, Thunder = Arc, Quake = Shockwave.
    // Pinball > Cards > Assign Card Faces. Add a new element's art with a new prefix in Prefixes.
    public static class CardArtSetup
    {
        const string Folder = "Assets/_Project/Art/Cards";
        const string Elements = "Assets/_Project/Data/Elements";

        static readonly Dictionary<string, string> Prefixes = new Dictionary<string, string>
        {
            { "Fire", "Spark" },
            { "Thunder", "Arc" },
            { "Quake", "Shockwave" },
        };

        [MenuItem("Pinball/Cards/Assign Card Faces")]
        public static void Assign()
        {
            foreach (var pair in Prefixes)
            {
                var element = AssetDatabase.LoadAssetAtPath<ElementDefinition>($"{Elements}/{pair.Value}.asset");
                if (!element) { Debug.LogWarning("Missing element " + pair.Value); continue; }
                var faces = new List<Sprite>();
                for (int n = 1; ; n++)
                {
                    string path = $"{Folder}/{pair.Key}_{n}.png";
                    if (!File.Exists(path)) break;
                    MakeSprite(path);
                    faces.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
                }
                element.cardFaces = faces.ToArray();
                EditorUtility.SetDirty(element);
                Debug.Log($"{element.name}: {faces.Count} card faces");
            }
            AssetDatabase.SaveAssets();
        }

        static void MakeSprite(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }
}
