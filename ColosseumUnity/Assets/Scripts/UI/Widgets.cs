// 웹판 components/common.tsx의 작은 조각들 (Stars, Bar)
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public static class Widgets
    {
        static readonly Dictionary<string, Sprite> crestCache = new();

        /// <summary>문장 그림 (team/문장 id 또는 나라 id). 한 번 읽은 그림은 캐시</summary>
        public static Sprite CrestSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!crestCache.TryGetValue(path, out var sp)) crestCache[path] = sp = Resources.Load<Sprite>("Art/crest/" + path);
            return sp;
        }

        /// <summary>글 칸 왼쪽에 루두스 문장을 붙인다 (씬 다시 조립 없이: 없으면 자식 Image를 만들고 글을 그만큼 민다)</summary>
        public static void SetInlineCrest(TMPro.TMP_Text label, Ludus l, float size = 22)
        {
            var t = label.transform.Find("Crest");
            Image img;
            if (t == null)
            {
                var go = new GameObject("Crest", typeof(RectTransform));
                go.transform.SetParent(label.transform, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = new Vector2(0, 0.5f);
                r.pivot = new Vector2(0, 0.5f);
                r.sizeDelta = new Vector2(size, size);
                r.anchoredPosition = Vector2.zero;
                img = go.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            else img = t.GetComponent<Image>();
            img.sprite = LudusCrest(l);
            img.enabled = img.sprite != null;
            var m = label.margin;
            label.margin = new Vector4(img.enabled ? size + 6 : 0, m.y, m.z, m.w);
        }

        /// <summary>루두스 문장. 고른 문장이 없으면(웹판 규칙·옛 저장) 나라 문장</summary>
        public static Sprite LudusCrest(Ludus l) => l == null ? null : (l.crest != null ? CrestSprite("team/" + l.crest) : null) ?? CrestSprite(l.nation);
        /// <summary>★ 별점 글자 (반 개는 ☆)</summary>
        public static string Stars(double value)
        {
            double s = StarScale.ForTeam(GameController.I?.State, value);
            int full = (int)Math.Floor(s);
            return TeamLink(new string('★', full) + (s - full >= 0.5 ? "☆" : ""));
        }

        /// <summary>FM식 별은 '우리 팀 수준 기준'이라 올리면 그 뜻을 알려 준다 (info:stars)</summary>
        static string TeamLink(string stars) => FmRules.Enabled && GameController.I?.State != null ? $"<link=\"info:stars\">{stars}</link>" : stars;

        /// <summary>FM식 교관 평가 별점: 우리 검투사는 독토레가 보는 대로, 남의 검투사는 정찰 범위로. CA 숫자는 보여 주지 않는다</summary>
        public static string CaStars(GameState s, Gladiator g)
        {
            var r = Scouting.CaRange(s, g);
            return RangeStars(r.lo, r.hi);
        }

        /// <summary>범위 별점 (웹판 RangeStars): 추정이 넓으면 "★★~★★★"</summary>
        public static string RangeStars(double lo, double hi)
        {
            // 정찰 오차는 절대 CA 구간을 먼저 구한 뒤(호출하는 쪽) 우리 팀 기준으로 바꾼다
            var st = GameController.I?.State;
            double St(double v) => StarScale.ForTeam(st, v);
            string Draw(double v) => new string('★', (int)Math.Floor(v)) + (v % 1 != 0 ? "☆" : "");
            double a = St(lo), b = St(hi);
            return TeamLink(b > a ? $"{Draw(a)}<color={Theme.Hex(Theme.TextFaint)}>~{Draw(b)}</color>" : Draw(a));
        }

        /// <summary>등급 색: 보통(양피지) · 정교한(초록) · 명품(파랑) · 전설(주황)</summary>
        public static readonly Color[] QualityColors =
        {
            new Color32(0xcf, 0xc2, 0xa8, 0xff), new Color32(0x8f, 0xd1, 0x7a, 0xff), new Color32(0x6f, 0xb6, 0xff, 0xff), new Color32(0xf2, 0xa3, 0x3a, 0xff),
        };
        static readonly string[] GradeNames = { "보통", "정교", "명품", "전설" };

        public static Color QualityColor(string id) => QualityColors[Math.Clamp(GearLogic.QualityOf(id), 0, 3)];

        /// <summary>장비 이름을 등급 색으로 (웹판 .item-name.q0~q3)</summary>
        public static string ItemLabel(string id)
        {
            int q = Math.Clamp(GearLogic.QualityOf(id), 0, 3);
            string name = GearLogic.ItemName(id);
            return $"<color={Theme.Hex(QualityColors[q])}>{(q >= 2 ? $"<b>{name}</b>" : name)}</color>";
        }

        /// <summary>장비 아이콘 (Resources/Art/icon/items/고유 id 또는 기본 장비 id). 고유 그림이 없으면 바탕 장비 그림</summary>
        public static Sprite ItemIcon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string b = Data.SplitItemId(id).Item1;
            string u = Data.UniqueOf(id);
            return (u != null ? Resources.Load<Sprite>("Art/icon/items/" + u) : null) ?? Resources.Load<Sprite>("Art/icon/items/" + b);
        }

        /// <summary>아이콘 칸 채우기: 그림이 없으면 숨긴다</summary>
        public static void SetIcon(Image img, string id)
        {
            if (img == null) return;
            var s = ItemIcon(id);
            img.sprite = s;
            img.enabled = s != null;
            img.preserveAspect = true;
        }

        /// <summary>빈 슬롯에 흐리게 보일 자리 그림 (부위별 기본 장비)</summary>
        static readonly System.Collections.Generic.Dictionary<string, string> SlotHints = new()
        {
            ["main"] = "gladius", ["off"] = "parmula", ["helmet"] = "open_helm", ["armor"] = "leather", ["arms"] = "leather_bracer", ["legs"] = "leather_greaves",
        };

        /// <summary>아이템 슬롯 채우기 (UiKit.ItemSlot: Rim·Icon). 장비가 없으면 부위 자리 그림을 흐리게, 부위도 모르면 빈 틀</summary>
        public static void SetSlot(Transform slot, string id, string slotKind = null)
        {
            if (slot == null) return;
            var icon = slot.Find("Icon")?.GetComponent<Image>();
            var rim = slot.Find("Rim")?.GetComponent<Image>();
            bool has = !string.IsNullOrEmpty(id);
            int q = has ? Math.Clamp(GearLogic.QualityOf(id), 0, 3) : 0;
            if (rim != null)
            {
                rim.enabled = q >= 1;
                rim.color = QualityColors[q];
            }
            if (icon == null) return;
            if (has) { SetIcon(icon, id); icon.color = Color.white; return; }
            SetIcon(icon, slotKind != null && SlotHints.TryGetValue(slotKind, out var hint) ? hint : null);
            icon.color = new Color(0.45f, 0.38f, 0.3f, 0.22f);
        }

        /// <summary>능력치 옆 성장 화살표: 최근 4주 안에 올랐으면 ▲, 내렸으면 ▼ (아니면 빈 글)</summary>
        public static string TrendArrow(GameState s, Gladiator g, string key)
        {
            int t = Training.TrendOf(g, key, Calendar.AbsDay(s.season, s.day));
            if (t == 0) return "";
            return $"<size=70%><color={Theme.Hex(t > 0 ? Theme.Good : new Color32(0xe0, 0x8b, 0x80, 0xff))}>{(t > 0 ? "▲" : "▼")}</color></size> ";
        }

        /// <summary>등급 딱지: 등급 색 글자</summary>
        public static string GradeTag(string id)
        {
            int q = Math.Clamp(GearLogic.QualityOf(id), 0, 3);
            return $"<color={Theme.Hex(QualityColors[q])}>{(q >= 1 ? "◆" : "◇")} {GradeNames[q]}</color>";
        }

        /// <summary>줄 바탕을 등급 색으로 옅게</summary>
        public static Color GradeTint(string id)
        {
            int q = Math.Clamp(GearLogic.QualityOf(id), 0, 3);
            var c = QualityColors[q];
            return q == 0 ? new Color(0, 0, 0, 0) : new Color(c.r, c.g, c.b, 0.04f + q * 0.03f);
        }

        /// <summary>흥정 상태 한 줄: 상대 인내심과 기분. 흥정이 없으면 null</summary>
        public static string HaggleLine(GameState s, string key)
        {
            var h = Haggling.Get(s, key);
            if (h == null) return null;
            if (Haggling.IsLocked(s, key)) return $"<color=#d9a29b>{Haggling.LockedLine(s, key)}</color>";
            double r = h.startPatience > 0 ? h.patience / h.startPatience : 0;
            var c = r > 0.65 ? Theme.Good : r > 0.35 ? Theme.Torch : Theme.Blood;
            int filled = (int)Math.Ceiling(r * 10);
            return $"인내심 <color={Theme.Hex(c)}>{new string('■', filled)}</color><color={Theme.Hex(Theme.TextFaint)}>{new string('□', 10 - filled)}</color>" +
                (h.counter > 0 ? $"  다시 부른 값 {h.counter:N0} D" : "");
        }

        /// <summary>영입 협상 한 줄: 흥정 상태 + 답 기다림 + 알려진 경쟁자. 아무것도 없으면 null</summary>
        public static string BidLine(GameState s, string key, PendingBid pending, string gladiatorId)
        {
            var parts = new List<string>();
            var h = HaggleLine(s, key);
            if (h != null) parts.Add(h);
            if (pending != null) parts.Add($"<color={Theme.Hex(Theme.Torch)}>{Negotiations.WaitLine(s, pending)}</color>");
            var rivals = Negotiations.RivalsOf(s, gladiatorId).Where(r => r.known).ToList();
            if (rivals.Count > 0) parts.Add($"<color=#d9a29b>경쟁: {string.Join(", ", rivals.Select(r => Tips.Ludus(s.ludi[r.ludusId])))}도 노리고 있습니다</color>");
            return parts.Count == 0 ? null : string.Join("\n", parts);
        }

        /// <summary>막대(Bar 오브젝트 아래 Fill)를 값에 맞춰 채운다</summary>
        public static void SetBar(RectTransform bar, double value, double max = 100, Color? color = null)
        {
            float pct = Mathf.Clamp01((float)(value / max));
            var fill = (RectTransform)bar.Find("Fill");
            fill.anchorMax = new Vector2(pct, 1);
            fill.GetComponent<Image>().color = color ?? (pct >= 0.7f ? Theme.Good : pct >= 0.4f ? Theme.BronzeHi : Theme.Blood);
        }
    }
}
