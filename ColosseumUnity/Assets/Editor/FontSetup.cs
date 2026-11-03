// 폰트 설정: TextMeshPro 기본 리소스를 들이고, 본문 Noto Sans KR / 제목 마루 부리(네이버, MaruBuri-Bold)로 동적 SDF 폰트를 만든다.
// 동적 아틀라스라 한글 전체를 미리 굽지 않아도 쓰는 글자만 채워진다. MCP(execute_code)나 메뉴로 부른다.
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Colosseum.EditorTools
{
    public static class FontSetup
    {
        public const string SansPath = "Assets/Resources/Fonts/NotoSansKR SDF.asset";
        /// <summary>제목·이름용 (UiKit.Serif). 2026-11-03 Noto Serif KR → 마루 부리</summary>
        public const string SerifPath = "Assets/Resources/Fonts/MaruBuri SDF.asset";

        [MenuItem("Colosseum/Setup Fonts")]
        public static string Run()
        {
            // TMP 기본 셰이더·설정 (없으면 패키지에서 들인다)
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                AssetDatabase.ImportPackage("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage", false);
                AssetDatabase.Refresh();
                return "TMP 기본 리소스를 들였다. 다시 실행해서 폰트를 만든다";
            }
            Directory.CreateDirectory("Assets/Resources/Fonts");
            var sans = Make("Assets/Fonts/NotoSansKR-VF.ttf", SansPath);
            var serif = Make("Assets/Fonts/MaruBuri-Bold.ttf", SerifPath);
            // 기본 폰트를 산세리프로
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultFontAsset").objectReferenceValue = sans;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
            return $"ok sans={sans != null} serif={serif != null}";
        }

        static TMP_FontAsset Make(string ttfPath, string outPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            var fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            fa.name = Path.GetFileNameWithoutExtension(outPath);
            AssetDatabase.CreateAsset(fa, outPath);
            // 아틀라스 텍스처와 머티리얼을 같은 에셋 안에 넣는다 (안 넣으면 저장 후 사라진다)
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            AssetDatabase.SaveAssets();
            return fa;
        }
    }
}
