// 전투 이벤트 로그를 텍스트 중계로 바꾼다 (src/core/combat/commentary.ts). 같은 seed면 웹판과 같은 문장이 나온다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static Colosseum.Core.Josa;

namespace Colosseum.Core.Combat
{
    public class CommentaryLine
    {
        public int round;
        public string text;
        /// <summary>normal | hit | crit | blood | crowd | system | death</summary>
        public string tone;
        public CombatEvent ev;
    }

    public static class Commentary
    {
        static readonly Dictionary<string, string> PartNames = new()
        {
            ["head"] = "머리", ["neck"] = "목", ["chest"] = "가슴", ["side"] = "옆구리", ["shoulder"] = "어깨", ["arm"] = "팔", ["thigh"] = "허벅지", ["leg"] = "정강이",
        };

        public static readonly Dictionary<string, string> StanceNames = new()
        {
            ["assault"] = "맹공", ["balanced"] = "균형", ["bulwark"] = "철벽", ["harass"] = "견제", ["showman"] = "쇼맨십", ["blitz"] = "속전속결",
        };

        static string StatusText(string status, string n) => status switch
        {
            "stun" => $"{J(n, "이/가")} 비틀거리며 정신을 놓았습니다.",
            "bound" => $"그물이 {J(n, "을/를")} 휘감았습니다!",
            "prone" => $"{J(n, "이/가")} 모래 위로 나뒹굽니다.",
            "grappled" => $"{J(n, "이/가")} 목을 졸린 채 꼼짝 못 합니다.",
            "blinded" => $"{n}의 눈에 모래가 들어갔습니다. 앞이 안 보입니다.",
            "armor_break" => $"{n}의 갑옷이 쩍 갈라졌습니다.",
            "poison" => $"{n}의 상처에서 검게 독이 번집니다.",
            "weakened" => $"{n}의 팔다리에서 힘이 쭉 빠집니다.",
            "hobbled" => $"{J(n, "이/가")} 다리를 절뚝입니다.",
            "arm_wound" => $"{n}의 무기 든 팔이 축 늘어집니다.",
            _ => null,
        };

        static readonly Dictionary<string, string> SelfText = new()
        {
            ["skill.iron_wall"] = "방패를 땅에 박고 몸을 웅크립니다",
            ["skill.riposte"] = "칼끝을 낮추고 상대의 빈틈을 노립니다",
            ["skill.parry"] = "발놀림을 가볍게 하며 공격을 흘려낼 채비를 합니다",
            ["skill.catch_breath"] = "한 걸음 물러나 거친 숨을 고릅니다",
            ["skill.taunt"] = "무기를 내리고 관중을 향해 두 팔을 벌립니다",
            ["skill.flame_weapon"] = "칼날을 손으로 훑자 불길이 확 일어납니다",
            ["skill.ward_rune"] = "허공에 룬을 그립니다. 푸른 빛이 몸을 감쌉니다",
            ["skill.adapt"] = "상대의 수를 읽고 자세를 바꿉니다",
            ["skill.war_cry"] = "짐승 같은 함성을 내지릅니다",
            ["skill.wind_step"] = "몸놀림이 바람처럼 가벼워집니다",
            ["skill.earth_stance"] = "두 발을 모래에 단단히 박습니다. 이제 꿈쩍도 않습니다",
            ["skill.war_roar"] = "포효합니다. 상대의 눈빛이 흔들립니다",
        };

        /// <param name="gore">strong | normal | mild</param>
        public static List<CommentaryLine> Build(List<CombatEvent> events, Dictionary<string, Gladiator> fighters, uint seed, string gore = "strong")
        {
            var rng = new Rng(seed ^ 0x5bd1e995u);
            var o = new List<CommentaryLine>();
            string Name(string id) => id != null && fighters.TryGetValue(id, out var g) ? g.name : "?";
            int round = 0;
            var start = events.FirstOrDefault(x => x.t == "start");
            bool noCrowd = start != null && start.rule == "training";
            void Push(string text, string tone, CombatEvent e)
            {
                // 훈련전은 관중이 없으니 관중 이야기를 뺀다
                string t = noCrowd ? WithoutCrowd(text) : text;
                if (!string.IsNullOrEmpty(t)) o.Add(new CommentaryLine { round = round, text = t, tone = tone, ev = e });
            }
            string Pick(params string[] lines) => rng.Pick(lines);

            foreach (var e in events)
            {
                switch (e.t)
                {
                    case "start" when e.actors != null:
                    {
                        // 난전: turns가 0쪽 사람 수
                        var left = e.actors.Take(e.turns).Select(Name);
                        var right = e.actors.Skip(e.turns).Select(Name);
                        Push($"{string.Join("·", left)} 대 {string.Join("·", right)}. 양쪽 {e.actors.Length}명이 한꺼번에 모래 위로 쏟아져 나옵니다. 난전입니다!", "system", e);
                        break;
                    }
                    case "start":
                    {
                        var a = fighters[e.a]; var b = fighters[e.b];
                        Push(noCrowd
                            ? $"텅 빈 연습장. {J(a.name, "과/와")} {J(b.name, "이/가")} 마주 섭니다. 들리는 건 교관의 호루라기뿐입니다."
                            : $"{J(a.name, "과/와")} {J(b.name, "이/가")} 모래 위에 섰습니다. 관중석이 술렁입니다.", "system", e);
                        break;
                    }
                    case "round":
                        round = e.round;
                        break;
                    case "move":
                    {
                        string n = Name(e.actor);
                        if (e.flee) { Push(Pick($"{J(n, "이/가")} 등을 보이며 달아납니다! 관중석에서 야유가 쏟아집니다.", $"{J(n, "은/는")} 겁에 질려 벽을 따라 도망칩니다."), "crowd", e); break; }
                        if (e.center) { Push(Pick($"{J(n, "이/가")} 벽을 등지지 않으려 가운데로 돌아 들어갑니다.", $"{J(n, "은/는")} 원을 그리며 경기장 한가운데를 차지합니다."), "normal", e); break; }
                        if (e.skill != null && e.skill != "skill.disengage") break; // 돌진류는 공격 문장에서 처리
                        if (e.closer) Push(Pick($"{J(n, "이/가")} 거리를 좁힙니다.", $"{J(n, "이/가")} 모래를 박차고 다가섭니다.", $"{J(n, "이/가")} 천천히 원을 그리며 다가갑니다."), "normal", e);
                        else Push(Pick($"{J(n, "이/가")} 뒤로 물러나 거리를 벌립니다.", $"{J(n, "은/는")} 한 걸음 빠지며 간격을 잽니다."), "normal", e);
                        break;
                    }
                    case "exchange":
                    {
                        string a = Name(e.actor), d = Name(e.target);
                        string dir = Exchange.DirNames[e.dir], mo = Exchange.MotionNames[e.motion], gd = Exchange.GuardNames[e.guard];
                        string lead = e.improv
                            ? (e.dir == 3 ? "사선으로 꺾여 드는 변칙 수! " : "본 적 없는 수입니다! ")
                            : e.feint == "ok" ? $"{J(a, "이/가")} 치는 척하다 방향을 꺾습니다! "
                            : e.feint == "fail" ? $"{a}의 속임수는 통하지 않습니다. "
                            : "";
                        if (e.outcome == "parry")
                        {
                            string how = new[] { "정면으로 받아낸 칼날이 튕겨 나갑니다.", "칼날을 비스듬히 흘려 보냅니다.", "몸을 비틀어 헛치게 만듭니다. 크게 빈틈이 났습니다!" }[e.guard];
                            string[] nths = { "", "", "", "세", "네", "다섯" };
                            string nth = e.seen + 1 < nths.Length ? nths[e.seen + 1] : "또 한";
                            string read = e.seen >= 2
                                ? $"{nth} 번째입니다. {J(d, "은/는")} 이미 그 {J(mo, "을/를")} 기다리고 있었습니다. "
                                : $"{J(d, "이/가")} {dir} {J(mo, "을/를")} 읽었습니다! ";
                            Push(lead + read + how, "hit", e);
                        }
                        else if (e.outcome == "break")
                            Push($"{lead}{J(mo, "이/가")} {d}의 {J(gd, "을/를")} 그대로 짓누릅니다. 자세가 무너졌습니다!", "hit", e);
                        else if (e.outcome == "clean" && (lead != "" || rng.Chance(0.3)))
                        {
                            string l = lead.Trim();
                            Push(l != "" ? l : $"{J(d, "은/는")} {Exchange.DirNames[e.guardDir]}을 막았지만, {a}의 {J(mo, "은/는")} {dir}으로 파고듭니다.", "normal", e);
                        }
                        else if (e.outcome == "late" && rng.Chance(0.4))
                            Push($"{lead}방향을 잘못 읽었습니다… {J(d, "이/가")} 늦게나마 몸을 틉니다.", "normal", e);
                        else if (lead != "") Push(lead.Trim(), "normal", e);
                        break;
                    }
                    case "attack":
                    {
                        if (e.parried) break; // 수 싸움 문장으로 이미 말했다
                        string a = Name(e.actor), d = Name(e.target);
                        var skill = e.skill != null ? Data.Skill(e.skill) : null;
                        string prefix = (e.counter ? "반격! " : "") + (skill != null ? $"{a}의 {skill.name}! " : "");
                        string part = PartNames[e.part];
                        if (!e.hit)
                        {
                            // 기술 이름으로 이미 공격자를 말했으면 문장에서 이름을 다시 쓰지 않는다
                            string who = skill != null ? "" : $"{a}의 ";
                            Push(prefix + Pick(
                                $"{who}공격이 허공을 가릅니다.",
                                $"{J(d, "이/가")} 몸을 틀어 피했습니다!",
                                skill != null ? "하지만 빗나갔습니다. 관중석에서 탄식이 터집니다." : $"{a}의 공격이 빗나갔습니다. 관중석에서 탄식이 터집니다."), "normal", e);
                            break;
                        }
                        if (e.blocked)
                        {
                            Push(prefix + Pick($"{J(d, "이/가")} 막아냈습니다! 쇳소리가 경기장을 울립니다.", $"{a}의 공격이 방패에 튕겨 나갑니다.") + $" ({d} {e.hpAfter}/{e.maxHp})", "normal", e);
                            break;
                        }
                        double ratio = e.damage / e.maxHp;
                        string hpText = $" ({d} {e.hpAfter}/{e.maxHp})";
                        if (e.cornered) prefix = $"벽에 몰린 {d}, 피할 곳이 없습니다! " + prefix;
                        var weapon = WeaponWord(fighters.TryGetValue(e.actor, out var ag) ? ag : null, e.skill);
                        if (e.magic) Push($"{prefix}{(skill != null ? "" : $"{a}의 ")}주술이 {J(d, "을/를")} 덮칩니다! 살 타는 냄새가 퍼집니다.{hpText}", e.crit ? "crit" : "hit", e);
                        else if (e.crit) Push(prefix + BloodLine(rng, gore, a, d, part, "crit", weapon, skill == null) + hpText, "crit", e);
                        else if (ratio > 0.12) Push(prefix + BloodLine(rng, gore, a, d, part, "heavy", weapon, skill == null) + hpText, "blood", e);
                        else Push(prefix + BloodLine(rng, gore, a, d, part, "light", weapon, skill == null) + hpText, "hit", e);
                        if (e.push) Push(Pick($"{J(d, "이/가")} 충격에 뒤로 밀려납니다.", $"{J(d, "은/는")} 모래 위를 미끄러지며 물러섭니다."), "normal", e);
                        break;
                    }
                    case "status":
                    {
                        string n = Name(e.target);
                        if (e.resisted) Push($"{J(n, "은/는")} 버텨냈습니다!", "normal", e);
                        else if (StatusText(e.status, n) is string st) Push(st, "hit", e);
                        break;
                    }
                    case "self":
                    {
                        var s = Data.Skill(e.skill);
                        Push($"{J(Name(e.actor), "이/가")} {(SelfText.TryGetValue(e.skill, out var tx) ? tx : "자세를 가다듬습니다")}. ({s.name})", "normal", e);
                        break;
                    }
                    case "react":
                    {
                        // 반응 기술 (기술 테이블): 이름만 짧게
                        var r = SkillBook.Row(e.skill);
                        Push($"{J(Name(e.actor), "이/가")} 곧바로 '{r?.name ?? e.skill}'!", "normal", e);
                        break;
                    }
                    case "crowd":
                        if (e.delta >= 8) Push(Pick("관중이 일어섭니다! 함성이 터집니다.", "환호성이 경기장을 뒤흔듭니다!"), "crowd", e);
                        else if (e.delta <= -8) Push(Pick("야유가 쏟아집니다.", "관중석에서 돌멩이가 날아듭니다."), "crowd", e);
                        break;
                    case "rest":
                        Push($"{J(Name(e.actor), "이/가")} 숨을 몰아쉽니다.", "normal", e);
                        break;
                    case "poison":
                        Push($"독이 돕니다. {J(Name(e.target), "이/가")} 검은 피를 토합니다. ({e.hpAfter})", "blood", e);
                        break;
                    case "skip":
                    {
                        string t = e.reason switch { "stun" => "아직 정신을 차리지 못했습니다", "prone" => "모래를 짚고 겨우 일어납니다", _ => "붙잡힌 채 버둥거립니다" };
                        Push($"{J(Name(e.actor), "이/가")} {t}.", "normal", e);
                        break;
                    }
                    case "enrage":
                        Push($"▶ 피의 격노 — {Name(e.actor)}의 눈이 붉게 물듭니다.", "crit", e);
                        break;
                    case "unbreakable":
                        Push($"▶ 불굴 — 쓰러지나 싶던 {J(Name(e.actor), "이/가")} 피투성이가 된 채 다시 일어섭니다!", "crit", e);
                        break;
                    case "doctrine":
                    {
                        string n = Name(e.actor);
                        var a = e.action;
                        string text = a.type switch
                        {
                            "stance" => $"{J(n, "이/가")} 자세를 {J(StanceNames[a.stance], "으로/로")} 바꿉니다.",
                            "skill" => $"{J(n, "이/가")} 기회를 노립니다. 노리는 건 {Data.Skill(a.skill)?.name ?? "한 방"}입니다.",
                            "rest" => $"{J(n, "이/가")} 교범대로 물러나 숨을 돌립니다.",
                            _ => $"{J(n, "이/가")} 거리를 좁혀 들어갑니다.",
                        };
                        Push($"▶ 교범 — {text}", "system", e);
                        break;
                    }
                    case "thorns":
                        Push($"{Name(e.actor)}의 장비가 {Name(e.target)}에게 상처를 되돌려 줍니다. ({Name(e.target)} {JsMath.Round(e.hpAfter)}/{JsMath.Round(e.maxHp)})", "hit", e);
                        break;
                    case "surrender":
                        Push($"{J(Name(e.actor), "이/가")} 무릎을 꿇고 손가락을 들어 올립니다. 항복입니다.", "system", e);
                        break;
                    case "ko" when e.actor != null:
                        // 난전: 누가 쓰러뜨렸는지 함께
                        Push(e.crit
                            ? $"{Name(e.actor)}의 일격! {J(Name(e.target), "이/가")} 피를 뿜으며 쓰러졌습니다!"
                            : $"{J(Name(e.target), "이/가")} {Name(e.actor)}에게 쓰러졌습니다. 난전은 계속됩니다…", "death", e);
                        break;
                    case "ko":
                        Push(e.crit
                            ? $"{J(Name(e.target), "이/가")} 피를 뿜으며 쓰러졌습니다! 모래가 검붉게 젖어 듭니다."
                            : $"{J(Name(e.target), "이/가")} 쓰러졌습니다. 관중이 숨을 죽입니다…", "death", e);
                        break;
                    case "timeout":
                        Push("시간 종료! 심판들이 판정에 들어갑니다.", "system", e);
                        break;
                    case "verdict":
                    {
                        string n = Name(e.target);
                        string opp = OpponentOf(events, e.target, fighters);
                        if (e.rule == "missio")
                        {
                            Push($"▶ 미시오 판정 — 주최자가 관중을 둘러봅니다… (사망 확률 {JsMath.Round(e.deathChance * 100)}%)", "system", e);
                            Push(e.died
                                ? $"엄지가 아래를 향합니다. {J(opp, "이/가")} {n}의 숨통을 끊습니다. 피가 모래 위로 쏟아집니다."
                                : $"엄지가… 위로! {J(n, "은/는")} 살아남았습니다.", e.died ? "death" : "crowd", e);
                        }
                        else if (e.died)
                        {
                            Push(e.rule == "sudden"
                                ? $"사투에 자비는 없습니다. {J(opp, "이/가")} 마지막 일격을 내리꽂습니다. {J(n, "은/는")} 다시 일어나지 못했습니다."
                                : $"{J(n, "이/가")} 일어나지 못합니다. 의무병이 뛰어들었지만 이미 숨이 끊긴 뒤였습니다.", "death", e);
                        }
                        else if (e.rule == "sudden") Push($"기적입니다! 아직 숨이 붙어 있습니다. {J(n, "이/가")} 들것에 실려 나갑니다.", "crowd", e);
                        break;
                    }
                    case "end":
                    {
                        string reason = e.reason switch { "ko" => "KO", "surrender" => "항복", _ => "판정" };
                        if (start?.actors != null)
                        {
                            bool left = Array.IndexOf(start.actors, e.winner) < start.turns;
                            var side = left ? start.actors.Take(start.turns) : start.actors.Skip(start.turns);
                            Push($"■ {string.Join("·", side.Select(Name))} 편 승리 ({(reason == "KO" ? "전멸" : reason)}) · 가장 빛난 검투사 {Name(e.winner)}", "system", e);
                        }
                        else Push($"■ {Name(e.winner)} 승리 ({reason})", "system", e);
                        break;
                    }
                }
            }
            return o;
        }

        static readonly Regex CrowdWords = new("관중|환호|야유|탄식");

        /// <summary>관중이 나오는 문장만 지운다. 남는 게 없으면 빈 문자열</summary>
        static string WithoutCrowd(string text) =>
            string.Join(" ", Regex.Split(text, @"(?<=[.!…])\s+").Where(s => !CrowdWords.IsMatch(s))).Trim();

        static string OpponentOf(List<CombatEvent> events, string id, Dictionary<string, Gladiator> fighters)
        {
            var start = events.FirstOrDefault(e => e.t == "start");
            if (start == null) return "?";
            // 난전: 그 사람을 쓰러뜨린 자 (항복이면 마지막으로 맞붙은 적)
            if (start.actors != null)
            {
                string by = events.LastOrDefault(e => e.t == "ko" && e.target == id)?.actor
                    ?? events.LastOrDefault(e => e.t == "attack" && e.target == id)?.actor;
                return by != null && fighters.TryGetValue(by, out var g) ? g.name : "?";
            }
            return fighters[start.a == id ? start.b : start.a].name;
        }

        static readonly Dictionary<string, (string noun, string kind)> WeaponWords = new()
        {
            ["sword"] = ("칼", "cut"), ["greatsword"] = ("대검", "cut"), ["axe"] = ("도끼", "cut"),
            ["dagger"] = ("단검", "stab"), ["spear"] = ("창", "stab"), ["trident_net"] = ("삼지창", "stab"), ["throwing"] = ("투창", "stab"),
            ["mace"] = ("철퇴", "blunt"), ["unarmed"] = ("주먹", "blunt"), ["shield"] = ("방패", "blunt"), ["focus"] = ("지팡이", "blunt"),
        };

        static (string noun, string kind) WeaponWord(Gladiator g, string skillId)
        {
            if (skillId == "skill.javelin") return WeaponWords["throwing"];
            if (skillId == "skill.shield_bash") return WeaponWords["shield"];
            if (skillId == "skill.horn_charge") return ("뿔", "stab");
            if (skillId == "skill.venom_bite") return ("이빨", "stab");
            if (skillId == "skill.pounce") return ("발톱", "cut");
            var w = g != null ? Data.Weapon(g.equipment.main) : null;
            return w != null ? WeaponWords[w.@class] : WeaponWords["unarmed"];
        }

        static string BloodLine(Rng rng, string gore, string a, string d, string part, string level, (string noun, string kind) weapon, bool withName)
        {
            string w = withName ? $"{a}의 {J(weapon.noun, "이/가")}" : J(weapon.noun, "이/가");
            string p = J(part, "을/를");
            string pSubj = J(part, "이/가");
            if (gore == "mild")
                return level == "light" ? $"{w} {d}의 {p} 스쳤습니다." : level == "heavy" ? $"{w} {d}의 {p} 제대로 때렸습니다!" : $"치명타! {w} {d}의 {p} 정통으로 맞혔습니다!";
            bool strong = gore == "strong";
            string[] lines = (weapon.kind, level) switch
            {
                ("cut", "light") => new[] { $"{w} {d}의 {p} 스칩니다. 피가 배어 나옵니다.", $"{d}의 {pSubj} 얕게 베였습니다." },
                ("cut", "heavy") => new[] { $"{w} {d}의 {p} 깊게 가릅니다! 피가 튑니다.", $"{d}의 {pSubj} 쩍 갈라집니다!{(strong ? " 모래가 붉게 젖어 듭니다." : "")}" },
                ("cut", _) => strong
                    ? new[] { $"치명타! {w} {d}의 {p} 그대로 갈라 버립니다. 관중석 앞줄까지 피가 튑니다!", $"치명타! {d}의 {part}에서 피가 뿜어져 나옵니다. 살점이 모래 위로 떨어집니다!" }
                    : new[] { $"치명타! {w} {d}의 {p} 깊숙이 베었습니다!" },
                ("stab", "light") => new[] { $"{w} {d}의 {p} 얕게 찌릅니다. 피가 번집니다.", $"{w} {d}의 {p} 긁고 지나갑니다." },
                ("stab", "heavy") => new[] { $"{w} {d}의 {part}에 깊이 박힙니다! 뽑아내자 피가 솟습니다.", $"{w} {d}의 {p} 파고듭니다! {d}의 입에서 신음이 터집니다." },
                ("stab", _) => strong
                    ? new[] { $"치명타! {w} {d}의 {p} 꿰뚫었습니다. 피가 왈칵 쏟아집니다!", $"치명타! {w} {d}의 {p} 관통했습니다. 등 뒤로 핏방울이 흩날립니다!" }
                    : new[] { $"치명타! {w} {d}의 {p} 꿰뚫었습니다!" },
                (_, "light") => new[] { $"{w} {d}의 {p} 때립니다. 살갗이 터지며 피가 맺힙니다.", $"{w} {d}의 {p} 스치듯 후려칩니다." },
                (_, "heavy") => new[] { $"퍽! {(withName ? $"{a}의 " : "")}{weapon.noun}에 {d}의 {pSubj} 짓이겨집니다.", $"둔탁한 소리와 함께 {J(d, "이/가")} 휘청입니다. {part}에서 피가 흐릅니다." },
                _ => strong
                    ? new[] { $"치명타! 뼈 부러지는 소리가 경기장에 울립니다. {d}의 {pSubj} 기괴하게 꺾였습니다!", $"치명타! {w} {d}의 {p} 으스러뜨립니다. 피와 뼛조각이 모래 위로 튑니다!" }
                    : new[] { $"치명타! {w} {d}의 {p} 정통으로 후려쳤습니다!" },
            };
            return rng.Pick(lines);
        }
    }
}
