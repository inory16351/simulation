// 경기 장면 조립 (웹판 components/ArenaStage.tsx). 웹판 SVG의 바닥·원·고리를 PNG로 한 번 그려 두고,
// 말 둘과 핏자국·이펙트 원본을 하이어라키에 놓는다. 좌표는 웹판과 같은 400×160 세계(위가 0).
using System.IO;
using Colosseum.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class ArenaStageKit
    {
        const string Dir = "Assets/Resources/Art/ui/";
        const float W = ArenaStage.W, H = ArenaStage.H, CX = ArenaStage.CX, CY = ArenaStage.CY, R = ArenaStage.R;
        static readonly Color Ink = Hex(0x1a120e);

        public static ArenaStage Build(Transform parent, float height)
        {
            // 탑뷰 경기장 그림이 있으면 그것을 쓰고, 없으면 코드로 그린 바닥
            var crowd = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "arena_crowd.png") ?? Floor(true);
            var empty = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "arena_empty.png") ?? Floor(false);
            var circle = Disc();
            var ring = Ring();
            var tri = Triangle();

            var stage = Box("Stage", parent, Hex(0x1c1712)).Layout(minH: height, prefH: height);
            stage.gameObject.AddComponent<RectMask2D>();
            var world = Rect("World", stage.transform);
            world.anchorMin = world.anchorMax = world.pivot = new Vector2(0.5f, 0.5f);
            world.sizeDelta = new Vector2(W, H);

            var floor = Box("Floor", world, Color.white);
            floor.rectTransform.Stretch();
            floor.sprite = crowd;
            floor.raycastTarget = false;
            var blood = Rect("Blood", world).Stretch();
            var bloodTpl = Box("BloodTemplate", blood, new Color(1, 1, 1, 0.85f));
            bloodTpl.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/fx/blood_pool.png");
            bloodTpl.raycastTarget = false;
            var a = Token("TokenA", world, circle, ring, tri);
            var b = Token("TokenB", world, circle, ring, tri);
            var fx = Rect("Fx", world).Stretch();
            var fxTpl = Box("FxTemplate", fx, Color.white);
            fxTpl.raycastTarget = false;
            fxTpl.preserveAspect = true;
            var impact = Box("Impact", world, new Color(1, 0.96f, 0.86f, 0.55f));
            impact.rectTransform.Stretch();
            impact.raycastTarget = false;
            impact.gameObject.SetActive(false);

            var s = stage.gameObject.AddComponent<ArenaStage>();
            Set(s, "world", world);
            Set(s, "floor", floor);
            Set(s, "floorCrowd", crowd);
            Set(s, "floorEmpty", empty);
            Set(s, "tokenA", a);
            Set(s, "tokenB", b);
            Set(s, "bloodLayer", blood);
            Set(s, "bloodTemplate", bloodTpl);
            Set(s, "fxLayer", fx);
            Set(s, "fxTemplate", fxTpl);
            Set(s, "impactFrame", impact);
            bloodTpl.gameObject.SetActive(false);
            fxTpl.gameObject.SetActive(false);
            return s;
        }

        /// <summary>초상화 말 하나 (웹판 Token)</summary>
        static RectTransform Token(string name, Transform world, Sprite circle, Sprite ring, Sprite tri)
        {
            float ringR = R + 4;
            var root = Rect(name, world);
            root.anchorMin = root.anchorMax = new Vector2(0, 1);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            root.gameObject.AddComponent<CanvasGroup>();
            Img("Shadow", root, circle, new Color(0, 0, 0, 0.35f), new Vector2(2, -4), new Vector2((R + 3) * 2, (R + 1) * 2));
            var body = Rect("Body", root);
            body.sizeDelta = Vector2.zero;

            Img("Ink", body, circle, Ink, Vector2.zero, Vector2.one * (ringR + 1.5f) * 2);
            Img("Color", body, circle, Theme.Bronze, Vector2.zero, Vector2.one * (ringR - 2) * 2);
            var clip = Img("Clip", body, circle, Color.white, Vector2.zero, Vector2.one * (R - 1) * 2);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Img("Bg", clip.transform, circle, Hex(0x120d0a), Vector2.zero, Vector2.one * R * 2);
            var pr = Rect("Portrait", clip.transform);
            pr.sizeDelta = Vector2.one * (R + 3) * 2;
            pr.anchoredPosition = new Vector2(0, -2);
            var tpl = Rect("LayerTemplate", pr).gameObject.AddComponent<Image>();
            tpl.raycastTarget = false;
            tpl.gameObject.SetActive(false);
            var pv = pr.gameObject.AddComponent<PortraitView>();
            Set(pv, "layerTemplate", tpl);
            Img("Hurt", clip.transform, circle, new Color(1, 0.16f, 0.1f, 0), Vector2.zero, Vector2.one * R * 2);
            Img("Dim", clip.transform, circle, new Color32(0x1a, 0x0a, 0x0a, 0x73), Vector2.zero, Vector2.one * R * 2).gameObject.SetActive(false);

            float ringSize = ringR * 2 + 3.4f;
            Img("HpBg", body, ring, Hex(0x2a1d16), Vector2.zero, Vector2.one * ringSize);
            var hp = Img("Hp", body, ring, Hex(0x7fc463), Vector2.zero, Vector2.one * ringSize);
            hp.type = Image.Type.Filled;
            hp.fillMethod = Image.FillMethod.Radial360;
            hp.fillOrigin = (int)Image.Origin360.Top;
            hp.fillClockwise = true;

            // 바라보는 쪽: 화살촉과 막기 호는 Facing을 돌려서 맞춘다
            var facing = Rect("Facing", body);
            facing.sizeDelta = Vector2.zero;
            var arrow = Img("Arrow", facing, tri, Theme.Bronze, new Vector2(ringR + 5.5f, 0), new Vector2(8, 10));
            arrow.gameObject.AddComponent<Outline>().effectColor = Ink;
            var shield = Img("Shield", facing, ring, Hex(0xe8d7a8), Vector2.zero, Vector2.one * (ringR + 6) * 2);
            shield.type = Image.Type.Filled;
            shield.fillMethod = Image.FillMethod.Radial360;
            shield.fillOrigin = (int)Image.Origin360.Right;
            shield.fillClockwise = false;
            shield.fillAmount = 100f / 360f;
            shield.rectTransform.localEulerAngles = new Vector3(0, 0, -50);
            shield.gameObject.SetActive(false);

            var badge = Img("Badge", body, circle, Hex(0xb08a4a), new Vector2(R * 0.72f, -R * 0.72f), Vector2.one * 17);
            Img("In", badge.transform, circle, Hex(0x241912), Vector2.zero, Vector2.one * 14.4f);
            var glyph = Text("Glyph", badge.transform, "검", 8, Hex(0xd8dbe0), false, FontStyles.Bold, TextAlignmentOptions.Center);
            glyph.rectTransform.sizeDelta = Vector2.one * 16;
            glyph.textWrappingMode = TextWrappingModes.NoWrap;

            Img("Cast", body, ring, Hex(0x7ad0ff), Vector2.zero, Vector2.one * (ringR + 4) * 2).gameObject.SetActive(false);
            Img("Cheer", body, ring, Hex(0xf2c14e), Vector2.zero, Vector2.one * (ringR + 5) * 2).gameObject.SetActive(false);

            var dead = Rect("DeadX", body);
            dead.sizeDelta = Vector2.zero;
            Img("A", dead, null, new Color32(0x8a, 0x1c, 0x14, 0xd9), Vector2.zero, new Vector2(26, 4)).rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            Img("B", dead, null, new Color32(0x8a, 0x1c, 0x14, 0xd9), Vector2.zero, new Vector2(26, 4)).rectTransform.localEulerAngles = new Vector3(0, 0, -45);
            dead.gameObject.SetActive(false);

            var flag = Rect("Flag", body);
            flag.sizeDelta = Vector2.zero;
            flag.anchoredPosition = new Vector2(-R + 2, R + 2);
            Img("Pole", flag, null, Theme.Bronze, new Vector2(0, 7), new Vector2(1.8f, 14));
            Img("Cloth", flag, null, Hex(0xf4efe2), new Vector2(5, 11), new Vector2(10, 6));
            flag.gameObject.SetActive(false);

            return root;
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size)
        {
            var img = Box(name, parent, color);
            img.sprite = sprite;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return img;
        }

        // ───────── 그림 만들기 (한 번 만들고 다시 쓴다) ─────────

        static Sprite Disc() => Make("stage_circle.png", 128, 128, (x, y) => Mathf.Clamp01(64 - Mathf.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64))));

        /// <summary>두께가 반지름의 10%인 고리</summary>
        static Sprite Ring() => Make("stage_ring.png", 128, 128, (x, y) =>
        {
            float d = Mathf.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64));
            return Mathf.Clamp01(64 - d) * Mathf.Clamp01(d - 57.6f);
        });

        /// <summary>오른쪽을 가리키는 삼각형</summary>
        static Sprite Triangle() => Make("stage_tri.png", 64, 64, (x, y) =>
        {
            float t = x / 64f;
            float half = 32 * (1 - t);
            return Mathf.Clamp01(half - Mathf.Abs(y - 32) + 0.5f) * Mathf.Clamp01(x + 0.5f);
        });

        static Sprite Make(string file, int w, int h, System.Func<float, float, float> alpha)
        {
            string path = Dir + file;
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha(x + 0.5f, y + 0.5f)));
            return Save(tex, path);
        }

        /// <summary>위에서 본 원형 경기장 바닥 (웹판 Floor): 관중석 고리, 벽, 갈퀴 자국 난 모래, 출입문, 가장자리 어둡게</summary>
        static Sprite Floor(bool crowd)
        {
            string path = Dir + (crowd ? "stage_floor_crowd.png" : "stage_floor_empty.png");
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;
            const int S = 4;
            int tw = (int)W * S, th = (int)H * S;
            var px = new Color[tw * th];
            Color bg = crowd ? Hex(0x2a1f18) : Hex(0x1c1712);
            for (int py = 0; py < th; py++)
                for (int qx = 0; qx < tw; qx++)
                {
                    float x = (qx + 0.5f) / S, y = (th - py - 0.5f) / S;
                    var c = bg;
                    if (crowd) c = Over(c, Hex(0x3a2b20), Fill(x, y, 240, 116, S));
                    c = Over(c, Hex(0x5a4535), Fill(x, y, 194, 80, S));
                    c = Over(c, Ink, Stroke(x, y, 194, 80, 2, S));
                    float sand = Fill(x, y, 188, 76, S);
                    if (sand > 0)
                    {
                        float gx = (x - CX) / (0.6f * 376), gy = (y - (CY - 76 + 0.45f * 152)) / (0.6f * 152);
                        float t = Mathf.Sqrt(gx * gx + gy * gy);
                        var sc = t < 0.7f ? Color.Lerp(Hex(0xd9b383), Hex(0xc09060), t / 0.7f) : Color.Lerp(Hex(0xc09060), Hex(0x8e6640), Mathf.Clamp01((t - 0.7f) / 0.3f));
                        c = Over(c, sc, sand);
                        for (int i = 0; i < 9; i++) c = Over(c, Hex(0x6b4a2a), Stroke(x, y, 20 + i * 20, 8 + i * 8, 0.8f, S) * 0.18f * sand);
                    }
                    c = Over(c, Hex(0x3a2a1e), Stroke(x, y, 188, 76, 1.5f, S));
                    px[py * tw + qx] = c;
                }
            if (crowd)
            {
                // 관중: 관중석 고리를 따라 점을 뿌린다 (항상 같은 배치)
                uint[] tones = { 0x8a5a3a, 0x6e4a33, 0xa0703e, 0x5c3c2a, 0xb38452, 0x7a3b2e, 0x4d5a6e };
                for (int ringI = 0; ringI < 3; ringI++)
                {
                    float rx = 206 + ringI * 14, ry = 86 + ringI * 10;
                    int n = 70 + ringI * 8;
                    for (int i = 0; i < n; i++)
                    {
                        float t = (float)i / n * Mathf.PI * 2 + ringI * 0.13f;
                        Dot(px, tw, th, S, CX + Mathf.Cos(t) * rx, CY + Mathf.Sin(t) * ry, 3.4f, Hex(tones[(i * 7 + ringI * 3) % tones.Length]));
                    }
                }
            }
            // 양쪽 출입문
            Door(px, tw, th, S, 2);
            Door(px, tw, th, S, W - 16);
            // 가장자리 어둡게 (웹판 tdVignette)
            for (int py = 0; py < th; py++)
                for (int qx = 0; qx < tw; qx++)
                {
                    float x = (qx + 0.5f) / S, y = (th - py - 0.5f) / S;
                    float t = Mathf.Sqrt(Sq((x - 200) / 280) + Sq((y - 80) / 112));
                    float a = t < 0.6f ? 0 : Mathf.Lerp(0, 0.7f, Mathf.Clamp01((t - 0.6f) / 0.4f));
                    px[py * tw + qx] = Over(px[py * tw + qx], Hex(0x0a0610), a);
                }
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            return Save(tex, path);
        }

        static void Dot(Color[] px, int tw, int th, int S, float cx, float cy, float r, Color c)
        {
            for (int py = Mathf.Max(0, (int)((H - cy - r - 1) * S)); py < Mathf.Min(th, (int)((H - cy + r + 1) * S)); py++)
                for (int qx = Mathf.Max(0, (int)((cx - r - 1) * S)); qx < Mathf.Min(tw, (int)((cx + r + 1) * S)); qx++)
                {
                    float x = (qx + 0.5f) / S, y = (th - py - 0.5f) / S;
                    float cov = Mathf.Clamp01((r - Mathf.Sqrt(Sq(x - cx) + Sq(y - cy))) * S + 0.5f);
                    px[py * tw + qx] = Over(px[py * tw + qx], c, cov);
                }
        }

        static void Door(Color[] px, int tw, int th, int S, float x0)
        {
            float y0 = CY - 13;
            for (int py = 0; py < th; py++)
                for (int qx = 0; qx < tw; qx++)
                {
                    float x = (qx + 0.5f) / S, y = (th - py - 0.5f) / S;
                    if (x < x0 - 0.75f || x > x0 + 14.75f || y < y0 - 0.75f || y > y0 + 26.75f) continue;
                    bool inner = x > x0 + 0.75f && x < x0 + 13.25f && y > y0 + 0.75f && y < y0 + 25.25f;
                    px[py * tw + qx] = inner ? Ink : Hex(0x7a5c33);
                }
        }

        /// <summary>타원 안쪽 덮임 (가장자리 안티앨리어싱)</summary>
        static float Fill(float x, float y, float rx, float ry, int S)
        {
            float f = Mathf.Sqrt(Sq((x - CX) / rx) + Sq((y - CY) / ry));
            return Mathf.Clamp01((1 - f) * Mathf.Min(rx, ry) * S + 0.5f);
        }

        static float Stroke(float x, float y, float rx, float ry, float w, int S)
        {
            float f = Mathf.Sqrt(Sq((x - CX) / rx) + Sq((y - CY) / ry));
            float d = Mathf.Abs(f - 1) * Mathf.Min(rx, ry);
            return Mathf.Clamp01((w / 2 - d) * S + 0.5f);
        }

        static Color Over(Color under, Color c, float a) => a <= 0 ? under : Color.Lerp(under, new Color(c.r, c.g, c.b, 1), a);
        static float Sq(float v) => v * v;
        static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        static Sprite Save(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
