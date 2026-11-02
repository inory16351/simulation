// 씬 구성 도구. MCP(execute_code)나 메뉴로 불러 씬 오브젝트를 만든다.
// 복제해서 쓰는 원본(템플릿)은 하이어라키에 비활성으로 둔다.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Colosseum.EditorTools
{
    public static class SceneBuilder
    {
        static readonly Color Bg = new Color32(0x14, 0x11, 0x0f, 0xff);
        static readonly Color PanelColor = new Color32(0x1f, 0x1a, 0x16, 0xff);
        static readonly Color Parchment = new Color32(0xd9, 0xcb, 0xb0, 0xff);
        static readonly Color Bronze = new Color32(0xb0, 0x8a, 0x4f, 0xff);
        static Font sans, serif;

        [MenuItem("Colosseum/Build Duel Demo Scene")]
        public static string BuildDuel()
        {
            sans = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-VF.ttf");
            serif = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSerifKR-VF.ttf");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Bg;
            cam.orthographic = true;
            camGo.transform.position = new Vector3(0, 0, -10);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var screen = Rect("DuelScreen", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Label("Title", screen, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -90), new Vector2(-40, -24), serif, 40, Parchment, TextAnchor.MiddleLeft, "콜로세움 리그 — 대결 데모");

            var cardA = Panel("FighterA", screen, new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(40, -250), new Vector2(-10, -110));
            var infoA = Label("Info", cardA, Vector2.zero, Vector2.one, new Vector2(150, 10), new Vector2(-20, -10), sans, 24, Parchment, TextAnchor.MiddleLeft, "검투사 A");
            var portraitA = Portrait("Portrait", cardA, new Vector2(0, 0.5f), new Vector2(10, -60));
            var cardB = Panel("FighterB", screen, new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(10, -250), new Vector2(-40, -110));
            var infoB = Label("Info", cardB, Vector2.zero, Vector2.one, new Vector2(20, 10), new Vector2(-150, -10), sans, 24, Parchment, TextAnchor.MiddleRight, "검투사 B");
            var portraitB = Portrait("Portrait", cardB, new Vector2(1, 0.5f), new Vector2(-130, -60));

            // 중계 로그
            var log = Panel("Log", screen, Vector2.zero, Vector2.one, new Vector2(40, 120), new Vector2(-40, -270));
            var scroll = log.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            var viewport = Rect("Viewport", log, Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -16));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            // 복제용 원본 줄 (비활성)
            var line = Label("LineTemplate", content, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 30), sans, 22, Parchment, TextAnchor.UpperLeft, "중계 줄 원본");
            line.gameObject.SetActive(false);

            var result = Label("Result", screen, Vector2.zero, new Vector2(0.7f, 0), new Vector2(40, 30), new Vector2(0, 100), serif, 30, Bronze, TextAnchor.MiddleLeft, "");

            var btnRt = Panel("FightButton", screen, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-320, 30), new Vector2(-40, 100));
            var btnImg = btnRt.GetComponent<Image>();
            btnImg.color = new Color32(0x7a, 0x5c, 0x33, 0xff);
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            Label("Label", btnRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, serif, 28, new Color32(0xff, 0xf3, 0xdc, 0xff), TextAnchor.MiddleCenter, "새 대결");

            var demo = screen.gameObject.AddComponent<Colosseum.UI.DuelDemo>();
            var so = new SerializedObject(demo);
            so.FindProperty("fighterA").objectReferenceValue = infoA;
            so.FindProperty("fighterB").objectReferenceValue = infoB;
            so.FindProperty("portraitA").objectReferenceValue = portraitA;
            so.FindProperty("portraitB").objectReferenceValue = portraitB;
            so.FindProperty("result").objectReferenceValue = result;
            so.FindProperty("fightButton").objectReferenceValue = btn;
            so.FindProperty("logContent").objectReferenceValue = content;
            so.FindProperty("lineTemplate").objectReferenceValue = line;
            so.FindProperty("scroll").objectReferenceValue = scroll;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
            return $"ok fonts={(sans != null)}/{(serif != null)}";
        }

        static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
            return rt;
        }

        /// <summary>120×120 초상화 칸. 층 복제용 원본 Image(LayerTemplate)를 비활성으로 품는다</summary>
        static Colosseum.UI.PortraitView Portrait(string name, Transform parent, Vector2 anchor, Vector2 pos)
        {
            var rt = Rect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(120, 120);
            var tpl = Rect("LayerTemplate", rt, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            tpl.raycastTarget = false;
            tpl.gameObject.SetActive(false);
            var view = rt.gameObject.AddComponent<Colosseum.UI.PortraitView>();
            var so = new SerializedObject(view);
            so.FindProperty("layerTemplate").objectReferenceValue = tpl;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        static RectTransform Panel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var rt = Rect(name, parent, aMin, aMax, oMin, oMax);
            rt.gameObject.AddComponent<Image>().color = PanelColor;
            return rt;
        }

        static Text Label(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, Font f, int size, Color c, TextAnchor al, string txt)
        {
            var t = Rect(name, parent, aMin, aMax, oMin, oMax).gameObject.AddComponent<Text>();
            t.font = f;
            t.fontSize = size;
            t.color = c;
            t.alignment = al;
            t.text = txt;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
