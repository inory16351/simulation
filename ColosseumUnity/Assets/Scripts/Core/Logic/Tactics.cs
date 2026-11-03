// 전투 교범: 검투사 개인 전술 (src/core/tactics.ts). defaultDoctrine은 GladiatorRules에 있다.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

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
            ["assault"] = "쉴 새 없이 몰아칩니다. 방어는 뒷전",
            ["balanced"] = "상황을 보며 싸웁니다",
            ["bulwark"] = "막고 버티며 상대가 지치길 기다립니다. 받는 피해 감소",
            ["harass"] = "거리를 지키며 치고 빠집니다",
            ["showman"] = "관중을 홀립니다. 미시오에서 목숨을 건질 보험",
            ["blitz"] = "비싼 기술을 초반에 몰아 씁니다",
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

        // ── 단체 전술 (Unity판, 난전) ──

        public static readonly string[] Roles = { "front", "hunter", "guard", "harass" };
        public static readonly Dictionary<string, string> RoleNames = new() { ["front"] = "전열", ["hunter"] = "사냥꾼", ["guard"] = "호위", ["harass"] = "견제" };
        public static readonly Dictionary<string, string> RoleDesc = new()
        {
            ["front"] = "가장 가까운 적을 막아서며 버팁니다",
            ["hunter"] = "빈사·약한 적을 끝내러 갑니다",
            ["guard"] = "지킬 동료를 노리는 적을 칩니다",
            ["harass"] = "아무도 안 붙은 적, 그중 강한 자를 묶어 둡니다",
        };
        public static readonly string[] Policies = { "focus", "spread", "weak", "strong" };
        public static readonly Dictionary<string, string> PolicyNames = new() { ["focus"] = "집중 공격", ["spread"] = "분산", ["weak"] = "약한 적 먼저", ["strong"] = "강한 적 먼저" };
        public static readonly Dictionary<string, string> PolicyDesc = new()
        {
            ["focus"] = "여럿이 한 명씩 달려듭니다. 첫 목표는 상대 중 가장 강한 자입니다",
            ["spread"] = "일대일로 흩어져 붙습니다",
            ["weak"] = "HP가 낮은 적부터 노립니다",
            ["strong"] = "가장 위험한 적부터 묶습니다",
        };

        /// <summary>인원수에 맞는 전술 칸: 3명 이하는 "3", 그 이상은 "5"</summary>
        public static string FormatKey(int n) => n <= 3 ? "3" : "5";

        /// <summary>능력·스타일로 고른 자동 역할: 방패·막기형은 전열, 창·삼지창·투척은 견제, 마무리 본능이 높으면 사냥꾼</summary>
        public static string AutoRole(Gladiator g)
        {
            var style = Data.Style(g.style);
            string mainId = g.equipment.main ?? style?.loadout?.main, offId = g.equipment.off ?? style?.loadout?.off;
            string main = mainId != null ? Data.Weapon(mainId)?.@class : null;
            bool shield = offId != null && Data.Armor(offId)?.slot == "shield";
            double A(string k) => GladiatorRules.EffectiveAttr(g, k);
            if (shield || A("blk") + A("tou") >= 28) return "front";
            if (main is "spear" or "trident_net" or "throwing" or "focus") return "harass";
            return "hunter";
        }

        /// <summary>AI 감독 성향대로 짠 전술. 역할은 능력·스타일, 5명이면 가장 약한 동료에게 호위 하나</summary>
        public static TeamTactic AutoTeam(Ludus l, List<Gladiator> lineup)
        {
            var t = new TeamTactic
            {
                policy = l?.aiPersonality switch { "aggressive" => "weak", "grudge" => "strong", "developer" => "focus", _ => "spread" },
            };
            foreach (var g in lineup) t.roles[g.id] = AutoRole(g);
            if (lineup.Count >= 4)
            {
                var weakest = lineup.OrderBy(g => GladiatorRules.ComputeCA(g)).First();
                var bodyguard = lineup.Where(g => g != weakest && t.roles[g.id] == "front").OrderByDescending(g => GladiatorRules.ComputeCA(g)).FirstOrDefault();
                if (bodyguard != null) { t.roles[bodyguard.id] = "guard"; t.guardOf[bodyguard.id] = weakest.id; }
            }
            return t;
        }

        /// <summary>경기에 쓸 전술: 플레이어가 짠 기본값이 있으면 그것(빠진 역할은 자동), 없거나 AI면 자동</summary>
        public static TeamTactic TeamFor(Ludus l, List<Gladiator> lineup)
        {
            var auto = AutoTeam(l, lineup);
            if (l == null || !l.isPlayer || l.teamTactics == null || !l.teamTactics.TryGetValue(FormatKey(lineup.Count), out var saved) || saved == null) return auto;
            var t = new TeamTactic { policy = saved.policy ?? auto.policy, focusTarget = saved.focusTarget };
            foreach (var g in lineup)
            {
                t.roles[g.id] = saved.roles.TryGetValue(g.id, out var r) && Array.IndexOf(Roles, r) >= 0 ? r : auto.roles[g.id];
                if (saved.guardOf.TryGetValue(g.id, out var who)) t.guardOf[g.id] = who;
            }
            return t;
        }

        /// <summary>전략 화면용: 저장된 기본값 (없으면 만들어 둔다)</summary>
        public static TeamTactic SavedTeam(Ludus l, string format)
        {
            l.teamTactics ??= new Dictionary<string, TeamTactic>();
            // 처음 저장할 때는 자동 전술의 방침에서 시작한다 (역할 하나만 바꿔도 방침이 달라지지 않게)
            if (!l.teamTactics.TryGetValue(format, out var t) || t == null)
                l.teamTactics[format] = t = new TeamTactic { policy = AutoTeam(l, new List<Gladiator>()).policy };
            return t;
        }
    }
}
