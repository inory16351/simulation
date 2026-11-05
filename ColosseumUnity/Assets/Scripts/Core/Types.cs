// 게임 상태 타입 (src/core/types.ts, tactics.ts). 필드 이름은 웹판 저장 JSON과 같게 둔다.
using System.Collections.Generic;

namespace Colosseum.Core
{
    public static class Attrs
    {
        public static readonly string[] Physical = { "str", "agi", "end", "tou", "rea", "rec" };
        public static readonly string[] Technical = { "acc", "eva", "blk", "grp", "mag" };
        public static readonly string[] Mental = { "grit", "comp", "dec", "show", "agg", "disc", "lead" };
        /// <summary>전투 감각: 수 싸움(예측·기만·변칙)과 단체전 감각 (docs/15_combat_v2.md)</summary>
        public static readonly string[] Sense = { "read", "feint", "genius", "adapt", "team", "timing", "kill", "aura" };
        public static readonly string[] All;

        public static readonly Dictionary<string, string> Names = new()
        {
            ["str"] = "근력", ["agi"] = "민첩", ["end"] = "지구력", ["tou"] = "강인함", ["rea"] = "반응", ["rec"] = "회복력",
            ["acc"] = "정확도", ["eva"] = "회피", ["blk"] = "막기", ["grp"] = "제압", ["mag"] = "마력",
            ["grit"] = "투지", ["comp"] = "침착", ["dec"] = "판단력", ["show"] = "쇼맨십", ["agg"] = "공격성", ["disc"] = "규율", ["lead"] = "리더십",
            ["read"] = "예측력", ["feint"] = "기만", ["genius"] = "천재성", ["adapt"] = "적응력", ["team"] = "팀워크", ["timing"] = "타이밍", ["kill"] = "마무리 본능", ["aura"] = "위압감",
        };

        public static readonly string[] WeaponClasses = { "sword", "greatsword", "axe", "mace", "spear", "trident_net", "dagger", "throwing", "shield", "unarmed", "focus" };

        public static readonly Dictionary<string, string> WeaponClassNames = new()
        {
            ["sword"] = "한손검", ["greatsword"] = "대검", ["axe"] = "도끼", ["mace"] = "둔기", ["spear"] = "창", ["trident_net"] = "삼지창·그물",
            ["dagger"] = "단검", ["throwing"] = "투척", ["shield"] = "방패", ["unarmed"] = "맨손", ["focus"] = "주술 촉매",
        };

        static Attrs()
        {
            var l = new List<string>();
            l.AddRange(Physical); l.AddRange(Technical); l.AddRange(Mental); l.AddRange(Sense);
            All = l.ToArray();
        }
    }

    public class Hidden { public int pa, consistency, bigStage, injuryProne, loyalty, ambition, greed; }

    public class Injury
    {
        /// <summary>"light" | "serious"</summary>
        public string severity;
        public double daysLeft;
        public string desc;
        public string type;
        /// <summary>"minor" | "moderate" | "severe"</summary>
        public string tier;
        public double? total;
    }

    public class Skills { public List<string> active = new(), passive = new(); }
    public class Equipment
    {
        public string main, off, armor, helmet;
        /// <summary>Unity판 전용 칸: 팔(팔보호대·팔갑), 다리(각반·정강이받이)</summary>
        public string arms, legs;

        /// <summary>칸 순서 (화면·전투 공용)</summary>
        public static readonly string[] Slots = { "main", "off", "helmet", "armor", "arms", "legs" };

        public string Get(string slot) => slot switch
        {
            "main" => main, "off" => off, "armor" => armor, "helmet" => helmet, "arms" => arms, "legs" => legs, _ => null,
        };

        public void Set(string slot, string v)
        {
            switch (slot)
            {
                case "main": main = v; break;
                case "off": off = v; break;
                case "armor": armor = v; break;
                case "helmet": helmet = v; break;
                case "arms": arms = v; break;
                case "legs": legs = v; break;
            }
        }

        /// <summary>낀 장비 전부 (빈 칸 빼고)</summary>
        public IEnumerable<string> Items()
        {
            foreach (var s in Slots) { var v = Get(s); if (!string.IsNullOrEmpty(v)) yield return v; }
        }
    }
    public class Record { public int wins, losses, kills; }
    public class TrainingPlan { public string focus = "physical", intensity = "normal"; }

    public class RuleCond { public string type; public double value; }
    public class RuleAction { public string type, stance, skill; }
    public class DoctrineRule { public RuleCond cond; public RuleAction action; }

    public class Doctrine
    {
        /// <summary>assault | balanced | bulwark | harass | showman | blitz</summary>
        public string stance = "balanced";
        /// <summary>style | close | mid | far</summary>
        public string range = "style";
        /// <summary>spread | head | body | legs | arm</summary>
        public string focus = "spread";
        public List<string> order = new();
        public List<DoctrineRule> rules = new();
        public bool allowSurrender = true;
        /// <summary>노릴 높이 null(자동) | high | mid | low (Unity판, auto = 상대의 약한 방어 지대)</summary>
        public string atkZone;
        /// <summary>막을 높이 null(자동) | high | mid | low (auto = 상대 무기가 강한 지대)</summary>
        public string guardZone;
        public double familiarity;
    }

    /// <summary>단체 전술 (Unity판): 3:3·5:5 난전에서 누구를 노리고 어디 설지. 자세·부위는 개인 교범 그대로</summary>
    public class TeamTactic
    {
        /// <summary>focus | spread | weak | strong</summary>
        public string policy = "focus";
        /// <summary>집중 공격의 첫 목표 (상대 검투사 id). null이면 상대 중 가장 강한 자</summary>
        public string focusTarget;
        /// <summary>검투사 id → front | hunter | guard | harass. 없으면 자동</summary>
        public Dictionary<string, string> roles = new();
        /// <summary>호위가 지킬 우리 편 id. 없으면 가장 약한 동료</summary>
        public Dictionary<string, string> guardOf = new();
    }

    public class Gladiator
    {
        public string id, name, nickname, gender, race, nation, status, style;
        public double age;
        public Dictionary<string, int> attrs = new();
        public Dictionary<string, int> prof = new();
        public Hidden hidden = new();
        public List<string> traits = new();
        public Skills skills = new();
        public Equipment equipment = new();
        public double wage, unpaidWeeks, morale, popularity, fatigue, sharpness, wear;
        public Injury injury;
        public Record record = new();
        public bool alive = true;
        public Doctrine doctrine;
        public int portraitSeed;
        public Dictionary<string, string> portrait;
        public TrainingPlan training = new();
        public Dictionary<string, double> growth = new();
        /// <summary>Unity판: 능력치가 최근에 움직인 날 (절대 일수, 내리면 음수). 화면의 ▲▼ 표시용</summary>
        public Dictionary<string, double> attrTrend;
        public double? lastFightAt;
        public DiedAt diedAt;
        public GladiatorContract contract;
        public Deployment deployment;
        public List<SeasonRecord> history = new();
        public List<CareerEvent> career = new();
        /// <summary>Unity판: 불만 (Logic/Grievances.cs). 사라지는 날(절대 일수)까지 사기가 조금씩 깎인다</summary>
        public string grievance;
        public double? grievanceUntil;
        /// <summary>불만 종류: raise(인상 요구 거절·무시 → 재계약하면 풀림) | transfer(이적이 막힘 → 이적 명단·이적·시간) | bench(출전 못 함 → 꾸준히 출전해야 풀림)</summary>
        public string grievanceKind, grievanceLudus;
        public double? grievanceAt;
        /// <summary>Unity판: 마지막으로 소속을 옮긴(또는 이적이 끝난) 이적 시장 번호 (Transfers.WindowKey). 한 기간에 한 번만 옮긴다</summary>
        public int? transferWindow;
        /// <summary>Unity판 FM 세계: 무소속이 된 날 (절대 일수). 무소속이 길수록 요구 주급이 내려가고 아래 리그에도 갈 마음이 생긴다 (Logic/SelfValue.cs)</summary>
        public double? freeSince;
        /// <summary>Unity판: 출전 불만 뒤로 이어 간 출전 수와 마지막 출전일</summary>
        public int? benchApps;
        public double? benchLastAt;
        /// <summary>Unity판: 주급 인상을 요구한 시즌과 답할 기한</summary>
        public int? raiseSeason;
        public double? raiseBy;
    }
}
