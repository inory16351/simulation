// 웹판 components/common.tsx의 작은 조각들 (Stars, Bar)
using System;
using Colosseum.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public static class Widgets
    {
        /// <summary>★ 별점 글자 (반 개는 ☆)</summary>
        public static string Stars(double value)
        {
            double s = GladiatorRules.Stars(value);
            int full = (int)Math.Floor(s);
            return new string('★', full) + (s - full >= 0.5 ? "☆" : "");
        }

        /// <summary>범위 별점 (웹판 RangeStars): 추정이 넓으면 "★★~★★★"</summary>
        public static string RangeStars(double lo, double hi)
        {
            double St(double v) => Math.Max(0.5, Math.Min(5, JsMath.Round(v / 40 * 2) / 2));
            string Draw(double v) => new string('★', (int)Math.Floor(v)) + (v % 1 != 0 ? "☆" : "");
            double a = St(lo), b = St(hi);
            return b > a ? $"{Draw(a)}<color={Theme.Hex(Theme.TextFaint)}>~{Draw(b)}</color>" : Draw(a);
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
                (h.counter > 0 ? $"  역제안 {h.counter:N0} D" : "");
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
