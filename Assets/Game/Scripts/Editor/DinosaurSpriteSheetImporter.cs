using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MicroJam.Game.Editor
{
    /// <summary>
    /// Imports every dinosaur animation sheet as a regular 256 x 256 frame grid.
    /// AssetPostprocessor changes automatically make Unity reimport matching textures.
    /// </summary>
    public sealed class DinosaurSpriteSheetImporter : AssetPostprocessor
    {
        private const string AnimationRoot = "Assets/Game/Animations/Enemies/";
        private const int FrameSize = 256;
        private const string ImportSessionKey = "MicroJam.DinosaurSheets.256.v1";

        [InitializeOnLoadMethod]
        private static void ScheduleInitialImport()
        {
            if (SessionState.GetBool(ImportSessionKey, false)) return;
            SessionState.SetBool(ImportSessionKey, true);
            EditorApplication.delayCall += ReimportAll;
        }

        [MenuItem("Tools/MicroJam/Dinosaurs/Slice Animation Sheets")]
        public static void ReimportAll()
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { AnimationRoot.TrimEnd('/') });
            foreach (string guid in textureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsDinosaurSheet(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Sliced {textureGuids.Length} dinosaur animation sheets into {FrameSize} x {FrameSize} frames.");
        }

        private void OnPreprocessTexture()
        {
            if (!IsDinosaurSheet(assetPath)) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 8192;

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width <= 0 || height <= 0 || width % FrameSize != 0 || height % FrameSize != 0)
            {
                Debug.LogWarning($"Dinosaur sheet '{assetPath}' is not divisible into {FrameSize} x {FrameSize} frames.");
                return;
            }

            string animationName = Path.GetFileNameWithoutExtension(assetPath);
            int columns = width / FrameSize;
            int rows = height / FrameSize;
            List<SpriteMetaData> frames = new(columns * rows);

            // Unity sprite rects count Y from the bottom. Names count visually from the
            // top-left so selecting frames by name plays them in the authored order.
            int frameIndex = 0;
            for (int row = 0; row < rows; row++)
            {
                int y = height - (row + 1) * FrameSize;
                for (int column = 0; column < columns; column++)
                {
                    frames.Add(new SpriteMetaData
                    {
                        name = $"{animationName}_{frameIndex:00}",
                        rect = new Rect(column * FrameSize, y, FrameSize, FrameSize),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        border = Vector4.zero
                    });
                    frameIndex++;
                }
            }

#pragma warning disable CS0618
            importer.spritesheet = frames.ToArray();
#pragma warning restore CS0618
        }

        private static bool IsDinosaurSheet(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(AnimationRoot) ||
                !path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relative = path.Substring(AnimationRoot.Length);
            return relative.StartsWith("Green/") || relative.StartsWith("Red/") || relative.StartsWith("Yellow/");
        }
    }
}
