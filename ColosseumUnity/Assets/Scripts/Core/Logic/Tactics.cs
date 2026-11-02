// 전투 교범: 검투사 개인 전술 (src/core/tactics.ts). defaultDoctrine은 GladiatorRules에 있다.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;

namespace Colosseum.Core
{
    public static class Tactics
    {
        public static readonly Dictionary<string, string> StanceNames = new()
        {
            ["assault"] = "맹공", ["balanced"] = "균형", ["bulwark"] = "철벽", ["harass"] = "견제", ["showman"] = "쇼맨십", ["blitz"] = "속전속결",
        };
        public static readonly Dictionary<string, string> StanceDesc = new()
        {
            ["assault"] = "쉴 새 없이 몰아친다. 방어는 뒷전",
            ["balanced"] = "상황을 보며 싸운다",
            ["bulwark"] = "막고 버티며 상대가 지치길 기다린다. 받는 피해 감소",
            ["harass"] = "거리를 지키며 치고 빠진다",
            ["showman"] = "관중을 홀린다. 미시오에서 목숨을 건질 보험",
            ["blitz"] = "비싼 기술을 초반에 몰아 쓴다",
        };
        public static readonly Dictionary<string, string> RangeNames = new()
        {
            ["style"] = "스타일 기본", ["close"] = "파고들기", ["mid"] = "중거리 유지", ["far"] = "멀리서 견제",
        };
        public static readonly Dictionary<string, string> FocusNames = new()
        {
            ["spread"] = "고르게", ["head"] = "머리", ["body"] = "몸통", ["legs"] = "다리", ["arm"] = "무기 든 팔",
        };
        public static readonly Dictionary<string, string> FocusDesc = new()
        {
            ["spread"] = "효과 없음",
            ["head"] = "명중 -15%, 치명타 +10%, 기절 확률",
            ["body"] = "명중 +10%, 안정적인 피해",
            ["legs"] = "명중 -5%, 맞히면 30% 확률로 다리 부상(민첩 저하)",
            ["arm"] = "명중 -10%, 맞히면 30% 확률로 팔 부상(피해 -25%)",
        };
        public static readonly Dictionary<string, string> CondNames = new()
        {
            ["myHpBelow"] = "내 HP가 N% 이하",
            ["oppHpBelow"] = "상대 HP가 N% 이하",
            ["staBelow"] = "내 스태미나가 N% 이하",
            ["oppDisabled"] = "상대가 묶이거나 넘어지면",
            ["roundAtLeast"] = "N라운드 이후",
        };
        public static readonly Dictionary<string, string> ActionNames = new()
        {
            ["stance"] = "자세 변경",
            ["skill"] = "기술 최우선",
            ["rest"] = "숨 고르기",
            ["close"] = "거리 좁히기",
        };

        public static string DescribeRule(DoctrineRule r, Func<string, string> skillName)
        {
            var c = r.cond;
            string cond =
                c.type == "myHpBelow" ? $"내 HP {StateUtil.Num(c.value)}% 이하"
                : c.type == "oppHpBelow" ? $"상대 HP {StateUtil.Num(c.value)}% 이하"
                : c.type == "staBelow" ? $"내 스태미나 {StateUtil.Num(c.value)}% 이하"
                : c.type == "oppDisabled" ? "상대가 묶이거나 넘어짐"
                : $"{StateUtil.Num(c.value)}라운드 이후";
            var a = r.action;
            string act =
                a.type == "stance" ? $"{(a.stance != null && StanceNames.TryGetValue(a.stance, out var sn) ? sn : "undefined")} 자세로"
                : a.type == "skill" ? $"{skillName(a.skill)} 최우선"
                : a.type == "rest" ? "숨 고르기"
                : "거리 좁히기";
            return $"{cond} → {act}";
        }

        /// <summary>스타일에 맞는 기본 교범</summary>
        public static Doctrine DefaultDoctrine(Gladiator g, double familiarity = 60) => GladiatorRules.DefaultDoctrine(g, familiarity);

        /// <summary>교범을 바꾸면 숙련도가 떨어진다</summary>
        public const double FamiliarityChangeCost = 30;
    }
}
