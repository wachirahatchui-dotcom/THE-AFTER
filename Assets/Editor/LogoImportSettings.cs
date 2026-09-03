using UnityEditor;
using UnityEngine;

// Import settings for the menu logo, applied automatically.
//
// MenuLogo reads the pixels of the file to turn a white background into
// transparency, and Unity refuses to hand over the pixels of a texture that is
// not marked readable. Rather than leaving that as a step someone has to
// remember, anything dropped into Assets/Resources/Logo/ is configured on
// import: readable, uncompressed, full alpha.
public class LogoImportSettings : AssetPostprocessor
{
    const string LogoFolder = "Assets/Resources/Logo/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(LogoFolder)) return;

        var importer = (TextureImporter)assetImporter;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;                 // MenuLogo needs GetPixels
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;

        // A title logo is large on screen; block compression on flat line art
        // shows as haloing around every stroke.
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
    }
}
