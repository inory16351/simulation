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
            // 장비 아이콘은 작게 쓰니 256으로 줄인다
            ti.maxTextureSize = assetPath.StartsWith("Assets/Resources/Art/icon/items/") ? 256 : 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            // 붓 UI 그림(Tools/ui-frames.mjs가 Codex 그림에서 만든다)은 9-slice
            // 부드러운 그림자(Tools/ui-fx.mjs)는 가장자리 번짐 24px를 9-slice로
            if (assetPath == "Assets/Resources/Art/ui/soft_shadow.png") ti.spriteBorder = new UnityEngine.Vector4(24, 24, 24, 24);
            if (assetPath.StartsWith("Assets/Resources/Art/ui/brush_"))
            {
                float b = assetPath.EndsWith("brush_frame.png") ? 18 : assetPath.EndsWith("brush_fill.png") ? 8 : assetPath.EndsWith("brush_button.png") ? 16 : 12;
                ti.spriteBorder = new UnityEngine.Vector4(b, b, b, b);
                ti.textureCompression = TextureImporterCompression.Uncompressed;
            }
        }
    }
}
