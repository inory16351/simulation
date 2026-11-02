// 씬 빌더용 UI 조립 도구 (TextMeshPro). 웹판 styles.css 규칙을 따른다.
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Colosseum.UI;

namespace Colosseum.EditorTools
{
    public static class UiKit
    {
        public static TMP_FontAsset Sans => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetup.SansPath);
        public static TMP_FontAsset Serif => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetup.SerifPath);

        public static RectTransform Rect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>부모를 꽉 채운다 (안쪽 여백 l, t, r, b)</summary>
        public static RectTransform Stretch(this RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static RectTransform Anchor(this RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return rt;
        }

        public static T Layout<T>(this T c, float minW = -1, float minH = -1, float prefW = -1, float prefH = -1, float flexW = -1, float flexH = -1) where T : Component
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.minWidth = minW; le.minHeight = minH; le.preferredWidth = prefW; le.preferredHeight = prefH; le.flexibleWidth = flexW; le.flexibleHeight = flexH;
            return c;
        }

        public static Image Box(string name, Transform parent, Color c)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        /// <summary>테두리 있는 패널 (웹판 .panel: 배경 --panel, 1px --line)</summary>
        public static Image Panel(string name, Transform parent, Color? fill = null)
        {
            var frame = Box(name, parent, Theme.Line);
            var inner = Box("Fill", frame.transform, fill ?? Theme.Panel);
            ((RectTransform)inner.transform).Stretch(1, 1, 1, 1);
            inner.raycastTarget = false;
            return frame;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size = 16, Color? color = null, bool serif = false, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = serif ? Serif : Sans;
            t.fontSize = size;
            t.color = color ?? Theme.Text;
            t.fontStyle = style;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>웹판 .btn (primary면 횃불색 .continue)</summary>
        public static Button Button(string name, Transform parent, string label, bool primary = false, float size = 16)
        {
            var frame = Box(name, parent, primary ? Theme.Torch : Theme.Line);
            var fill = Box("Fill", frame.transform, primary ? Theme.ContinueBg : Theme.Panel2);
            ((RectTransform)fill.transform).Stretch(1, 1, 1, 1);
            fill.raycastTarget = false;
            var b = frame.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.disabledColor = new Color(1, 1, 1, 0.4f);
            b.colors = colors;
            var t = Text("Label", frame.transform, label, size, primary ? Theme.ContinueText : Theme.Parchment, false, primary ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Center);
            ((RectTransform)t.transform).Stretch(8, 0, 8, 0);
            return b;
        }

        public static Image Sprite(string name, Transform parent, string artPath, float w, float h)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Art/{artPath}.png");
            img.preserveAspect = true;
            img.raycastTarget = false;
            ((RectTransform)img.transform).sizeDelta = new Vector2(w, h);
            img.Layout(prefW: w, prefH: h, minW: w, minH: h);
            return img;
        }

        public static HorizontalLayoutGroup HRow(Transform t, float spacing, int l = 0, int r = 0, int top = 0, int bottom = 0, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = t.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.padding = new RectOffset(l, r, top, bottom); h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        public static VerticalLayoutGroup VCol(Transform t, float spacing, int l = 0, int r = 0, int top = 0, int bottom = 0)
        {
            var v = t.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = new RectOffset(l, r, top, bottom); v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        /// <summary>세로 스크롤 영역. 반환값은 내용(Content, 세로 레이아웃 + 높이 맞춤)</summary>
        public static RectTransform ScrollView(string name, Transform parent, out RectTransform root, float spacing = 0)
        {
            root = Rect(name, parent);
            var sr = root.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40;
            var vp = Rect("Viewport", root).Stretch();
            vp.gameObject.AddComponent<RectMask2D>();
            vp.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var content = Rect("Content", vp);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            VCol(content, spacing);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = vp;
            sr.content = content;
            return content;
        }



        /// <summary>제목 줄이 있는 패널 (웹판 .panel > h3 + .pad). 반환값은 내용 칸(세로 레이아웃)</summary>
        public static RectTransform TitledPanel(string name, Transform parent, string title, float spacing = 6)
        {
            var frame = Panel(name, parent);
            VCol(frame.transform, 0, 1, 1, 1, 1);
            frame.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var head = Box("Head", frame.transform, new Color(Theme.Bronze.r, Theme.Bronze.g, Theme.Bronze.b, 0.22f)).Layout(minH: 34, prefH: 34);
            var ht = Text("Text", head.transform, "◆  " + title, 15, Theme.Parchment, true, FontStyles.Bold);
            ((RectTransform)ht.transform).Stretch(12, 0, 12, 0);
            Box("Line", frame.transform, Theme.Line).Layout(minH: 1, prefH: 1);
            var pad = Rect("Pad", frame.transform);
            VCol(pad, spacing, 12, 12, 12, 12);
            return pad;
        }

        /// <summary>웹판 .bar: 어두운 홈에 채움(Fill). Widgets.SetBar로 채운다</summary>
        public static RectTransform Bar(string name, Transform parent, float w = 60, float h = 6)
        {
            var bg = Box(name, parent, new Color32(0x12, 0x0f, 0x0d, 0xff));
            var fill = Box("Fill", bg.transform, Theme.Good);
            var f = (RectTransform)fill.transform;
            f.anchorMin = Vector2.zero; f.anchorMax = new Vector2(0.5f, 1); f.offsetMin = f.offsetMax = Vector2.zero;
            bg.Layout(minW: w, prefW: w, minH: h, prefH: h);
            return (RectTransform)bg.transform;
        }

        const string GlowPath = "Assets/Resources/Art/ui/portrait_glow.png";

        /// <summary>초상화 슬롯 (웹판 .portrait): 청동 테두리, 어두운 바탕에 나라 색 빛, 칸 밖은 잘라낸다.
        /// 층 복제용 원본(LayerTemplate)을 비활성으로 품는다</summary>
        public static PortraitView Portrait(string name, Transform parent, float size)
        {
            var frame = Box(name, parent, Theme.Bronze);
            frame.Layout(minW: size, prefW: size, minH: size, prefH: size, flexW: 0);
            ((RectTransform)frame.transform).sizeDelta = new Vector2(size, size);
            var mask = Box("Mask", frame.transform, new Color32(0x12, 0x0d, 0x0a, 0xff));
            ((RectTransform)mask.transform).Stretch(2, 2, 2, 2);
            mask.gameObject.AddComponent<RectMask2D>();
            var glow = Box("Glow", mask.transform, Color.clear);
            glow.sprite = GlowSprite();
            glow.raycastTarget = false;
            ((RectTransform)glow.transform).Stretch();
            var rt = Rect("Portrait", mask.transform).Stretch();
            var tpl = Rect("LayerTemplate", rt).gameObject.AddComponent<Image>();
            tpl.raycastTarget = false;
            tpl.gameObject.SetActive(false);
            var view = rt.gameObject.AddComponent<PortraitView>();
            Set(view, "layerTemplate", tpl);
            Set(view, "glow", glow);
            return view;
        }

        /// <summary>흰 원형 빛 (웹판 radialGradient cx 45% cy 35% r 75%). 색은 Image.color로 입힌다</summary>
        static Sprite GlowSprite()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
            if (s != null) return s;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x / (N - 1f) - 0.45f, dy = (1 - y / (N - 1f)) - 0.35f;
                    float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 0.75f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, 1 - t));
                }
            System.IO.File.WriteAllBytes(GlowPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(GlowPath);
            return AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
        }

        /// <summary>웹판 select. 펼침 목록(Template)은 드롭다운 아래 하이어라키에 비활성으로 있다</summary>
        public static TMP_Dropdown Dropdown(string name, Transform parent, float w, float h = 30)
        {
            var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            var dd = go.GetComponent<TMP_Dropdown>();
            go.GetComponent<Image>().color = Theme.Panel2;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                t.font = Sans;
                t.fontSize = 14;
                t.color = Theme.Parchment;
            }
            // 화살표: 스프라이트가 없으니 글자로
            var arrow = go.transform.Find("Arrow");
            arrow.GetComponent<Image>().enabled = false;
            var at = Text("Glyph", arrow, "▼", 10, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.Center);
            ((RectTransform)at.transform).Stretch();
            var tpl = go.transform.Find("Template");
            tpl.GetComponent<Image>().color = Theme.Panel;
            var item = tpl.Find("Viewport/Content/Item");
            item.Find("Item Background").GetComponent<Image>().color = Theme.Panel2;
            item.Find("Item Checkmark").GetComponent<Image>().color = Theme.Torch;
            ((RectTransform)item.Find("Item Checkmark")).sizeDelta = new Vector2(6, 6);
            var sb = tpl.Find("Scrollbar");
            if (sb != null) { sb.GetComponent<Image>().color = Theme.Bg2; sb.Find("Sliding Area/Handle").GetComponent<Image>().color = Theme.Bronze; }
            dd.Layout(minW: w, prefW: w, minH: h, prefH: h, flexW: 0);
            return dd;
        }

        /// <summary>웹판 background: linear-gradient(덮개) + url(그림) center / cover.
        /// 부모 칸을 꽉 채우고(레이아웃 무시) 넘치는 부분은 잘라 낸다. 맨 뒤에 깔린다.
        /// fromColor~toColor: 덮개 그라디언트 (세로 위→아래, horizontal이면 왼쪽→가운데→오른쪽 대칭)</summary>
        public static void CoverBackground(Transform parent, string artPath, Color fromColor, Color toColor, float focusY = 0.5f, bool horizontal = false)
        {
            var holder = Rect("Cover", parent).Stretch();
            holder.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            holder.gameObject.AddComponent<RectMask2D>();
            holder.SetAsFirstSibling();
            var img = Rect("Image", holder).gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Art/{artPath}.png");
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, focusY);
            rt.pivot = new Vector2(0.5f, focusY);
            var fit = img.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            if (img.sprite != null) fit.aspectRatio = img.sprite.rect.width / img.sprite.rect.height;
            var overlay = Rect("Overlay", holder).Stretch().gameObject.AddComponent<RawImage>();
            overlay.texture = GradientTexture(fromColor, toColor, horizontal);
            overlay.raycastTarget = false;
        }

        static Texture2D GradientTexture(Color a, Color b, bool horizontal)
        {
            const int N = 64;
            var tex = horizontal ? new Texture2D(N, 1, TextureFormat.RGBA32, false) : new Texture2D(1, N, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0; i < N; i++)
            {
                float t = i / (N - 1f);
                if (horizontal) tex.SetPixel(i, 0, Color.Lerp(a, b, 1 - Mathf.Abs(t * 2 - 1) * 1.0f));   // 양끝 a, 가운데 b
                else tex.SetPixel(0, i, Color.Lerp(b, a, t));                                               // 텍스처 y는 아래→위
            }
            tex.Apply();
            // 씬에 저장되도록 에셋으로 남긴다
            string dir = "Assets/Resources/Art/ui/gen";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Resources/Art/ui", "gen");
            string path = $"{dir}/grad_{ColorUtility.ToHtmlStringRGBA(a)}_{ColorUtility.ToHtmlStringRGBA(b)}_{(horizontal ? "h" : "v")}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) { Object.DestroyImmediate(tex); return existing; }
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        /// <summary>화면 제목과 부제 (웹판 .screen-title / .screen-sub)</summary>
        public static void ScreenHeader(Transform parent, string title, string sub)
        {
            Text("Title", parent, title, 26, Theme.Parchment, true, FontStyles.Bold).Layout(prefH: 36);
            if (sub != null) Text("Sub", parent, sub, 15, Theme.TextDim).Layout(prefH: 26);
        }

        public static void Set(Object target, string prop, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(prop).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
