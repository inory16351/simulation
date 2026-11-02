// Assets/Resources/Art 아래 그림은 UI 스프라이트로 들여온다 (tools/export-unity-assets.mjs가 내보낸 PNG).
using UnityEditor;

namespace Colosseum.EditorTools
{
    public class ArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
