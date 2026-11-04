// 마우스를 올리면 설명이 뜨는 글자 (웹판 common.tsx RaceTag/StyleTag).
// TMP <link="race:id">·<link="style:id">로 감싸 두면 TipLayer가 알아서 툴팁을 띄우고,
// <link="g:id">로 감싼 검투사 이름은 누르면 팝업 카드가 뜬다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using UnityEngine;

namespace Colosseum.UI
{
    public static class Tips
    {
        /// <summary>종족 이름 (툴팁 링크)</summary>
        public static string Race(string id) => $"<link=\"race:{id}\">{Data.Race(id)?.name ?? id}</link>";

        /// <summary>스타일 이름 (툴팁 링크)</summary>
        public static string Style(string id) => $"<link=\"style:{id}\">{Data.Style(id)?.name ?? id}</link>";

        /// <summary>검투사 이름 (누르면 팝업)</summary>
        public static string Name(Gladiator g) => g == null ? "" : $"<link=\"g:{g.id}\">{g.name}</link>";

        /// <summary>나라 이름 (누르면 나라 카드)</summary>
        public static string Nation(string id, bool shortName = false) =>
            id == null ? "" : $"<link=\"nation:{id}\">{(shortName ? Data.Nation(id).@short : Data.Nation(id).name)}</link>";

        /// <summary>루두스 이름 (누르면 루두스 카드)</summary>
        public static string Ludus(Ludus l) => l == null ? "무소속" : $"<link=\"ludus:{l.id}\">{l.name}</link>";
        public static string Ludus(string id, string name) => id == null ? (name ?? "") : $"<link=\"ludus:{id}\">{name}</link>";

        /// <summary>대회 이름 (누르면 대회 카드): 판테온 컵</summary>
        public static string Cup() => $"<link=\"comp:cup\">{Honours.CupName}</link>";

        /// <summary>대회 이름 (누르면 대회 카드): 나라·부 리그</summary>
        public static string League(string nation, int tier) => $"<link=\"comp:league:{nation}:{tier}\">{Honours.LeagueName(nation, tier)}</link>";

        /// <summary>국내 컵 이름 (누르면 대회 카드)</summary>
        public static string NatCup(string nation) => $"<link=\"comp:natcup:{nation}\">{NatCupLogic.Name(nation)}</link>";

        /// <summary>올림피카 컵 이름 (누르면 대회 카드)</summary>
        public static string Olympica() => $"<link=\"comp:olympica\">{OlympicaLogic.Name}</link>";

        /// <summary>장비 이름 (등급 색, 올리면 성능, 누르면 장비 카드)</summary>
        public static string Item(string id) => string.IsNullOrEmpty(id) ? "" : $"<link=\"item:{id}\">{Widgets.ItemLabel(id)}</link>";

        // ── 저장된 글(서판 소식 등)에 링크 입히기 ──

        static System.Text.RegularExpressions.Regex autoRe;
        static Dictionary<string, string> autoMap;
        static int autoStamp = -1;
        static GameState autoState;

        /// <summary>글 속의 루두스·검투사·식솔·나라·리그·컵 이름을 링크로 바꾼다 (FM처럼 웬만한 이름은 눌러 볼 수 있게).
        /// 서판 소식은 만들 때 글자로 저장되므로 보여 줄 때 입힌다. 이미 링크·태그가 든 글은 그대로 둔다</summary>
        public static string AutoLink(GameState s, string text)
        {
            if (s == null || string.IsNullOrEmpty(text) || text.Contains("<link")) return text;
            if (autoState != s || autoStamp != s.nextId)
            {
                autoState = s;
                autoStamp = s.nextId;
                autoMap = new Dictionary<string, string>();
                void Add(string name, string link)
                {
                    if (string.IsNullOrEmpty(name) || name.Length < 2 || autoMap.ContainsKey(name)) return;
                    autoMap[name] = $"<link=\"{link}\">{name}</link>";
                }
                foreach (var n in Data.Nations)
                {
                    for (int t = 1; t <= FmRules.MaxTier; t++) Add(Honours.LeagueName(n.id, t), $"comp:league:{n.id}:{t}");
                    Add(NatCupLogic.Name(n.id), "comp:natcup:" + n.id);
                    Add(n.name, "nation:" + n.id);
                }
                Add(Honours.CupName, "comp:cup");
                Add(OlympicaLogic.Name, "comp:olympica");
                foreach (var l in s.ludi.Values) Add(l.name, "ludus:" + l.id);
                foreach (var st in s.staff.Values) if (st.name.Contains(' ')) Add(st.name, "person:" + st.id);
                // 한 단어 이름은 다른 낱말과 겹치기 쉬워 두 단어(이름 성)이거나 세 글자 이상만
                foreach (var g in s.gladiators.Values) if (g.name.Contains(' ') || g.name.Length >= 3) Add(g.name, "g:" + g.id);
                var keys = autoMap.Keys.OrderByDescending(k => k.Length).Select(System.Text.RegularExpressions.Regex.Escape);
                autoRe = new System.Text.RegularExpressions.Regex($"(?<![가-힣A-Za-z])(?:{string.Join("|", keys)})", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            }
            return autoRe.Replace(text, m => autoMap.TryGetValue(m.Value, out var v) ? v : m.Value);
        }

        /// <summary>식솔 이름 (올리면 역할·핵심 능력치, 누르면 세부 능력치 카드)</summary>
        public static string Person(Staff s) => s == null ? "" : $"<link=\"person:{s.id}\">{s.name}</link>";

        /// <summary>식솔 능력치 이름 (올리면 하는 일)</summary>
        public static string StaffAttr(string key) => $"<link=\"sattr:{key}\">{StaffLogic.AttrNames[key]}</link>";

        /// <summary>역할에 중요한 능력치 한 줄: "신체 지도 14 · 기술 지도 12 · 정신 지도 9 · 전술 지도 11"</summary>
        public static string StaffKeyAttrs(Staff s, bool links = false, int top = 0)
        {
            if (s == null) return "";
            var keys = StaffLogic.RoleWeights[s.role].Where(x => x.w >= 1).Select(x => x.key);
            // 좁은 칸에는 가장 잘하는 것 몇 개만 (전부는 툴팁·카드로)
            if (top > 0) keys = keys.OrderByDescending(k => StaffLogic.Attr(s, k)).Take(top);
            return string.Join(" · ", keys.Select(k => $"{(links ? StaffAttr(k) : StaffLogic.AttrNames[k])} {StaffLogic.Attr(s, k)}"));
        }

        static string PersonTip(string id)
        {
            var st = GameController.I?.State;
            if (st == null || !st.staff.TryGetValue(id, out var s)) return null;
            return $"<b>{s.name}</b>  <color={Theme.Hex(Theme.TextDim)}>{StaffLogic.RoleNames[s.role]} · 평점 {StateUtil.Num(s.rating)} · {StateUtil.Num(s.age)}세</color>\n{StaffKeyAttrs(s)}{More}";
        }

        // ── 수상 (Logic/Awards.cs): awards:g:검투사 = 그 검투사가 받은 상, awards:season:N = 시즌 시상식 ──

        /// <summary>검투사가 받은 상 개수 링크 (없으면 빈 글)</summary>
        public static string AwardsOf(GameState s, Gladiator g)
        {
            int n = s.awards?.Count(a => a.gladiatorId == g.id) ?? 0;
            return n == 0 ? "" : $"<link=\"awards:g:{g.id}\">수상 {n}회</link>";
        }

        public static string Ceremony(int season) => $"<link=\"awards:season:{season}\">시즌 {season} 시상식</link>";

        static string AwardsTip(string id)
        {
            var s = GameController.I?.State;
            if (s == null) return null;
            if (id.StartsWith("season:")) return $"<b>시즌 {id.Substring(7)} 시상식</b>\n대회별 수상과 나라별·세계 올해의 검투사{More}";
            if (!id.StartsWith("g:")) return null;
            var list = (s.awards ?? new List<AwardEntry>()).Where(a => a.gladiatorId == id.Substring(2)).OrderByDescending(a => a.season).ToList();
            if (list.Count == 0) return null;
            return "<b>받은 상</b>\n" + string.Join("\n", list.Take(6).Select(a => $"시즌 {a.season} {Awards.CompName(a.comp)} {Awards.KindNames[a.kind]}")) + (list.Count > 6 ? $"\n… 외 {list.Count - 6}개" : "") + More;
        }

        /// <summary>링크 id에 맞는 툴팁 글. 툴팁 없이 누르기만 하는 링크면 null</summary>
        public static string For(string link)
        {
            int i = link.IndexOf(':');
            if (i < 0) return null;
            string kind = link.Substring(0, i), id = link.Substring(i + 1);
            return kind switch
            {
                "race" => RaceText(id), "style" => StyleText(id), "item" => ItemTip(id),
                "nation" => $"<b>{Data.Nation(id).name}</b>\n<color={Theme.Hex(Theme.TextDim)}>{Data.Nation(id).motif}</color>{More}",
                "ludus" => LudusTip(id),
                "comp" => $"<b>{(id == "cup" ? Honours.CupName : CompName(id))}</b>{More}",
                "g" => GladiatorTip(id),
                "skill" => SkillTip(id),
                "trait" => TraitTip(id),
                "dungeon" => DungeonTip(id),
                "status" => StatusTip(id),
                "info" => InfoTip(id),
                "racial" => RacialTip(id),
                "role" => Tactics.RoleDesc.TryGetValue(id, out var rd) ? $"<b>{Tactics.RoleNames[id]}</b>\n{rd}" : null,
                "staff" => StaffLogic.RoleDesc.TryGetValue(id, out var sd) ? $"<b>{StaffLogic.RoleNames[id]}</b>\n{sd}" : null,
                "person" => PersonTip(id),
                "sattr" => StaffLogic.AttrDesc.TryGetValue(id, out var ad) ? $"<b>{StaffLogic.AttrNames[id]}</b>\n{ad}" : null,
                "awards" => AwardsTip(id),
                _ => null,
            };
        }

        /// <summary>종족 특성 (특성 줄 맨 앞 ◆). 올리면 설명, 누르면 종족 카드</summary>
        public static string Racial(string raceId, string color = null)
        {
            var r = Data.Race(raceId);
            if (r == null) return "";
            string n = $"◆ {r.racialTrait.name}";
            return $"<link=\"racial:{raceId}\">{(color != null ? $"<color={color}>{n}</color>" : n)}</link>";
        }

        static string RacialTip(string raceId)
        {
            var r = Data.Race(raceId);
            return r == null ? null : $"<b>◆ {r.racialTrait.name}</b>  <color={Theme.Hex(Theme.TextDim)}>{r.name} 종족 특성</color>\n{r.racialTrait.desc}{More}";
        }

        /// <summary>단체전 역할 이름 (올리면 설명)</summary>
        public static string Role(string id) => Tactics.RoleNames.TryGetValue(id, out var n) ? $"<link=\"role:{id}\">{n}</link>" : id;

        /// <summary>식솔 자리 이름 (올리면 설명)</summary>
        public static string StaffRole(string id) => StaffLogic.RoleNames.TryGetValue(id, out var n) ? $"<link=\"staff:{id}\">{n}</link>" : id;

        // ── 상단 바 설명 (info:pay 다음 급료일 내역, info:window 이적 시장 기간) ──

        public static string Pay(string text) => $"<link=\"info:pay\">{text}</link>";
        public static string Window(string text) => $"<link=\"info:window\">{text}</link>";

        static string InfoTip(string id)
        {
            var s = GameController.I?.State;
            if (s == null) return null;
            var me = s.ludi[s.playerLudusId];
            string dim = Theme.Hex(Theme.TextDim), faint = Theme.Hex(Theme.TextFaint);
            if (id == "pay")
            {
                var b = Economy.NextPayBill(s, me);
                string Row(string k, double v) => $"\n{k} <color={dim}>{v:N0} D</color>";
                string tail = b.Net > me.treasury
                    ? $"\n<color=#d9a29b>금고가 {b.Net - me.treasury:N0} D 모자랍니다. 주급이 높은 검투사부터 주고, 못 받은 자는 급료가 밀립니다</color>"
                    : $"\n<color={faint}>내고 나면 {me.treasury - b.Net:N0} D가 남습니다</color>";
                return $"<b>다음 급료일 · {Calendar.NextPaydayLabel(s.day)}</b>" +
                    Row("검투사 주급", b.wages) + (b.arrears > 0 ? $"\n밀린 급료 <color=#d9a29b>{b.arrears:N0} D</color>" : "") +
                    Row("식비", b.food) + Row("시설 유지비", b.upkeep) + Row("식솔 급료", b.staff) +
                    $"\n<b>합계 {b.Total:N0} D</b>" +
                    (b.grant > 0 ? $"\n하부 리그 지원금 <color={Theme.Hex(Theme.Good)}>+{b.grant:N0} D</color> <color={faint}>(급료일에 먼저 들어옴, 세금 뺀 값)</color>" : "") +
                    $"{tail}\n<color={faint}>용병으로 나간 검투사의 주급은 고용국이 냅니다</color>";
            }
            if (id == "window")
                return $"<b>{Transfers.WindowLine(s)}</b>\n<color={dim}>이적 시장 기간: {Transfers.WindowLabel}</color>\n" +
                    $"<color={faint}>다른 루두스 검투사에게는 이 기간에만 이적료를 제안할 수 있습니다. 무소속 검투사는 언제든 영입할 수 있습니다.\n제안을 보내면 하루~사흘 뒤 서판으로 답이 옵니다</color>";
            return null;
        }

        // ── 특성·던전·상태 ──

        /// <summary>특성 이름 링크. color가 있으면 그 색으로 (나쁜 특성 붉게 등)</summary>
        public static string Trait(string id, string color = null)
        {
            string n = Data.Trait(id)?.name ?? id;
            return $"<link=\"trait:{id}\">{(color != null ? $"<color={color}>{n}</color>" : n)}</link>";
        }

        public static string TraitTip(string id)
        {
            var t = Data.Trait(id);
            return t == null ? null : $"<b>{t.name}</b>  <color={Theme.Hex(Theme.TextDim)}>{(t.cat == "personality" ? "성격" : t.cat == "combat" ? "전투" : "몸")}{(t.negative ? " · 나쁨" : "")}</color>\n{t.desc}";
        }

        public static string Dungeon(string id) => $"<link=\"dungeon:{id}\">{DungeonLogic.GetDungeon(id).name}</link>";

        public static string DungeonTip(string id)
        {
            var d = DungeonLogic.Dungeons.FirstOrDefault(x => x.id == id);
            return d == null ? null : $"<b>{d.name}</b>  <color={Theme.Hex(Theme.TextDim)}>{d.tier}등급 · {d.floors}층 · {d.days}일</color>\n{d.desc}{More}";
        }

        /// <summary>상태 이상 링크 (표의 Status 키, 예: Bleed)</summary>
        public static string Status(string key)
        {
            var s = SkillBook.Statuses.TryGetValue(key, out var r) ? r : null;
            return s == null ? key : $"<link=\"status:{key}\">{s.name}</link>";
        }

        public static string StatusTip(string key)
        {
            if (!SkillBook.Statuses.TryGetValue(key, out var s)) return null;
            string fx = string.Join(" · ", SkillBook.EffectsOfStatus(s).Select(EffectLine));
            return $"<b>{s.name}</b>  <color={Theme.Hex(Theme.TextDim)}>{(s.kind == "Buff" ? "강화" : s.kind == "Control" ? "제압" : "약화")} · 최대 {s.maxDuration}라운드</color>\n{s.desc}\n<color={Theme.Hex(Theme.TextFaint)}>{fx}</color>";
        }

        // ── 기술 (기술 테이블) ──

        public static string Skill(string id) => $"<link=\"skill:{id}\">{SkillBook.Row(id)?.name ?? Data.Skill(id)?.name ?? id}</link>";

        public static Sprite SkillIcon(string id)
        {
            var r = SkillBook.Row(id);
            return r != null ? Resources.Load<Sprite>("Art/icon/" + r.icon) : null;
        }

        static readonly Dictionary<string, string> SkillTypeNames = new() { ["Active"] = "능동", ["Reaction"] = "반응", ["Passive"] = "상시" };
        static readonly Dictionary<string, string> TriggerText = new()
        {
            ["Always"] = "늘", ["OnTurn"] = "내 차례에", ["OnMatchStart"] = "경기 시작에", ["OnRoundAtLeast"] = "{0}라운드부터", ["OnHpBelow"] = "내 HP {0:0%} 이하일 때",
            ["OnOppHpBelow"] = "상대 HP {0:0%} 이하일 때", ["OnStaBelow"] = "기력 {0:0%} 이하일 때", ["OnOppDisabled"] = "상대가 묶이거나 넘어졌을 때", ["OnAttackHit"] = "공격이 맞을 때",
            ["OnCrit"] = "치명타일 때", ["OnParry"] = "흘려냈을 때", ["OnBlock"] = "방패로 막았을 때", ["OnDodge"] = "피했을 때", ["OnDamaged"] = "맞았을 때",
            ["OnAllyDown"] = "동료가 쓰러졌을 때", ["OnAllyHpBelow"] = "동료 HP {0:0%} 이하일 때",
        };
        static readonly Dictionary<string, string> EffectText = new()
        {
            ["Damage"] = "피해", ["ExtraHit"] = "추가 타격", ["MagicDamage"] = "주술 피해", ["Heal"] = "HP 회복", ["RestoreSta"] = "기력 회복", ["Momentum"] = "기세", ["Crowd"] = "관중",
            ["HitMod"] = "명중", ["CritMod"] = "치명타", ["PierceMod"] = "관통", ["BlockMod"] = "막기", ["EvadeMod"] = "회피", ["DamageMod"] = "주는 피해", ["DamageTakenMod"] = "받는 피해",
            ["CounterMod"] = "되받아치기", ["MercyMod"] = "관중 자비", ["CloseIn"] = "거리 좁히기", ["Disengage"] = "거리 벌리기", ["AutoHit"] = "반드시 명중", ["Endure"] = "치명상 버팀",
        };
        static readonly Dictionary<string, string> StatusText = new()
        {
            ["Stun"] = "기절", ["Prone"] = "넘어짐", ["Bound"] = "속박", ["Grappled"] = "붙잡힘", ["Blinded"] = "실명", ["Poison"] = "중독", ["Bleed"] = "출혈", ["Weakened"] = "쇠약",
            ["ArmorBreak"] = "갑옷 깨짐", ["Exposed"] = "빈틈", ["Hobbled"] = "다리 부상", ["ArmWound"] = "팔 부상", ["Guard"] = "방어 태세", ["EvadeUp"] = "회피 태세", ["Flame"] = "화염 무기",
            ["Ward"] = "수호막", ["Adapt"] = "임기응변", ["WindStep"] = "바람 걸음", ["EarthStance"] = "대지 버팀", ["Enraged"] = "격노",
        };

        static string EffectLine(SkillBook.EffectRow e)
        {
            string who = e.target switch { "Self" => "", "Enemy" => "상대 ", "Ally" => "동료 ", "AllAllies" => "우리 편 ", _ => "" };
            string chance = e.chance < 1 ? $" ({e.chance:0%})" : "";
            if (e.effect == "ApplyStatus") return $"{who}{(SkillBook.Statuses.ContainsKey(e.status) ? Status(e.status) : StatusText.TryGetValue(e.status, out var sn) ? sn : e.status)} {e.duration}라운드{chance}";
            if (e.effect == "StatMod") return $"{Attrs.Names.GetValueOrDefault(e.stat.ToLowerInvariant(), e.stat)} {e.value:+0;-0} ({e.duration}라운드)";
            string name = EffectText.TryGetValue(e.effect, out var n) ? n : e.effect;
            string v = e.valueType switch
            {
                "Multiplier" => $"×{e.value:0.##}",
                "Additive" => $"{e.value * 100:+0;-0}%",
                "PercentMax" => $"최대치의 {e.value:0%}",
                "PerStat" => $"{Attrs.Names.GetValueOrDefault(e.stat.ToLowerInvariant(), e.stat)}×{e.value:0.##}",
                _ => e.effect is "CloseIn" or "Disengage" or "AutoHit" ? "" : $"{e.value:+0;-0}",
            };
            return $"{who}{name} {v}{chance}".Trim();
        }

        public static string SkillTip(string id)
        {
            var r = SkillBook.Row(id);
            string dim = Theme.Hex(Theme.TextDim), faint = Theme.Hex(Theme.TextFaint);
            if (r == null) return $"<b>{Data.Skill(id)?.name ?? id}</b>";
            string when = TriggerText.TryGetValue(r.trigger, out var t) ? string.Format(t, r.triggerValue) : r.trigger;
            var cost = new List<string>();
            if (r.cooldown > 0 && r.cooldown < 99) cost.Add($"쿨타임 {r.cooldown}라운드");
            if (r.cooldown >= 99) cost.Add("경기에 한 번");
            if (r.sta > 0) cost.Add($"기력 {r.sta}");
            if (r.mana > 0) cost.Add($"마나 {r.mana}");
            if (r.type != "Passive") cost.Add($"발동 {r.procRate:0%}{(r.procStat != "None" ? $" + {Attrs.Names.GetValueOrDefault(r.procStat.ToLowerInvariant(), r.procStat)}" : "")}");
            string fx = string.Join(" · ", SkillBook.EffectsOf(r).Select(EffectLine));
            return $"<b>{r.name}</b>  <color={dim}>{SkillTypeNames.GetValueOrDefault(r.type, r.type)} · {new string('★', r.tier)}</color>\n{r.desc}\n" +
                   $"<color={Theme.Hex(Theme.BronzeHi)}>{when}</color> {fx}\n<color={faint}>{string.Join(" · ", cost)}</color>";
        }

        static string More =>$"\n<size=85%><color={Theme.Hex(Theme.TextFaint)}>눌러서 자세히</color></size>";

        static string CompName(string id)
        {
            var p = id.Split(':');
            if (id == "olympica") return OlympicaLogic.Name;
            if (p.Length == 2 && p[0] == "natcup") return NatCupLogic.Name(p[1]);
            return p.Length == 3 && int.TryParse(p[2], out int t) ? Honours.LeagueName(p[1], t) : id;
        }

        static string LudusTip(string id)
        {
            var s = GameController.I?.State;
            if (s == null || !s.ludi.TryGetValue(id, out var l)) return null;
            return $"<b>{l.name}</b>\n<color={Theme.Hex(Theme.TextDim)}>{Data.Nation(l.nation).@short} {l.tier}부 · RP {l.rp} · {l.record.wins}승 {l.record.losses}패</color>{More}";
        }

        static string GladiatorTip(string id)
        {
            var s = GameController.I?.State;
            if (s == null || !s.gladiators.TryGetValue(id, out var g)) return null;
            return $"<b>{g.name}</b>\n<color={Theme.Hex(Theme.TextDim)}>{Data.Race(g.race)?.name} · {Data.Style(g.style)?.name} · {g.age}세</color>\n<size=85%><color={Theme.Hex(Theme.TextFaint)}>눌러서 카드 보기</color></size>";
        }

        public static string ItemTip(string id) =>
            $"<b>{Widgets.ItemLabel(id)}</b>  <color={Theme.Hex(Theme.TextDim)}>{GearLogic.ItemKindNames.GetValueOrDefault(GearLogic.ItemKind(id) ?? "", "")}</color>\n{GearLogic.ItemStats(id)}{UniqueLines(id)}{More}";

        /// <summary>고유 장비 효과 줄 (툴팁)</summary>
        static string UniqueLines(string id)
        {
            var u = Uniques.Of(id);
            return u == null ? "" : "\n" + string.Join("\n", u.effects.Select(e => $"<color={Theme.Hex(Theme.Torch)}>◆</color> {Uniques.Describe(e)}"));
        }

        public static string RaceText(string id)
        {
            var r = Data.Race(id);
            if (r == null) return null;
            string dim = Theme.Hex(Theme.TextDim);
            string mods = string.Join(", ", r.attrMods.Select(kv => $"{(Attrs.Names.TryGetValue(kv.Key, out var n) ? n : kv.Key)} {(kv.Value > 0 ? "+" : "")}{kv.Value}"));
            return $"<b>{r.name}</b>\n<color={Theme.Hex(Theme.BronzeHi)}>◆ {r.racialTrait.name}</color>: {r.racialTrait.desc}\n" +
                $"<color={dim}>능력치</color> {(mods.Length > 0 ? mods : "보정 없음")}\n" +
                $"<color={dim}>나이</color> 데뷔 {r.age.debut}세 · 전성기 {r.age.growthEnd + 1}~{r.age.primeEnd}세 · 은퇴 무렵 {r.age.retire}세" +
                (r.wageMult != 1 ? $"\n<color={dim}>주급</color> ×{r.wageMult}" : "");
        }

        public static string StyleText(string id)
        {
            var s = Data.Style(id);
            if (s == null) return null;
            string strong = string.Join(", ", s.counters.Select(c => Data.Style(c)?.name ?? c));
            string weak = string.Join(", ", Data.Styles.Where(o => o.counters.Contains(id)).Select(o => o.name));
            return $"<b>{s.name}</b>\n{s.desc}" +
                (strong.Length > 0 ? $"\n<color={Theme.Hex(Theme.Good)}>유리한 상대</color> {strong}" : "") +
                (weak.Length > 0 ? $"\n<color=#d9a29b>불리한 상대</color> {weak}" : "");
        }
    }
}
