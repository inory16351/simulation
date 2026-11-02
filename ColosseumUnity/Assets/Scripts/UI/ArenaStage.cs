// 경기 장면 (웹판 components/ArenaStage.tsx): 위에서 내려다본 모래 마당에서 동그란 초상화 말 두 개가 전투 이벤트대로 움직인다.
// 좌표는 웹판 SVG와 같은 400×160 '세계'(Core Arena)를 쓰고, World를 늘려 경기장 전체가 칸에 들어오게 한다.
// 말의 자리는 전투 시뮬레이션이 낸 실제 좌표(CombatEvent.pos)다. 말은 경기장 폭의 약 8% (TokenScale).
// 말 둘과 핏자국·이펙트 원본은 하이어라키에 있고, 핏자국·이펙트는 원본을 복제한다.
using System.Collections.Generic;
using Colosseum.Core;
using Colosseum.Core.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class ArenaStage : MonoBehaviour
    {
        public const float W = 400, H = 160, CX = 200, CY = 82, R = 28;
        /// <summary>말 크기 배율 (말 도형은 반지름 R로 만들고 줄여서 쓴다)</summary>
        public const float TokenScale = 0.5f;
        static readonly Color Ink = new Color32(0x1a, 0x12, 0x0e, 0xff);

        [SerializeField] RectTransform world;
        [SerializeField] Image floor;
        [SerializeField] Sprite floorCrowd;
        [SerializeField] Sprite floorEmpty;
        [SerializeField] RectTransform tokenA;
        [SerializeField] RectTransform tokenB;
        [SerializeField] RectTransform bloodLayer;
        [SerializeField] Image bloodTemplate;
        [SerializeField] RectTransform fxLayer;
        [SerializeField] Image fxTemplate;
        [SerializeField] Image impactFrame;

        enum Pose { Idle, Attack, Hurt, Block, Dodge, Step, Cast, Stagger, Down, Dead, Kneel, Cheer }

        class View
        {
            public Gladiator g;
            public Pose pose;
            public int key = -1;
            public float hp = 1;
        }

        class Hit { public int side, key; public bool big, heavy, blunt; }

        /// <summary>말 하나의 화면 상태 (보간용)</summary>
        class TokenState
        {
            public RectTransform root, body, facing;
            public Image hp, hurt, dim, shield, cast, cheer;
            public GameObject arrow, deadX, flag;
            public Vector2 pos, from, to;
            public float moveT = 1, hpShown = 1, hpTarget = 1, poseT = 99;
            public Pose pose;
            public int key = -1;
            public Vector2 dir = Vector2.right;
            public string drawnFor;
        }

        readonly TokenState[] tokens = new TokenState[2];
        readonly List<Image> bloods = new();
        readonly List<(Image img, string anim, float t, float dur, float delay, Vector3 baseScale, float rot)> fx = new();
        Gladiator a, b;
        int lastBurstKey = -1, lastDodgeKey = -1, lastShakeKey = -1, lastSwingKey = -1;
        float shakeT = 99, impactT = 99;

        void Awake()
        {
            bloodTemplate.gameObject.SetActive(false);
            fxTemplate.gameObject.SetActive(false);
            tokens[0] = Bind(tokenA);
            tokens[1] = Bind(tokenB);
        }

        static TokenState Bind(RectTransform t)
        {
            var body = (RectTransform)t.Find("Body");
            var facing = (RectTransform)body.Find("Facing");
            return new TokenState
            {
                root = t, body = body, facing = facing,
                arrow = facing.Find("Arrow").gameObject,
                shield = facing.Find("Shield").GetComponent<Image>(),
                hp = body.Find("Hp").GetComponent<Image>(),
                hurt = body.Find("Clip/Hurt").GetComponent<Image>(),
                dim = body.Find("Clip/Dim").GetComponent<Image>(),
                cast = body.Find("Cast").GetComponent<Image>(),
                cheer = body.Find("Cheer").GetComponent<Image>(),
                deadX = body.Find("DeadX").gameObject,
                flag = body.Find("Flag").gameObject,
            };
        }

        /// <summary>새 경기: 말에 선수를 앉히고 장면을 비운다</summary>
        public void Begin(Gladiator ga, Gladiator gb, bool crowd)
        {
            if (tokens[0] == null) Awake();
            a = ga; b = gb;
            floor.sprite = crowd ? floorCrowd : floorEmpty;
            foreach (var x in bloods) Destroy(x.gameObject);
            bloods.Clear();
            foreach (var f in fx) Destroy(f.img.gameObject);
            fx.Clear();
            lastBurstKey = lastDodgeKey = lastShakeKey = lastSwingKey = -1;
            shakeT = impactT = 99;
            impactFrame.gameObject.SetActive(false);
            var p = new[] { new Vector2((float)Arena.Start[0], (float)Arena.Start[1]), new Vector2((float)Arena.Start[2], (float)Arena.Start[3]) };
            for (int s = 0; s < 2; s++)
            {
                var tk = tokens[s];
                var g = s == 0 ? a : b;
                tk.pos = tk.from = tk.to = p[s];
                tk.root.localScale = new Vector3(TokenScale, TokenScale, 1);
                tk.moveT = 1;
                tk.hpShown = tk.hpTarget = 1;
                tk.pose = Pose.Idle;
                tk.key = -1;
                tk.poseT = 99;
                tk.body.Find("Color").GetComponent<Image>().color = NationColor(g);
                tk.facing.Find("Arrow").GetComponent<Image>().color = NationColor(g);
                tk.body.Find("Badge/Glyph").GetComponent<TMP_Text>().text = WeaponGlyph(g);
                if (tk.drawnFor != g.id) { tk.drawnFor = g.id; tk.body.GetComponentInChildren<PortraitView>(true).Show(g); }
            }
            Show(new List<CombatEvent>(), 0);
        }

        /// <summary>앞에서부터 count개 이벤트까지 진행한 장면</summary>
        public void Show(List<CombatEvent> events, int count)
        {
            if (a == null) return;
            var d = Derive(events, count);
            var pos = d.pos;
            var u = (pos[1] - pos[0]).normalized;
            for (int s = 0; s < 2; s++)
            {
                var tk = tokens[s];
                var v = d.views[s];
                if (tk.to != pos[s]) { tk.from = tk.pos; tk.to = pos[s]; tk.moveT = 0; }
                tk.dir = s == 0 ? u : -u;
                tk.hpTarget = Mathf.Clamp01(v.hp);
                if (v.pose != tk.pose || v.key != tk.key) { tk.pose = v.pose; tk.key = v.key; tk.poseT = 0; }
            }
            // 쓰러진 쪽을 먼저 그려 선 쪽이 위에 오게
            bool bDown = d.views[1].pose is Pose.Down or Pose.Dead;
            (bDown ? tokenB : tokenA).SetSiblingIndex(fxLayer.GetSiblingIndex() - 2);
            (bDown ? tokenA : tokenB).SetSiblingIndex(fxLayer.GetSiblingIndex() - 1);

            while (bloods.Count < d.blood.Count)
            {
                int i = bloods.Count;
                var bp = d.blood[i];
                var img = Instantiate(bloodTemplate, bloodLayer);
                img.gameObject.SetActive(true);
                Put(img.rectTransform, new Vector2(bp.x, bp.y), new Vector2(bp.z * 4.4f, bp.z * 2.2f) * 0.6f, (i * 67) % 360);
                bloods.Add(img);
            }
            if (d.dodge.HasValue && d.dodge.Value.key != lastDodgeKey)
            {
                lastDodgeKey = d.dodge.Value.key;
                var p = pos[d.dodge.Value.side];
                Spawn("dust", p + new Vector2(0, 8 * TokenScale), 50, 0, "dust", 0.6f, 0);
            }
            // 무기 휘두르기: 공격한 쪽 말 앞에서 상대 쪽으로 호를 긋는다
            if (d.swing.HasValue && d.swing.Value.key != lastSwingKey)
            {
                lastSwingKey = d.swing.Value.key;
                int sw = d.swing.Value.side;
                var dirv = sw == 0 ? u : -u;
                float ang = Mathf.Atan2(dirv.y, dirv.x) * Mathf.Rad2Deg;
                Spawn("slash_gold", pos[sw] + dirv * R * TokenScale * 1.25f, 44, ang + 90, "swing", 0.3f, 0);
            }
            if (d.burst != null && d.burst.key != lastBurstKey)
            {
                lastBurstKey = d.burst.key;
                HitFx(pos[d.burst.side], d.burst, d.burst.side == 0 ? u : -u);
                if (d.burst.big) impactT = 0;
            }
            if (d.shake && d.burst != null && d.burst.key != lastShakeKey) { lastShakeKey = d.burst.key; shakeT = 0; }
        }

        void Update()
        {
            if (tokens[0] == null || a == null) return;
            float dt = Time.deltaTime;
            for (int s = 0; s < 2; s++) Animate(tokens[s], dt);

            // 흔들림 (웹판 shake 0.35s)
            shakeT += dt;
            float k = shakeT < 0.35f ? 1 - shakeT / 0.35f : 0;
            world.anchoredPosition = new Vector2(Mathf.Sin(shakeT * 90) * 5 * k, Mathf.Cos(shakeT * 70) * 3 * k);
            impactT += dt;
            impactFrame.gameObject.SetActive(impactT < 0.18f);
            if (impactT < 0.18f) impactFrame.color = new Color(1, 0.96f, 0.86f, impactT < 0.09f ? 0.55f : 0.25f);

            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i];
                f.t += dt;
                fx[i] = f;
                float p = (f.t - f.delay) / f.dur;
                if (p >= 1) { Destroy(f.img.gameObject); fx.RemoveAt(i); continue; }
                FxFrame(f.img, f.anim, Mathf.Max(0, p), p < 0, f.baseScale, f.rot);
            }
            Fit();
        }

        /// <summary>World(400×160)를 칸에 맞춰 늘린다: 경기장 전체가 보이게 (남는 쪽은 바탕색)</summary>
        void Fit()
        {
            var r = ((RectTransform)transform).rect;
            if (r.width <= 0) return;
            float k = Mathf.Min(r.width / W, r.height / H);
            world.localScale = new Vector3(k, k, 1);
        }

        void Animate(TokenState tk, float dt)
        {
            // 자리 이동: 0.5s, 끝으로 갈수록 느려진다
            tk.moveT = Mathf.Min(1, tk.moveT + dt / 0.5f);
            float e = 1 - Mathf.Pow(1 - tk.moveT, 3);
            tk.pos = Vector2.LerpUnclamped(tk.from, tk.to, e);
            tk.root.anchoredPosition = new Vector2(tk.pos.x, -tk.pos.y);

            tk.hpShown = Mathf.MoveTowards(tk.hpShown, tk.hpTarget, dt / 0.4f);
            tk.hp.fillAmount = tk.hpShown;
            tk.hp.color = tk.hpShown > 0.6f ? new Color32(0x7f, 0xc4, 0x63, 0xff) : tk.hpShown > 0.3f ? new Color32(0xe0, 0xb8, 0x4a, 0xff) : new Color32(0xd4, 0x48, 0x3a, 0xff);

            tk.poseT += dt;
            float t = tk.poseT;
            var fwd = new Vector2(tk.dir.x, -tk.dir.y);   // UI는 y가 위로
            var side = new Vector2(-fwd.y, fwd.x);
            Vector2 off = Vector2.zero;
            float scale = 1, rot = 0, flash = 0;
            bool down = tk.pose is Pose.Down or Pose.Dead;
            switch (tk.pose)
            {
                case Pose.Idle:
                case Pose.Step:
                    scale = 1 + 0.015f * (1 - Mathf.Cos(t / 1.8f * Mathf.PI * 2));
                    break;
                case Pose.Attack:
                    if (t < 0.45f)
                    {
                        float p = t / 0.45f;
                        // 30%: 살짝 물러섰다가 55%: 들이받고 100%: 제자리
                        off = p < 0.3f ? Vector2.Lerp(Vector2.zero, -fwd * 4.5f, p / 0.3f)
                            : p < 0.55f ? Vector2.Lerp(-fwd * 4.5f, fwd * 16, (p - 0.3f) / 0.25f)
                            : Vector2.Lerp(fwd * 16, Vector2.zero, (p - 0.55f) / 0.45f);
                        scale = p >= 0.3f && p < 1 ? 1 + 0.08f * (1 - Mathf.Abs(p - 0.55f) / 0.45f) : 1;
                    }
                    break;
                case Pose.Hurt:
                    if (t < 0.45f)
                    {
                        float p = t / 0.45f;
                        float w = p < 0.3f ? p / 0.3f : 1 - (p - 0.3f) / 0.7f;
                        off = -fwd * 9 * w;
                        rot = 8 * w;
                        flash = p < 0.2f ? 0.55f * p / 0.2f : 0.55f * (1 - (p - 0.2f) / 0.8f);
                    }
                    break;
                case Pose.Block:
                    if (t < 0.4f)
                    {
                        float p = t / 0.4f;
                        float w = p < 0.3f ? p / 0.3f : 1 - (p - 0.3f) / 0.7f;
                        off = -fwd * 3.6f * w;
                        scale = 1 - 0.04f * w;
                    }
                    break;
                case Pose.Dodge:
                    if (t < 0.5f)
                    {
                        float p = t / 0.5f;
                        float w = p < 0.4f ? p / 0.4f : 1 - (p - 0.4f) / 0.6f;
                        off = side * 14 * w;
                    }
                    break;
                case Pose.Cast:
                    if (t < 0.6f) scale = 1 + 0.03f * Mathf.Sin(t / 0.6f * Mathf.PI);
                    break;
                case Pose.Stagger:
                    if (t < 0.7f)
                    {
                        float p = t / 0.7f;
                        rot = p < 0.25f ? Mathf.Lerp(0, 14, p / 0.25f) : p < 0.5f ? Mathf.Lerp(14, -10, (p - 0.25f) / 0.25f)
                            : p < 0.75f ? Mathf.Lerp(-10, 5, (p - 0.5f) / 0.25f) : Mathf.Lerp(5, 0, (p - 0.75f) / 0.25f);
                    }
                    break;
                case Pose.Down:
                case Pose.Dead:
                    float q = Mathf.Min(1, t / 0.4f);
                    scale = Mathf.Lerp(1, 0.9f, q);
                    rot = Mathf.Lerp(0, 18, q);
                    break;
                case Pose.Kneel:
                    scale = 0.92f;
                    break;
                case Pose.Cheer:
                    scale = 1 + 0.05f * (1 - Mathf.Cos(t / 0.6f * Mathf.PI));
                    break;
            }
            tk.body.anchoredPosition = off;
            tk.body.localScale = new Vector3(scale, scale, 1);
            tk.body.localEulerAngles = new Vector3(0, 0, rot);
            tk.facing.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg - rot);
            tk.arrow.SetActive(!down);
            tk.hurt.color = new Color(1, 0.16f, 0.1f, flash);
            tk.dim.gameObject.SetActive(down);
            tk.deadX.SetActive(tk.pose == Pose.Dead);
            tk.flag.SetActive(tk.pose == Pose.Kneel);
            Fade(tk.shield, tk.pose == Pose.Block, t, 0.5f, false);
            Fade(tk.cast, tk.pose == Pose.Cast, t, 0.6f, false);
            Fade(tk.cheer, tk.pose == Pose.Cheer, t % 1f, 1f, true);
            var cg = tk.root.GetComponent<CanvasGroup>();
            if (cg) cg.alpha = tk.pose == Pose.Kneel ? 0.85f : 1;
        }

        /// <summary>잠깐 보였다 사라지는 고리. grow면 커지며 흐려진다(환호 고리)</summary>
        static void Fade(Image img, bool on, float t, float dur, bool grow)
        {
            img.gameObject.SetActive(on && t < dur);
            if (!img.gameObject.activeSelf) return;
            float p = t / dur;
            var c = img.color;
            c.a = grow ? 0.9f * (1 - p) : 1 - p;
            img.color = c;
            float s = grow ? 1 + 0.35f * p : 1;
            img.rectTransform.localScale = new Vector3(s, s, 1);
        }

        // ───────── 이펙트 ─────────

        void HitFx(Vector2 at, Hit hit, Vector2 from)
        {
            float k = hit.big ? 1.3f : hit.heavy ? 1.05f : 0.85f;
            float ang = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            var edge = at + from * R * TokenScale * 0.5f;
            var outer = at - from * R * TokenScale * 0.9f;
            if (hit.big) Spawn("crit_star", at, 60, 0, "star", 0.45f, 0);
            if (hit.blunt) Spawn("impact_blunt", edge, 46 * k, 0, "pop", 0.42f, 0);
            else Spawn(hit.heavy || hit.big ? "slash_blood" : "slash_gold", edge, 50 * k, ang + 90, "slash", 0.38f, 0);
            if (hit.heavy || hit.big) Spawn("blood_splash", outer, 48 * k, ang + 180, "spray", 0.55f, 0.04f);
            else Spawn("blood_drops", outer, 30, ang + 180, "spray", 0.55f, 0.04f);
            if (hit.big) Spawn("blood_gush", outer + new Vector2(0, 6 * TokenScale), 44, 0, "gush", 0.7f, 0.08f);
        }

        /// <summary>그림 이펙트 한 장. 각도는 웹판처럼 y가 아래로 가는 좌표 기준(시계 방향)</summary>
        void Spawn(string name, Vector2 at, float size, float rot, string anim, float dur, float delay)
        {
            var img = Instantiate(fxTemplate, fxLayer);
            img.gameObject.SetActive(true);
            img.sprite = Resources.Load<Sprite>("Art/fx/" + name);
            Put(img.rectTransform, at, new Vector2(size, size) * TokenScale * 1.2f, rot);
            img.color = new Color(1, 1, 1, 0);
            fx.Add((img, anim, 0, dur, delay, Vector3.one, -rot));
        }

        /// <summary>웹판 fx-* 키프레임 (끊어지는 움직임: steps)</summary>
        static void FxFrame(Image img, string anim, float p, bool waiting, Vector3 baseScale, float rot)
        {
            if (waiting) { img.color = new Color(1, 1, 1, 0); return; }
            int steps = anim is "spray" or "gush" ? 5 : 4;
            p = Mathf.Floor(p * steps) / steps;
            float alpha = 1, sx = 1, sy = 1, extraRot = 0;
            switch (anim)
            {
                case "pop": sx = sy = p < 0.5f ? Mathf.Lerp(0.4f, 1.05f, p / 0.5f) : Mathf.Lerp(1.05f, 1.2f, (p - 0.5f) / 0.5f); alpha = p < 0.5f ? 1 : 1 - (p - 0.5f) / 0.5f; break;
                case "slash": sx = p < 0.45f ? Mathf.Lerp(0.5f, 1.05f, p / 0.45f) : Mathf.Lerp(1.05f, 1.15f, (p - 0.45f) / 0.55f); sy = p < 0.45f ? Mathf.Lerp(0.8f, 1, p / 0.45f) : Mathf.Lerp(1, 0.9f, (p - 0.45f) / 0.55f); alpha = p < 0.45f ? 1 : 1 - (p - 0.45f) / 0.55f; break;
                case "spray": sx = sy = p < 0.4f ? Mathf.Lerp(0.3f, 1, p / 0.4f) : Mathf.Lerp(1, 1.2f, (p - 0.4f) / 0.6f); alpha = p < 0.4f ? 1 : 1 - (p - 0.4f) / 0.6f; break;
                case "gush": sx = p < 0.4f ? Mathf.Lerp(0.4f, 1, p / 0.4f) : Mathf.Lerp(1, 1.05f, (p - 0.4f) / 0.6f); sy = p < 0.4f ? Mathf.Lerp(0.2f, 1, p / 0.4f) : Mathf.Lerp(1, 1.15f, (p - 0.4f) / 0.6f); alpha = p < 0.4f ? 1 : 1 - (p - 0.4f) / 0.6f; break;
                case "star": sx = sy = p < 0.5f ? Mathf.Lerp(0.3f, 1.1f, p / 0.5f) : Mathf.Lerp(1.1f, 1.3f, (p - 0.5f) / 0.5f); extraRot = p < 0.5f ? 10 * p / 0.5f : Mathf.Lerp(10, 16, (p - 0.5f) / 0.5f); alpha = p < 0.5f ? 1 : 1 - (p - 0.5f) / 0.5f; break;
                case "dust": sx = sy = Mathf.Lerp(0.5f, 1.3f, p); alpha = 0.9f * (1 - p); break;
                case "swing": sx = Mathf.Lerp(0.6f, 1.1f, p); sy = 0.8f; extraRot = Mathf.Lerp(-40, 40, p); alpha = p < 0.6f ? 1 : 1 - (p - 0.6f) / 0.4f; break;
            }
            img.color = new Color(1, 1, 1, alpha);
            img.rectTransform.localScale = new Vector3(sx, sy, 1);
            img.rectTransform.localEulerAngles = new Vector3(0, 0, rot - extraRot);
        }

        /// <summary>세계 좌표(위가 0)의 가운데·크기·각도로 놓는다</summary>
        static void Put(RectTransform r, Vector2 center, Vector2 size, float rotDeg)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(center.x, -center.y);
            r.sizeDelta = size;
            r.localEulerAngles = new Vector3(0, 0, -rotDeg);
        }

        // ───────── 장면 계산 (웹판 derive) ─────────

        class Derived
        {
            /// <summary>지금 두 사람의 경기장 좌표 (시뮬레이션이 낸 값)</summary>
            public Vector2[] pos = { new((float)Arena.Start[0], (float)Arena.Start[1]), new((float)Arena.Start[2], (float)Arena.Start[3]) };
            public View[] views;
            /// <summary>마지막 사건이 공격이면 휘두른 쪽</summary>
            public (int side, int key)? swing;
            public List<Vector3> blood = new();
            public Hit burst;
            public (int side, int key)? dodge;
            public bool shake;
        }

        Derived Derive(List<CombatEvent> events, int count)
        {
            int Side(string id) => id == a.id ? 0 : 1;
            var d = new Derived { views = new[] { new View { g = a }, new View { g = b } } };
            var persistent = new Pose?[2];
            for (int idx = 0; idx < count; idx++)
            {
                var e = events[idx];
                bool last = idx == count - 1;
                if (e.pos != null)
                {
                    d.pos[0] = new Vector2((float)e.pos[0], (float)e.pos[1]);
                    d.pos[1] = new Vector2((float)e.pos[2], (float)e.pos[3]);
                }
                void Set(int s, Pose p)
                {
                    if (persistent[s] != null) return;
                    if (last) { d.views[s].pose = p; d.views[s].key = idx; }
                }
                switch (e.t)
                {
                    case "move":
                    {
                        Set(Side(e.actor), Pose.Step);
                        break;
                    }
                    case "attack":
                    {
                        int t = Side(e.target);
                        Set(Side(e.actor), e.magic ? Pose.Cast : Pose.Attack);
                        Set(t, !e.hit ? Pose.Dodge : e.blocked ? Pose.Block : Pose.Hurt);
                        if (last && !e.magic) d.swing = (Side(e.actor), idx);
                        if (e.hit) d.views[t].hp = (float)(e.hpAfter / e.maxHp);
                        if (e.hit && !e.blocked && e.damage > 0)
                        {
                            var p = d.pos[t];
                            if (e.damage / e.maxHp > 0.08 || e.crit)
                            {
                                float j = (idx * 53) % 360 * Mathf.Deg2Rad;
                                d.blood.Add(new Vector3(p.x + Mathf.Cos(j) * 7, p.y + Mathf.Sin(j) * 4, Mathf.Min(10, 4 + (float)(e.damage / e.maxHp) * 30)));
                            }
                            if (last)
                            {
                                d.burst = new Hit { side = t, big = e.crit, heavy = e.damage / e.maxHp > 0.12, blunt = Blunt(d.views[Side(e.actor)].g), key = idx };
                                d.shake = e.crit;
                            }
                        }
                        if (!e.hit && last) d.dodge = (t, idx);
                        break;
                    }
                    case "self":
                        Set(Side(e.actor), Pose.Cast);
                        break;
                    case "status":
                        if (e.status == "stun" || e.status == "prone") Set(Side(e.target), Pose.Stagger);
                        break;
                    case "poison":
                        d.views[Side(e.target)].hp = (float)(System.Math.Max(0, e.hpAfter) / System.Math.Max(1, e.hpAfter + e.damage));
                        break;
                    case "surrender":
                        persistent[Side(e.actor)] = Pose.Kneel;
                        break;
                    case "ko":
                        persistent[Side(e.target)] = Pose.Down;
                        d.views[Side(e.target)].hp = 0;
                        break;
                    case "verdict":
                        if (e.died)
                        {
                            persistent[Side(e.target)] = Pose.Dead;
                            var p = d.pos[Side(e.target)];
                            d.blood.Add(new Vector3(p.x, p.y + 2, 16));
                        }
                        break;
                    case "end":
                        if (persistent[Side(e.winner)] == null) persistent[Side(e.winner)] = Pose.Cheer;
                        break;
                }
            }
            for (int i = 0; i < 2; i++) if (persistent[i] is Pose p) d.views[i].pose = p;
            return d;
        }

        static bool Blunt(Gladiator g)
        {
            string cls = Data.Weapon(g.equipment.main)?.@class ?? "unarmed";
            return cls is "mace" or "unarmed" or "shield";
        }

        static Color NationColor(Gladiator g) => ColorUtility.TryParseHtmlString(Data.Nation(g.nation).color, out var c) ? c : Theme.Bronze;

        /// <summary>무기 표식 글자 (웹판은 작은 그림)</summary>
        static string WeaponGlyph(Gladiator g) => (Data.Weapon(g.equipment.main)?.@class ?? "unarmed") switch
        {
            "greatsword" => "대", "axe" => "도", "mace" => "둔", "spear" => "창", "trident_net" => "삼", "dagger" => "단",
            "focus" => "주", "unarmed" => "권", "shield" => "방", "throwing" => "투", _ => "검",
        };
    }
}
