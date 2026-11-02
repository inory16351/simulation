// 그림 초상화: Portrait2.Layers가 정한 층을 Image로 겹쳐 그린다 (웹판 PaintedAvatar.tsx).
// 층은 하이어라키에 있는 비활성 LayerTemplate(Image)을 복제해서 만든다. 이 오브젝트의 RectTransform 크기에 512 셀을 맞춘다.
using System.Collections.Generic;
using Colosseum.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class PortraitView : MonoBehaviour
    {
        [SerializeField] Image layerTemplate;
        [SerializeField] string view = "front";
        [Tooltip("슬롯 바탕 빛. 나라 색을 입힙니다 (없어도 됩니다)")]
        [SerializeField] Image glow;

        /// <summary>웹판 HAIR_TINTS를 흉내 낸 값: 색, 밝기, 대비</summary>
        static readonly Dictionary<string, (Color tint, float bright, float contrast)> Tints = new()
        {
            ["black"] = (Color.white, 0.45f, 1f),
            ["dark"] = (new Color(0.85f, 0.7f, 0.55f), 0.55f, 1f),
            ["brown"] = (new Color(0.95f, 0.72f, 0.5f), 0.8f, 1f),
            ["auburn"] = (new Color(1f, 0.55f, 0.35f), 0.85f, 1f),
            ["blond"] = (new Color(1f, 0.82f, 0.55f), 1.35f, 1f),
            ["grey"] = (Color.white, 1.45f, 0.9f),
        };
        static readonly Dictionary<string, Material> materials = new();
        static readonly Dictionary<string, Sprite> sprites = new();

        readonly List<GameObject> spawned = new();

        Gladiator pending;

        public void Show(Gladiator g)
        {
            Clear();
            if (glow != null && ColorUtility.TryParseHtmlString(Data.Nation(g.nation).color, out var nc)) glow.color = new Color(nc.r, nc.g, nc.b, 0.9f);
            var rt = (RectTransform)transform;
            // 막 복제돼 레이아웃 전이면 크기가 잡힐 때 그린다
            if (rt.rect.width <= 0) { pending = g; return; }
            pending = null;
            var spec = Portrait2.Spec(g);
            if (spec == null) return;
            var layers = Portrait2.Layers(spec, view);
            if (layers == null) return;
            float scale = rt.rect.width / Portrait2.Cell;
            foreach (var l in layers)
            {
                var sprite = Load(l.src);
                if (sprite == null) continue;
                Transform parent = transform;
                if (l.clipBottom != null)
                {
                    // 얼굴 다시 그리기: 턱 줄 위만 보이게 잘라 그린다
                    var clip = new GameObject("Clip", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                    clip.SetParent(transform, false);
                    Place(clip, 0, 0, Portrait2.Cell, l.clipBottom.Value, scale);
                    spawned.Add(clip.gameObject);
                    parent = clip;
                }
                var img = Instantiate(layerTemplate, parent);
                img.gameObject.SetActive(true);
                img.sprite = sprite;
                img.material = l.tint != null ? TintMaterial(l.tint) : null;
                Place((RectTransform)img.transform, l.x, l.y, l.w, l.h, scale);
                if (parent == transform) spawned.Add(img.gameObject);
            }
        }

        void OnRectTransformDimensionsChange()
        {
            if (pending != null && ((RectTransform)transform).rect.width > 0) Show(pending);
        }

        public void Clear()
        {
            foreach (var go in spawned) Destroy(go);
            spawned.Clear();
        }

        /// <summary>셀 좌표(위가 0)를 부모 왼쪽 위 기준 RectTransform으로</summary>
        static void Place(RectTransform r, float x, float y, float w, float h, float scale)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x * scale, -y * scale);
            r.sizeDelta = new Vector2(w * scale, h * scale);
        }

        static Sprite Load(string src)
        {
            if (!sprites.TryGetValue(src, out var s)) sprites[src] = s = Resources.Load<Sprite>("Art/" + src);
            return s;
        }

        static Material TintMaterial(string tint)
        {
            if (materials.TryGetValue(tint, out var m)) return m;
            var (color, bright, contrast) = Tints[tint];
            m = new Material(Resources.Load<Shader>("Shaders/UIHairTint")) { name = "HairTint_" + tint };
            m.SetColor("_Tint", color);
            m.SetFloat("_Bright", bright);
            m.SetFloat("_Contrast", contrast);
            return materials[tint] = m;
        }
    }
}
