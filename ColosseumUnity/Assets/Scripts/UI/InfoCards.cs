// 하이퍼링크 카드 내용 (FM처럼 이름을 누르면 그 대상의 정보): 나라·루두스·대회·장비·종족·스타일.
// 카드 안의 이름도 링크라 계속 따라 들어갈 수 있다 (InfoPopup이 뒤로 가기를 기억한다).
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;

namespace Colosseum.UI
{
    public class InfoCard
    {
        public string title, sub, body;
        /// <summary>장비 카드면 그 장비 id (큰 아이콘)</summary>
        public string iconItem;
        /// <summary>기술 카드처럼 장비가 아닌 그림 (있으면 iconItem 대신)</summary>
        public UnityEngine.Sprite iconSprite;
        /// <summary>아래 이동 버튼 (없으면 숨김)</summary>
        public string actionLabel;
        public Action action;
    }

    public static class InfoCards
    {
        static readonly Dictionary<string, string> PersonalityNames = new()
        {
            ["aggressive"] = "공격형", ["stable"] = "안정형", ["grudge"] = "원한형", ["merchant"] = "상인형", ["developer"] = "육성형",
        };

        static string Dim => Theme.Hex(Theme.TextDim);
        static string Faint => Theme.Hex(Theme.TextFaint);
        static string Torch => Theme.Hex(Theme.Torch);
        static string Head(string t) => $"\n<color={Theme.Hex(Theme.BronzeHi)}><b>◆ {t}</b></color>\n";

        /// <summary>링크 id로 카드를 만든다. 모르는 링크면 null</summary>
        public static InfoCard For(GameState s, string link)
        {
            int i = link.IndexOf(':');
            if (i < 0) return null;
            string kind = link.Substring(0, i), id = link.Substring(i + 1);
            return kind switch
            {
                "nation" => Nation(s, id),
                "ludus" => s.ludi.TryGetValue(id, out var l) ? Ludus(s, l) : null,
                "comp" => WithAwards(s, id, Comp(s, id)),
                "item" => Item(id),
                "race" or "racial" => Data.Race(id) == null ? null : new InfoCard { title = Data.Race(id).name, sub = "종족", body = Tips.RaceText(id) },
                "style" => Data.Style(id) == null ? null : new InfoCard { title = Data.Style(id).name, sub = "전투 스타일", body = Tips.StyleText(id) + StyleGear(id) },
                "skill" => Skill(s, id),
                "trait" => Data.Trait(id) == null ? null : new InfoCard { title = Data.Trait(id).name, sub = "특성", body = Tips.TraitTip(id).Substring(Tips.TraitTip(id).IndexOf('\n') + 1) },
                "status" => SkillBook.Statuses.TryGetValue(id, out var st) ? new InfoCard { title = st.name, sub = "상태 이상", body = Tips.StatusTip(id).Substring(Tips.StatusTip(id).IndexOf('\n') + 1), iconSprite = UnityEngine.Resources.Load<UnityEngine.Sprite>("Art/icon/" + st.icon) } : null,
                "dungeon" => Dungeon(s, id),
                "person" => s.staff.TryGetValue(id, out var person) ? Person(s, person) : null,
                "awards" => AwardsCard(s, id),
                "youth" => YouthTrialCard(s),
                _ => null,
            };
        }

        // ── 모래 시험 (시즌 중반 학당 선발) ──

        static InfoCard YouthTrialCard(GameState s)
        {
            var me = s.ludi[s.playerLudusId];
            var list = (me.youthTrial ?? new List<string>()).Where(s.gladiators.ContainsKey).Select(id => s.gladiators[id]).ToList();
            if (list.Count == 0) return null;
            string until = me.youthTrialUntil is double u ? Calendar.DateLabel(Calendar.FromAbs(u).day) : "";
            var body = string.Join("\n", list.OrderByDescending(g => g.hidden.pa).Select(g =>
            {
                var pr = Scouting.PaRange(s, g);
                return $"{Tips.Name(g)}  <color={Faint}>{Tips.Race(g.race)} · {g.age}세 · {Tips.Style(g.style)}</color>  " +
                       $"현재 <color={Torch}>{Widgets.CaStars(s, g)}</color>  잠재 <color={Torch}>{Widgets.RangeStars(pr.lo, pr.hi)}</color>";
            }));
            return new InfoCard
            {
                title = "모래 시험", sub = $"찾아온 아이 {list.Count}명 · {until}까지 서약을 받을 아이를 고르세요",
                body = body + $"\n\n<color={Faint}>유소년 학당 화면에서 아이마다 '서약 받기'로 주급·기간·계약금 같은 조건을 정해 제안합니다. 답은 하루이틀 뒤 서판으로 오고, 협상이 깨지면 그 아이는 돌아갑니다. 학당 정원 {me.youth.Count}/{Youth.YouthCapacity(me)}명. 고르지 않은 아이는 시험이 끝나는 날 돌아갑니다.</color>",
                iconSprite = UnityEngine.Resources.Load<UnityEngine.Sprite>("Art/icon/menu/youth"),
                actionLabel = "유소년 학당으로", action = () => GameController.I.Go("youth"),
            };
        }

        // ── 수상 (시즌 시상식 · 검투사가 받은 상) ──

        /// <summary>수상 한 줄: 상 이름 · 수상자(링크) · 소속 · 기록</summary>
        public static string AwardWho(GameState s, AwardEntry a)
        {
            if (a.gladiatorId != null)
            {
                string name = s.gladiators.TryGetValue(a.gladiatorId, out var g) ? Tips.Name(g) : a.name;
                return $"{name} <color={Faint}>({(a.ludusId != null ? Tips.Ludus(a.ludusId, a.ludusName) : "무소속")})</color>";
            }
            return a.ludusId != null ? Tips.Ludus(a.ludusId, a.ludusName) : a.nation != null ? $"{Tips.Nation(a.nation)} 대표팀" : "";
        }

        static string AwardLine(GameState s, AwardEntry a) =>
            $"<color={Torch}>★</color> {Awards.KindNames[a.kind]}  {AwardWho(s, a)}" + (a.note != null ? $"  <color={Faint}>{a.note}</color>" : "");

        static InfoCard AwardsCard(GameState s, string id)
        {
            var all = s.awards ?? new List<AwardEntry>();
            if (id.StartsWith("g:"))
            {
                string gid = id.Substring(2);
                var mine = all.Where(a => a.gladiatorId == gid).OrderByDescending(a => a.season).ToList();
                string name = s.gladiators.TryGetValue(gid, out var g) ? g.name : mine.FirstOrDefault()?.name ?? "";
                return new InfoCard
                {
                    title = name, sub = $"받은 상 {mine.Count}개",
                    body = mine.Count == 0 ? $"<color={Faint}>아직 받은 상이 없습니다</color>"
                        : string.Join("\n", mine.Select(a => $"시즌 {a.season}  <color={Torch}>★</color> {CompLinkOf(a.comp)} {AwardKindLink(a)}" + (a.note != null ? $"  <color={Faint}>{a.note}</color>" : ""))),
                    iconSprite = UnityEngine.Resources.Load<UnityEngine.Sprite>("Art/icon/award"),
                    actionLabel = mine.Count > 0 ? "역대 수상자 보기" : null,
                    action = mine.Count > 0 ? () => GameController.I.OpenHallAwards(mine[0].comp) : null,
                };
            }
            // 대회 시상: comp:<대회 키>:<시즌>
            if (id.StartsWith("comp:"))
            {
                int cut = id.LastIndexOf(':');
                string comp = id.Substring(5, cut - 5);
                if (!int.TryParse(id.Substring(cut + 1), out int cs)) return null;
                var got = all.Where(a => a.season == cs && a.comp == comp).OrderBy(a => Array.IndexOf(new[] { "best", "top", "rookie", "lanista" }, a.kind)).ToList();
                return new InfoCard
                {
                    title = $"{Awards.CompName(comp)} 시상", sub = $"시즌 {cs} · 최우수 검투사 · 최다 처치 · 최우수 신인 · 올해의 라니스타",
                    body = got.Count == 0 ? $"<color={Faint}>수상 기록이 없습니다</color>" : CompLinkOf(comp) + "\n\n" + string.Join("\n", got.Select(a => AwardLine(s, a))),
                    iconSprite = UnityEngine.Resources.Load<UnityEngine.Sprite>("Art/icon/award"),
                    actionLabel = "역대 수상자 보기", action = () => GameController.I.OpenHallAwards(comp),
                };
            }
            if (!id.StartsWith("season:") || !int.TryParse(id.Substring(7), out int season)) return null;
            var list = all.Where(a => a.season == season).ToList();
            string Group(string head, IEnumerable<AwardEntry> xs)
            {
                var l = xs.ToList();
                return l.Count == 0 ? "" : Head(head) + string.Join("\n", l.Select(a => AwardLine(s, a))) + "\n";
            }
            // 시즌 시상식은 세계 최고와 나라별 최우수만 (대회별 상은 대회가 끝날 때 따로 띄운다)
            var body = Group("세계 올해의 검투사", list.Where(a => a.kind == "world_best"));
            body += list.Any(a => a.kind == "nation_best")
                ? Head("나라별 올해의 검투사") + string.Join("\n", list.Where(a => a.kind == "nation_best").Select(a => $"{Tips.Nation(a.comp.Substring(7), true)}  {AwardWho(s, a)}  <color={Faint}>{a.note}</color>")) + "\n"
                : "";
            body += $"\n<color={Faint}>대회별 수상은 비석의 언덕 '수상' 탭에서 볼 수 있습니다.</color>";
            return new InfoCard
            {
                title = $"시즌 {season} 시상식", sub = "세계 올해의 검투사 · 나라별 올해의 검투사",
                body = !list.Any(a => a.kind is "world_best" or "nation_best") ? $"<color={Faint}>이 시즌에는 수상 기록이 없습니다</color>" : body.TrimStart('\n'),
                iconSprite = UnityEngine.Resources.Load<UnityEngine.Sprite>("Art/icon/award"),
                actionLabel = "역대 수상자 보기", action = () => GameController.I.OpenHallAwards("world"),
            };
        }

        /// <summary>상 이름 링크: 누르면 그 대회·시즌 수상자 카드 (거기서 역대 수상자로 이어진다)</summary>
        public static string AwardKindLink(AwardEntry a) => $"<link=\"awards:comp:{a.comp}:{a.season}\">{Awards.KindNames[a.kind]}</link>";

        /// <summary>대회 카드 끝에 최근 수상자를 붙인다. 상 이름을 누르면 수상자 카드, 거기서 역대 수상자 표로 간다</summary>
        static InfoCard WithAwards(GameState s, string id, InfoCard card)
        {
            if (card == null || s.awards == null) return card;
            string comp = id == "cup" ? "pantheon" : id;
            var got = s.awards.Where(a => a.comp == comp).ToList();
            if (got.Count == 0) return card;
            int last = got.Max(a => a.season);
            card.body += Head($"최근 수상자 · 시즌 {last}") + string.Join("\n", got.Where(a => a.season == last).Select(a =>
                $"<color={Torch}>★</color> {AwardKindLink(a)}  {AwardWho(s, a)}"));
            return card;
        }

        /// <summary>대회 키(league:나라:부 …)를 대회 링크로</summary>
        public static string CompLinkOf(string comp)
        {
            var p = comp.Split(':');
            return p[0] switch
            {
                "league" => Tips.League(p[1], int.Parse(p[2])),
                "natcup" => Tips.NatCup(p[1]),
                "pantheon" => Tips.Cup(),
                "olympica" => Tips.Olympica(),
                "nation" => $"{Tips.Nation(p[1], true)} 시즌 시상",
                _ => "대륙 시즌 시상",
            };
        }

        // ── 식솔 (FM 직원 프로필처럼: 역할, 평점, 세부 능력치 14개) ──

        static InfoCard Person(GameState s, Staff p)
        {
            string dim = Theme.Hex(Theme.TextDim), torch = Theme.Hex(Theme.Torch);
            var main = new HashSet<string>(StaffLogic.RoleWeights[p.role].Select(x => x.key));
            string Group(string label, params string[] keys) =>
                $"<color={dim}>{label}</color>\n" + string.Join("\n", keys.Select(k =>
                {
                    int v = StaffLogic.Attr(p, k);
                    string num = main.Contains(k) ? $"<color={torch}><b>{v}</b></color>" : v.ToString();
                    return $"  {Tips.StaffAttr(k)}  {num}";
                }));
            var me = s.ludi[s.playerLudusId];
            bool mine = me.staff.Contains(p.id);
            string where = mine ? $"{Tips.Ludus(me)}의 식솔" : s.staffMarket.Contains(p.id) ? "고용할 수 있는 후보" : "";
            string body =
                $"<color={dim}>역할</color> {Tips.StaffRole(p.role)} · <color={dim}>평점</color> {StateUtil.Num(p.rating)} · <color={dim}>주급</color> {StateUtil.Num(p.wage)} D · {StateUtil.Num(p.age)}세 · {Tips.Race(p.race)}" +
                (where.Length > 0 ? $"\n{where}" : "") +
                (p.attrs == null ? $"\n\n<color={dim}>세부 능력치가 없는 옛 식솔입니다 (평점 하나로 일합니다).</color>" :
                $"\n\n{Group("지도", "coachPhy", "coachTec", "coachMen", "coachTac")}\n{Group("판단", "judgeA", "judgeP")}\n{Group("의료", "physio", "sci")}\n{Group("사람", "motiv", "disc", "manage", "det")}\n{Group("거래", "nego", "network")}" +
                $"\n\n<color={dim}>역할에 중요한 능력치는 굵게 표시합니다. 이름에 마우스를 올리면 무슨 일에 쓰이는지 볼 수 있습니다.</color>");
            return new InfoCard { title = p.name, sub = $"식솔 · {StaffLogic.RoleNames[p.role]}", body = body };
        }

        // ── 던전 ──

        static InfoCard Dungeon(GameState s, string id)
        {
            var d = DungeonLogic.Dungeons.FirstOrDefault(x => x.id == id);
            if (d == null) return null;
            string dim = Theme.Hex(Theme.TextDim);
            var foes = string.Join(", ", d.foes.Select(f => $"{f.name} ({Tips.Race(f.race)})"));
            string body = $"{d.desc}\n\n<color={dim}>층</color> {d.floors}층 · <color={dim}>원정</color> {d.days}일 · <color={dim}>적 수준</color> {Widgets.Stars(DungeonLogic.FoeCa(d))}" +
                          $"\n<color={dim}>적</color> {foes}\n<color={dim}>우두머리</color> {d.boss.name} ({Tips.Race(d.boss.race)})" +
                          $"\n<color={dim}>전리품</color> 보통 {d.loot[0]}% · 정교 {d.loot[1]}% · 명품 {d.loot[2]}% · 전설 {d.loot[3]}%";
            return new InfoCard { title = d.name, sub = $"던전 · {d.tier}등급", body = body, actionLabel = "던전으로", action = () => GameController.I.Go("dungeon") };
        }

        // ── 기술 (기술 테이블) ──

        static readonly Dictionary<string, string> LearnNames = new()
        {
            ["WeaponProf"] = "숙련", ["Matches"] = "출전", ["Wins"] = "승리", ["Kills"] = "살해", ["Popularity"] = "인기", ["Age"] = "나이",
        };

        /// <summary>표의 무기 계열 키(TridentNet) → 이름(삼지창·그물)</summary>
        static string ClassName(string key)
        {
            string snake = string.Concat(key.Select((ch, i) => i > 0 && char.IsUpper(ch) ? "_" + char.ToLower(ch) : char.ToLower(ch).ToString()));
            return Attrs.WeaponClassNames.TryGetValue(snake, out var n) ? n : key;
        }

        static string LearnLine(SkillBook.LearnRow c)
        {
            string op = c.op == "GE" ? "이상" : c.op == "LE" ? "이하" : "";
            switch (c.cond)
            {
                case "WeaponProf":
                    return $"{(c.key == "Any" ? "주무기" : ClassName(c.key))} 숙련 {c.value} {op}";
                case "Attr": return $"{(Attrs.Names.TryGetValue(c.key.ToLowerInvariant(), out var an) ? an : c.key)} {c.value} {op}";
                case "Race": return $"{Tips.Race(c.key.ToLowerInvariant())} 종족";
                case "Style": return $"{Tips.Style(c.key)} 스타일";
                case "Trait": return $"특성 {Tips.Trait(c.key)}";
                case "HasSkill": return $"{Tips.Skill("skill." + c.key)}을(를) 먼저 익힘";
                default: return $"{(LearnNames.TryGetValue(c.cond, out var ln) ? ln : c.cond)} {c.value} {op}";
            }
        }

        static InfoCard Skill(GameState s, string id)
        {
            var r = SkillBook.Row(id);
            if (r == null) return Data.Skill(id) == null ? null : new InfoCard { title = Data.Skill(id).name, sub = "기술" };
            string faint = Theme.Hex(Theme.TextFaint), dim = Theme.Hex(Theme.TextDim);
            var lines = new List<string>();
            // 툴팁과 같은 설명 (첫 줄 제목은 카드 제목으로)
            var tip = Tips.SkillTip(id);
            lines.Add(tip.Substring(tip.IndexOf('\n') + 1));
            if (r.weapon != "Any" || r.race != "Any") lines.Add($"\n<color={dim}>필요</color> {(r.weapon != "Any" ? ClassName(r.weapon) + " 무기 " : "")}{(r.race != "Any" ? Tips.Race(r.race.ToLowerInvariant()) + " 전용" : "")}");
            if (SkillBook.Learns.TryGetValue(r.learnGroup, out var conds))
                lines.Add($"\n<color={dim}>습득 조건</color> (모두 채우면 달마다 {(r.learnRate >= 1 ? "바로" : $"{r.learnRate * Math.Pow(SkillBook.C("LearnTierPenalty", 0.5), r.tier - 1):0%} 확률")})\n" + string.Join("\n", conds.Select(c => "· " + LearnLine(c))));
            if (s != null && s.ludi.TryGetValue(s.playerLudusId, out var me))
            {
                var mine = me.gladiators.Select(g => s.gladiators[g]).Where(g => g.alive).ToList();
                var has = mine.Where(g => g.skills.active.Contains(id) || g.skills.passive.Contains(id)).ToList();
                var ready = mine.Where(g => !has.Contains(g) && SkillBook.Meets(g, r)).ToList();
                lines.Add($"\n<color={dim}>우리 검투사</color> 익힘: {(has.Count > 0 ? string.Join(", ", has.Select(Tips.Name)) : $"<color={faint}>없음</color>")}" +
                          $"\n조건 충족: {(ready.Count > 0 ? string.Join(", ", ready.Select(Tips.Name)) : $"<color={faint}>없음</color>")}");
            }
            return new InfoCard { title = r.name, sub = $"기술 · {(r.type == "Active" ? "능동" : r.type == "Reaction" ? "반응" : "상시")} · {new string('★', r.tier)}", body = string.Join("\n", lines), iconSprite = Tips.SkillIcon(id) };
        }

        // ── 나라 ──

        static InfoCard Nation(GameState s, string id)
        {
            var n = Data.Nation(id);
            var t1 = Economy.TierRanking(s, 1, id);
            var body = $"<color={Dim}>{n.lore}</color>\n";
            if (n.ruleText.Count > 0) body += Head("리그 특징") + string.Join("\n", n.ruleText.Select(r => "· " + r)) + "\n";
            body += Head($"{Tips.League(id, 1)} 순위") + string.Join("\n", t1.Select((l, k) => $"{k + 1}. {Tips.Ludus(l)}  <color={Faint}>RP {l.rp}</color>"));
            if (id == s.nation) body += $"\n{Tips.League(id, 2)}";
            var champs = (s.honours ?? new List<HonourEntry>()).Where(h => h.kind == "league" && h.nation == id && h.tier == 1).OrderByDescending(h => h.season).Take(5).ToList();
            if (champs.Count > 0) body += "\n" + Head("최근 1부 우승") + string.Join("\n", champs.Select(h => $"시즌 {h.season}  <color={Torch}>★</color> {Tips.Ludus(h.winnerId, h.winnerName)}"));
            if (s.coefficients.TryGetValue(id, out var co) && co.Count > 0) body += $"\n\n<color={Dim}>판테온 컵 나라 계수</color> {co[co.Count - 1]}";
            return new InfoCard
            {
                title = n.name + (id == s.nation ? $" <size=60%><color={Dim}>(우리 나라)</color></size>" : ""),
                sub = $"{n.motif} · 수도 {n.capital} · 경기장 {n.arena}",
                body = body,
                actionLabel = "대륙 정세로", action = () => GameController.I.Go("world"),
            };
        }

        // ── 루두스 ──

        static InfoCard Ludus(GameState s, Ludus l)
        {
            var table = Economy.TierRanking(s, l.tier, l.nation);
            int rank = table.FindIndex(x => x.id == l.id) + 1;
            double rep = Reputation.LudusReputation(s, l);
            var body = $"<color={Dim}>전적</color> {l.record.wins}승 {l.record.losses}패" +
                (l.isPlayer ? "" : $"    <color={Dim}>성향</color> {PersonalityNames.GetValueOrDefault(l.aiPersonality ?? "", "—")}") +
                $"    <color={Dim}>시설</color> 경기장 {l.facilities.arena}단계 · 훈련장 {l.facilities.training}단계\n";
            var trophies = Honours.TrophiesOf(s, l.id);
            body += Head("우승컵") + (trophies.Count == 0 ? $"<color={Faint}>없습니다</color>"
                : string.Join("\n", trophies.Select(t => $"<color={Torch}>★</color> {CompLink(t.name)} ×{t.seasons.Count} <color={Faint}>(시즌 {string.Join(", ", t.seasons)})</color>")));
            var people = l.gladiators.Select(id => s.gladiators.TryGetValue(id, out var g) ? g : null).Where(g => g != null && g.alive)
                .OrderByDescending(g => Scouting.CaRange(s, g).mid).ToList();
            body += "\n" + Head($"검투사 {people.Count}명") + string.Join("\n", people.Select(g =>
            {
                var ca = (lo: Scouting.CaRange(s, g).lo, hi: Scouting.CaRange(s, g).hi);
                return $"{Tips.Name(g)} <color={Faint}>{Tips.Race(g.race)} · {Tips.Style(g.style)} · {g.age}세</color>  {Widgets.RangeStars(ca.lo, ca.hi)}";
            }));
            var seasons = (l.seasons ?? new List<LudusSeason>()).OrderByDescending(x => x.season).Take(4).ToList();
            if (seasons.Count > 0)
                body += "\n" + Head("최근 시즌") + string.Join("\n", seasons.Select(x =>
                    $"시즌 {x.season}  {Tips.League(x.nation ?? l.nation, x.tier)} {x.rank}위 · {x.wins}-{x.losses}" +
                    (x.result == "promoted" ? $" <color={Theme.Hex(Theme.Good)}>승격</color>" : x.result == "relegated" ? " <color=#d9a29b>강등</color>" : x.rank == 1 ? $" <color={Torch}>우승</color>" : "")));
            string id = l.id;
            return new InfoCard
            {
                title = l.name + (l.isPlayer ? $" <size=60%><color={Dim}>(우리)</color></size>" : ""),
                sub = $"{Tips.Nation(l.nation)} · {Tips.League(l.nation, l.tier)} {rank}위 · RP {l.rp} · 명성 {rep} ({Reputation.ReputationNames(rep)})",
                body = body,
                actionLabel = "연혁 보기", action = () => GameController.I.OpenHall(id),
                iconSprite = Widgets.LudusCrest(l),
            };
        }

        /// <summary>우승컵 이름을 대회 링크로</summary>
        public static string CompLink(string trophyName)
        {
            if (trophyName == Honours.CupName) return Tips.Cup();
            if (trophyName == OlympicaLogic.Name) return Tips.Olympica();
            foreach (var n in Data.Nations)
            {
                if (NatCupLogic.Name(n.id) == trophyName) return Tips.NatCup(n.id);
                for (int t = 1; t <= FmRules.MaxTier; t++)
                    if (Honours.LeagueName(n.id, t) == trophyName) return Tips.League(n.id, t);
            }
            return trophyName;
        }

        /// <summary>대회 이름에 맞는 트로피 그림 (없으면 null)</summary>
        public static UnityEngine.Sprite TrophySprite(string compName)
        {
            string key = null;
            if (compName == Honours.CupName) key = "pantheon";
            else if (compName == OlympicaLogic.Name) key = "olympica";
            else
                foreach (var n in Data.Nations)
                {
                    if (NatCupLogic.Name(n.id) == compName) key = "natcup_" + n.id;
                    for (int t = 1; t <= FmRules.MaxTier; t++)
                        if (Honours.LeagueName(n.id, t) == compName) key = "league_" + t;
                }
            return key == null ? null : UnityEngine.Resources.Load<UnityEngine.Sprite>("Art/icon/trophy/" + key);
        }

        // ── 대회 ──

        static InfoCard Comp(GameState s, string id)
        {
            var hist = s.honours ?? new List<HonourEntry>();
            string Past(IEnumerable<HonourEntry> list)
            {
                var l = list.OrderByDescending(h => h.season).Take(10).ToList();
                return Head("역대 우승") + (l.Count == 0 ? $"<color={Faint}>아직 기록이 없습니다</color>"
                    : string.Join("\n", l.Select(h => $"시즌 {h.season}  <color={Torch}>★</color> {Tips.Ludus(h.winnerId, h.winnerName)}" +
                        (h.runnerUpName != null ? $"  <color={Faint}>준우승</color> {Tips.Ludus(h.runnerUpId, h.runnerUpName)}" : ""))));
            }
            if (id == "cup")
            {
                var cup = s.cup;
                string now = cup == null ? $"<color={Faint}>이번 시즌 컵은 아직 열리지 않았습니다</color>"
                    : !string.IsNullOrEmpty(cup.champion) && s.ludi.TryGetValue(cup.champion, out var ch) ? $"시즌 {cup.season} 우승: <color={Torch}>★</color> {Tips.Ludus(ch)}"
                    : $"시즌 {cup.season} 진행 중 · 출전 {cup.teams.Count}곳: " + string.Join(", ", cup.teams.Where(t => s.ludi.ContainsKey(t)).Select(t => Tips.Ludus(s.ludi[t])));
                return new InfoCard
                {
                    title = Honours.CupName, sub = "국내 컵 우승 팀과 나라별 1부 1·2위가 겨루는 대륙 대회",
                    body = now + "\n" + Past(hist.Where(h => h.kind == "cup")),
                    iconSprite = TrophySprite(Honours.CupName),
                    actionLabel = "판테온 컵으로", action = () => GameController.I.Go("cup"),
                };
            }
            if (id == "olympica")
            {
                var o = OlympicaLogic.Current(s) ?? s.olympica;
                string now = o == null ? $"<color={Faint}>다음 대회는 시즌 {OlympicaLogic.NextSeason(s.season)}입니다</color>"
                    : !string.IsNullOrEmpty(o.champion) ? $"시즌 {o.season} 우승: <color={Torch}>★</color> {Tips.Nation(o.champion)}"
                    : $"시즌 {o.season} 진행 중";
                var past = hist.Where(h => h.kind == "olympica").OrderByDescending(h => h.season).Take(10)
                    .Select(h => $"시즌 {h.season}  <color={Torch}>★</color> {Tips.Nation(h.winnerId)}  <color={Faint}>준우승</color> {Tips.Nation(h.runnerUpId)}").ToList();
                return new InfoCard
                {
                    title = OlympicaLogic.Name, sub = "4년에 한 번 여섯 나라 대표가 겨루는 국가 대항전",
                    body = now + "\n" + Head("역대 우승") + (past.Count == 0 ? $"<color={Faint}>아직 기록이 없습니다</color>" : string.Join("\n", past)),
                    iconSprite = TrophySprite(OlympicaLogic.Name),
                    actionLabel = "올림피카 컵으로", action = () => GameController.I.Go("olympica"),
                };
            }
            var parts = id.Split(':');
            if (parts.Length == 2 && parts[0] == "natcup")
            {
                string n = parts[1];
                var c = NatCupLogic.Of(s, n);
                string now = c == null ? $"<color={Faint}>정규 리그가 끝나면 시작합니다</color>"
                    : !string.IsNullOrEmpty(c.champion) ? $"시즌 {c.season} 우승: <color={Torch}>★</color> {Tips.Ludus(s.ludi[c.champion])}"
                    : $"시즌 {c.season} 진행 중";
                return new InfoCard
                {
                    title = NatCupLogic.Name(n), sub = $"{Tips.Nation(n)}의 1~4부 루두스 40곳이 모두 나오는 컵",
                    body = now + "\n" + Past(hist.Where(h => h.kind == "natcup" && h.nation == n)),
                    iconSprite = TrophySprite(NatCupLogic.Name(n)),
                    actionLabel = "국내 컵으로", action = () => GameController.I.Go("natcup"),
                };
            }
            if (parts.Length != 3 || parts[0] != "league") return null;
            string nation = parts[1];
            int tier = int.TryParse(parts[2], out var tt) ? tt : 1;
            var table = Economy.TierRanking(s, tier, nation);
            var body = Head("지금 순위") + string.Join("\n", table.Select((l, k) => $"{k + 1}. {Tips.Ludus(l)}  <color={Faint}>{l.record.wins}-{l.record.losses} · RP {l.rp}</color>"));
            body += "\n" + Past(hist.Where(h => h.kind == "league" && h.nation == nation && h.tier == tier));
            return new InfoCard
            {
                title = Honours.LeagueName(nation, tier), sub = $"{Tips.Nation(nation)}의 {tier}부 리그 · {table.Count}곳",
                body = body,
                iconSprite = TrophySprite(Honours.LeagueName(nation, tier)),
                actionLabel = "서열표로", action = () => GameController.I.Go("league"),
            };
        }

        // ── 장비 ──

        static InfoCard Item(string id)
        {
            var (bid, q) = Data.SplitItemId(id);
            var w = Data.Weapon(id);
            var a = Data.Armor(id);
            if (w == null && a == null) return null;
            string N(double x) => StateUtil.Num(x);
            var lines = new List<string>();
            if (w != null)
            {
                lines.Add($"<color={Dim}>피해</color> {N(w.damage)}    <color={Dim}>속도</color> {N(w.speed)}    <color={Dim}>사거리</color> {N(w.range)}    <color={Dim}>필요 근력</color> {N(w.reqStr)}");
                lines.Add($"<color={Dim}>관통</color> {N(JsMath.Round(w.pierce * 100))}%    <color={Dim}>치명</color> {N(JsMath.Round(w.crit * 100))}%" + (w.twoHanded ? "    양손" : "") + (w.offhandOnly ? "    보조 손 전용" : ""));
                var extra = new List<string>();
                if ((w.stun ?? 0) != 0) extra.Add($"기절 {N(JsMath.Round(w.stun.Value * 100))}%");
                if ((w.blockBypass ?? 0) != 0) extra.Add($"막기 우회 {N(JsMath.Round(w.blockBypass.Value * 100))}%");
                if ((w.boundBonus ?? 0) != 0) extra.Add($"묶인 상대에게 피해 +{N(JsMath.Round(w.boundBonus.Value * 100))}%");
                if ((w.ammo ?? 0) != 0) extra.Add($"탄 {N(w.ammo.Value)}발");
                if ((w.magicBonus ?? 0) != 0) extra.Add($"주술 +{N(JsMath.Round(w.magicBonus.Value * 100))}%");
                if (extra.Count > 0) lines.Add(string.Join(" · ", extra));
            }
            else
            {
                lines.Add(GearLogic.ItemStats(id));
                var extra = new List<string>();
                if ((a.staCost ?? 0) != 0) extra.Add($"공격할 때 기력 +{N(JsMath.Round(a.staCost.Value * 100))}%");
                if ((a.accPenalty ?? 0) != 0) extra.Add($"명중 -{N(a.accPenalty.Value)}");
                if ((a.netResist ?? 0) != 0) extra.Add($"그물 버티기 {N(JsMath.Round(a.netResist.Value * 100))}%");
                if ((a.crowd ?? 0) != 0) extra.Add($"관중 열기 +{N(JsMath.Round(a.crowd.Value * 100))}%");
                if (extra.Count > 0) lines.Add(string.Join(" · ", extra));
            }
            lines.Add($"<color={Dim}>기준가</color> {N(GearLogic.ItemPrice(id))} D");
            var uq = Uniques.Of(id);
            if (uq != null)
            {
                lines.Add(Head(uq.grade >= 3 ? "전설의 힘" : "고유 효과") + string.Join("\n", uq.effects.Select(e => $"<color={Torch}>◆</color> {Uniques.Describe(e)}")));
                if (!string.IsNullOrEmpty(uq.lore)) lines.Add($"\n<i><color={Dim}>“{uq.lore}”</color></i>");
                lines.Add($"\n<color={Faint}>바탕 장비</color> {Tips.Item(bid)}");
            }
            var users = Data.Styles.Where(st => st.loadout != null && new[] { st.loadout.main, st.loadout.off, st.loadout.armor, st.loadout.helmet }.Contains(bid)).ToList();
            string body = string.Join("\n", lines) + (users.Count > 0 ? "\n" + Head("기본 장비로 쓰는 스타일") + string.Join(", ", users.Select(st => Tips.Style(st.id))) : "");
            string kind = GearLogic.ItemKindNames.GetValueOrDefault(GearLogic.ItemKind(id) ?? "", "장비");
            return new InfoCard
            {
                title = Widgets.ItemLabel(id), iconItem = id,
                sub = $"{kind}" + (q > 0 ? $" · {Widgets.GradeTag(id)}" : " · 보통 품질") + (uq != null ? " · 고유 장비" : ""),
                body = body,
                actionLabel = "장비 시장으로", action = () => GameController.I.Go("market"),
            };
        }

        /// <summary>스타일 카드 아래: 기본 장비 링크</summary>
        static string StyleGear(string id)
        {
            var lo = Data.Style(id)?.loadout;
            if (lo == null) return "";
            var items = new[] { lo.main, lo.off, lo.armor, lo.helmet }.Where(x => !string.IsNullOrEmpty(x)).Select(Tips.Item).ToList();
            return items.Count == 0 ? "" : "\n" + Head("기본 장비") + string.Join(", ", items);
        }
    }
}
