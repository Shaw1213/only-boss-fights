using UnityEditor;
using UnityEngine;

namespace DreamLayer.UnityTools
{
    /// <summary>
    /// Automatic import settings for DreamLayer images under the import root (Assets/Art/DreamLayer/ by default).
    ///
    /// First import only (so later Inspector changes stick): Sprite, alpha is transparency, no
    /// mipmaps, compressed, plus max size / pixels-per-unit / pivot from <see cref="DreamLayerImportRules"/>.
    /// Every import: warns when width or height isn't a multiple of 4 (WebGL's DXT/BC compression then
    /// silently falls back to uncompressed RGBA32) or when the image exceeds its max size.
    /// Sheets written by the ZIP importer (*_sheet.png) are set to Multiple and sliced there.
    /// </summary>
    public class DreamLayerTexturePostprocessor : AssetPostprocessor
    {
        public const string SheetSuffix = "_sheet.png";

        void OnPreprocessTexture()
        {
            if (!DreamLayerImportRules.IsManaged(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            if (!importer.importSettingsMissing) return;

            var rule = DreamLayerImportRules.For(assetPath);
            bool isSheet = assetPath.EndsWith(SheetSuffix);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = isSheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = rule.pixelsPerUnit;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear; // illustrated art, not pixel art
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = rule.maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;

            if (!isSheet)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = rule.pivot;
                importer.SetTextureSettings(settings);
            }
        }

        void OnPostprocessTexture(Texture2D texture)
        {
            if (!DreamLayerImportRules.IsManaged(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);

            if (w % 4 != 0 || h % 4 != 0)
                Debug.LogWarning($"[DreamLayer] {assetPath} is {w}x{h}. WebGL texture compression (DXT/BC) needs width and height " +
                                 "in multiples of 4, so this ships UNCOMPRESSED. Pad it (the package's Tools~/clean_cutout.py does) or pack it into a Sprite Atlas.", texture);

            if (w > importer.maxTextureSize || h > importer.maxTextureSize)
                Debug.Log($"[DreamLayer] {assetPath} is {w}x{h}, above its max size {importer.maxTextureSize}; Unity will downscale it. " +
                          "Pre-size the source for the sharpest result.", texture);
        }
    }
}
