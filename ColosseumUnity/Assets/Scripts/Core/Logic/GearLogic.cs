// 장비: 루두스 창고, 장비 시장 매물, 착용 규칙, 사투 노획 (src/core/gear.ts).
// 장비 id 뒤의 '+숫자'가 품질이다 (Data.SplitItemId). 시장엔 명품까지, 전설은 던전에서만 나온다.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class GearLogic
    {
        /// <summary>slot: main | off | armor | helmet</summary>
        public static readonly Dictionary<string, string> SlotNames = new() { ["main"] = "주무기", ["off"] = "보조", ["armor"] = "갑옷", ["helmet"] = "투구" };

        /// <summary>버려도 아깝지 않은 기본 장비: 벗기면 창고에 남기지 않는다</summary>
        static readonly HashSet<string> Basic = new() { "loincloth", "cestus" };
        /// <summary>빈손일 때 대신 드는 것</summary>
        static readonly Dictionary<string, string> Fallback = new() { ["main"] = "cestus", ["armor"] = "loincloth" };

        public static readonly Dictionary<string, string> ItemKindNames = new() { ["weapon"] = "무기", ["armor"] = "갑옷", ["helmet"] = "투구", ["shield"] = "방패" };

        /// <summary>weapon | armor | helmet | shield, 아니면 null</summary>
        public static string ItemKind(string id)
        {
            if (Data.Weapon(id) != null) return "weapon";
            var a = Data.Armor(id);
            return a != null ? a.slot : null;
        }

        public static string ItemName(string id) => Data.Weapon(id)?.name ?? Data.Armor(id)?.name ?? id;
        public static int QualityOf(string id) => Data.SplitItemId(id).Item2;
        public static readonly string[] QualityClass = { "q0", "q1", "q2", "q3" };
        public static string[] QualityNames => Data.QualityNames;

        static readonly double[] QualityPriceMult = { 1, 2.2, 5, 12 };

        /// <summary>장비 기준가: 성능에 비례하고 품질이 오를수록 가파르게 비싸진다</summary>
        public static double ItemPrice(string id)
        {
            var (b, q) = Data.SplitItemId(id);
            var w = Data.Weapon(b);
            var a = Data.Armor(b);
            double bs = 40;
            if (w != null) bs = 60 + w.damage * 14 + w.pierce * 300 + (w.magicBonus ?? 0) * 1600 + (w.stun ?? 0) * 900 + w.crit * 1200 + (w.offhandOnly ? 80 : 0);
            else if (a?.slot == "armor") bs = 50 + (a.defense ?? 0) * 70 + (a.crowd ?? 0) * 600;
            else if (a?.slot == "helmet") bs = 70 + (a.defense ?? 0) * 70 + (a.showBonus ?? 0) * 60 + (a.netResist ?? 0) * 300;
            else if (a?.slot == "shield") bs = 80 + (a.block ?? 0) * 900;
            return JsMath.Round((bs * QualityPriceMult[q]) / 10) * 10;
        }

        /// <summary>장비 한 줄 요약 (표에 쓴다)</summary>
        public static string ItemStats(string id)
        {
            string N(double x) => StateUtil.Num(x);
            var w = Data.Weapon(id);
            if (w != null)
                return string.Join(" · ", new[] { $"피해 {N(w.damage)}", $"속도 {N(w.speed)}", w.range != 0 ? $"사거리 {N(w.range)}" : "", w.twoHanded ? "양손" : "", w.offhandOnly ? "보조 전용" : "", $"근력 {N(w.reqStr)}" }.Where(s => !string.IsNullOrEmpty(s)));
            var a = Data.Armor(id);
            if (a == null) return "";
            if (a.slot == "shield") return $"막기 {N(JsMath.Round((a.block ?? 0) * 100))}%";
            string wt = a.weight switch { "light" => "경량", "medium" => "중량", "heavy" => "중장", _ => "" };
            return string.Join(" · ", new[]
            {
                $"방어 {N(a.defense ?? 0)}",
                !string.IsNullOrEmpty(a.weight) ? wt : "",
                (a.agiPenalty ?? 0) != 0 ? $"민첩 -{N(a.agiPenalty.Value)}" : "",
                (a.showBonus ?? 0) != 0 ? $"쇼맨십 +{N(a.showBonus.Value)}" : "",
            }.Where(s => !string.IsNullOrEmpty(s)));
        }

        public static List<string> ArmoryOf(GameState s) => s.armory ??= new List<string>();

        static string GetSlot(Equipment e, string slot) => slot switch
        {
            "main" => e.main, "off" => e.off, "armor" => e.armor, "helmet" => e.helmet, _ => null,
        };

        static void SetSlot(Equipment e, string slot, string v)
        {
            switch (slot)
            {
                case "main": e.main = v; break;
                case "off": e.off = v; break;
                case "armor": e.armor = v; break;
                case "helmet": e.helmet = v; break;
            }
        }

        /// <summary>이 장비를 이 칸에 낄 수 있는가 (안 되면 이유, 되면 null)</summary>
        public static string CannotEquip(Gladiator g, string slot, string id)
        {
            var w = Data.Weapon(id);
            var a = Data.Armor(id);
            switch (slot)
            {
                case "main":
                    if (w == null) return "무기가 아니다";
                    if (w.offhandOnly) return "보조 손 전용이다";
                    return null;
                case "off":
                    if (Data.Weapon(g.equipment.main)?.twoHanded == true) return "양손 무기를 들고 있다";
                    if (a?.slot == "shield") return null;
                    if (w != null && (w.offhandOnly || w.@class == "dagger")) return null;
                    return "방패나 보조 무기만 된다";
                case "armor":
                    if (a?.slot != "armor") return "갑옷이 아니다";
                    if (g.race == "minotaur" && a.weight == "light") return "미노타우로스 몸에 맞지 않는다";
                    return null;
                case "helmet":
                    return a?.slot == "helmet" ? null : "투구가 아니다";
            }
            return null;
        }

        /// <summary>장비 착용: 창고에서 꺼내 끼고, 원래 것은 창고로. item이 null이면 벗기만 한다</summary>
        public static string EquipItem(GameState s, string gId, string slot, string item)
        {
            s.gladiators.TryGetValue(gId, out var g);
            if (g == null || !g.alive) return "검투사가 없다";
            if (g.deployment != null) return "파견 중이라 장비를 바꿀 수 없다";
            var armory = ArmoryOf(s);
            if (item != null)
            {
                string why = CannotEquip(g, slot, item);
                if (why != null) return why;
                int at = armory.IndexOf(item);
                if (at < 0) return "창고에 없는 장비다";
                armory.RemoveAt(at);
            }
            void Stash(string o) { if (!string.IsNullOrEmpty(o) && !Basic.Contains(o)) armory.Add(o); }
            string old = GetSlot(g.equipment, slot);
            if (slot == "main" || slot == "armor") SetSlot(g.equipment, slot, item ?? Fallback[slot]);
            else SetSlot(g.equipment, slot, item);
            Stash(old);
            // 양손 무기를 들면 보조 손은 비운다
            if (slot == "main" && Data.Weapon(g.equipment.main)?.twoHanded == true && !string.IsNullOrEmpty(g.equipment.off))
            {
                Stash(g.equipment.off);
                g.equipment.off = null;
            }
            return null;
        }

        /// <summary>창고 장비 팔기: 기준가의 35%</summary>
        public static string SellItem(GameState s, string item)
        {
            var armory = ArmoryOf(s);
            int at = armory.IndexOf(item);
            if (at < 0) return "창고에 없다";
            armory.RemoveAt(at);
            Economy.AddMoney(s, s.ludi[s.playerLudusId], JsMath.Round(ItemPrice(item) * 0.35), $"{ItemName(item)} 판매");
            return null;
        }

        public static List<GearListing> GearMarketOf(GameState s) => s.gearMarket ??= new List<GearListing>();

        public static string BuyListing(GameState s, string listingId)
        {
            var market = GearMarketOf(s);
            var l = market.Find(x => x.id == listingId);
            if (l == null) return "이미 팔린 매물이다";
            var me = s.ludi[s.playerLudusId];
            if (me.treasury < l.price) return "금고가 모자란다";
            Economy.AddMoney(s, me, -l.price, $"{ItemName(l.item)} 구입 ({l.seller})");
            ArmoryOf(s).Add(l.item);
            s.gearMarket = market.Where(x => x != l).ToList();
            return null;
        }

        // ── 흥정과 위탁 판매 (Unity판 전용) ──

        /// <summary>흥정에 내놓은 장비는 기준가의 절반으로 쳐 준다 (그냥 팔면 35%). 중개인이 있으면 조금 더</summary>
        public const double TradeInRate = 0.5;
        /// <summary>중개인 위탁 수수료</summary>
        public const double AgentCommission = 0.1;

        static double AgentRating(GameState s) => StaffLogic.RatingOf(s, s.ludi[s.playerLudusId], "agent");

        public static double TradeInValue(GameState s, string item) => JsMath.Round(ItemPrice(item) * TradeInRate * StaffLogic.SellMult(AgentRating(s)));

        public static string HaggleKey(string listingId) => "gear:" + listingId;

        /// <summary>매물 흥정: 현금 + 창고 장비(맞교환)를 묶어 제안한다. 받아들이면 바로 거래된다</summary>
        public static HaggleResult HaggleListing(GameState s, Rng rng, string listingId, double cash, List<string> tradeIns)
        {
            var market = GearMarketOf(s);
            var l = market.Find(x => x.id == listingId);
            if (l == null) return new HaggleResult { status = "error", line = "이미 팔린 매물이다" };
            var me = s.ludi[s.playerLudusId];
            cash = Math.Max(0, cash);
            if (cash > me.treasury) return new HaggleResult { status = "error", line = "금고가 모자란다" };
            var armory = ArmoryOf(s);
            var left = armory.ToList();
            foreach (var t in tradeIns)
                if (!left.Remove(t)) return new HaggleResult { status = "error", line = $"창고에 {Josa.J(ItemName(t), "이/가")} 없다" };
            if (cash <= 0 && tradeIns.Count == 0) return new HaggleResult { status = "error", line = "아무것도 내놓지 않았다" };

            double r = AgentRating(s);
            string key = HaggleKey(listingId);
            Haggling.Open(s, rng, key, l.price, 0.04 + r * 0.005, 0.16 + r * 0.01, 100 + r * 1.5);
            double total = cash + tradeIns.Sum(t => TradeInValue(s, t));
            var res = Haggling.Offer(s, key, total);
            if (!res.Accepted) return res;

            if (cash > 0) Economy.AddMoney(s, me, -cash, $"{ItemName(l.item)} 구입 ({l.seller})");
            foreach (var t in tradeIns) armory.RemoveAt(armory.IndexOf(t));
            armory.Add(l.item);
            s.gearMarket = market.Where(x => x != l).ToList();
            if (tradeIns.Count > 0) res.line += $" ({Josa.J(string.Join(", ", tradeIns.Select(ItemName)), "을/를")} 넘겼다)";
            return res;
        }

        public static List<Consignment> ConsignmentsOf(GameState s) => s.consignments ??= new List<Consignment>();

        /// <summary>내놓을 때 권하는 값: 기준가의 80% (중개인이 있으면 조금 더)</summary>
        public static double SuggestedPrice(GameState s, string item) => JsMath.Round(ItemPrice(item) * 0.8 * StaffLogic.SellMult(AgentRating(s)) / 10) * 10;

        /// <summary>창고 장비를 시장에 내놓는다. 성공하면 null</summary>
        public static string Consign(GameState s, string item, double price, bool viaAgent)
        {
            var armory = ArmoryOf(s);
            if (!armory.Contains(item)) return "창고에 없다";
            if (price <= 0) return "값을 정하라";
            if (viaAgent && StaffLogic.StaffOf(s, s.ludi[s.playerLudusId], "agent") == null) return "중개인이 없다. 식솔에서 중개인을 들이거나 직접 내놓아라";
            armory.RemoveAt(armory.IndexOf(item));
            ConsignmentsOf(s).Add(new Consignment { id = $"C{s.nextId++}", item = item, price = price, viaAgent = viaAgent, listedAt = Today(s) });
            return null;
        }

        /// <summary>내놓은 장비를 거둬들인다</summary>
        public static void Withdraw(GameState s, string id)
        {
            var list = ConsignmentsOf(s);
            var c = list.Find(x => x.id == id);
            if (c == null) return;
            list.Remove(c);
            ArmoryOf(s).Add(c.item);
        }

        /// <summary>하루에 팔릴 확률: 기준가보다 쌀수록 빨리 팔린다. 중개인은 사는 사람을 더 잘 찾는다</summary>
        public static double DailySaleChance(GameState s, Consignment c)
        {
            double ratio = c.price / ItemPrice(c.item);
            double @base = c.viaAgent ? 0.12 + AgentRating(s) * 0.012 : 0.07;
            return Math.Clamp(@base * Math.Exp(-3 * (ratio - 0.7)), 0, 0.9);
        }

        /// <summary>위탁 매물 판매 (하루 진행). 내놓은 게 없으면 난수를 쓰지 않는다 (웹판과 같은 결과)</summary>
        public static void DailyConsignments(GameState s, Rng rng)
        {
            if (s.consignments == null || s.consignments.Count == 0) return;
            var me = s.ludi[s.playerLudusId];
            foreach (var c in s.consignments.ToList())
            {
                if (!rng.Chance(DailySaleChance(s, c))) continue;
                s.consignments.Remove(c);
                double fee = c.viaAgent ? JsMath.Round(c.price * AgentCommission) : 0;
                Economy.AddMoney(s, me, c.price - fee, $"{ItemName(c.item)} 판매{(c.viaAgent ? " (중개인)" : "")}");
                StateUtil.PushMessage(s, "info", $"장비가 팔렸다: {ItemName(c.item)}",
                    $"{c.price:N0} D에 팔렸다.{(fee > 0 ? $" 중개인 수수료 {fee:N0} D를 뗐다." : "")}");
            }
        }

        /// <summary>나라마다 잘 나오는 장비 (그 나라 상인 매물에 가중치)</summary>
        static readonly Dictionary<string, string[]> NationTaste = new()
        {
            ["aurelia"] = new[] { "gladius", "scutum", "muscle_cuirass", "crested_helm", "brimmed_helm", "manica" },
            ["dolgar"] = new[] { "hasta", "aspis", "plate", "open_helm", "warhammer", "greatsword" },
            ["karsea"] = new[] { "trident", "net", "sica", "parmula", "scale", "javelin" },
            ["kemer"] = new[] { "staff", "rune_charm", "sica", "scale", "hunting_spear", "pugio" },
            ["grommar"] = new[] { "greataxe", "battleaxe", "mace", "chainmail", "sealed_helm", "knuckles" },
            ["sylvaren"] = new[] { "hunting_spear", "rune_charm", "staff", "leather", "pugio", "longsword" },
        };
        static readonly string[] Sellers = { "떠돌이 대장장이", "군수 상인", "전리품 상인", "명장 공방", "항구 밀수꾼", "노병의 유품 가게" };

        static List<string> allItems;
        /// <summary>데이터가 읽힌 뒤 처음 쓸 때 만든다</summary>
        static List<string> AllItems
        {
            get
            {
                if (allItems == null || allItems.Count == 0)
                {
                    var l = new List<string>();
                    foreach (var w in Data.Weapons) l.Add(w.id);
                    foreach (var a in Data.Armors) l.Add(a.id);
                    allItems = l.Where(id => !Basic.Contains(id)).ToList();
                }
                return allItems;
            }
        }

        static readonly List<KeyValuePair<string, double>> QualityWeights = new()
        {
            new("0", 70), new("1", 24), new("2", 6),
        };

        public static GearListing MakeListing(GameState s, Rng rng, string nation)
        {
            var taste = NationTaste[nation];
            string b = rng.Chance(0.55) ? rng.Pick(taste) : rng.Pick(AllItems);
            // 시장엔 명품까지만. 전설은 던전에서만 나온다
            string q = rng.Weighted(QualityWeights);
            string item = q == "0" ? b : $"{b}+{q}";
            var listing = new GearListing { id = $"G{s.nextId++}", item = item, nation = nation };
            listing.price = JsMath.Round((ItemPrice(item) * rng.Range(0.85, 1.3)) / 10) * 10;
            listing.seller = $"{Data.Nation(nation).@short} {rng.Pick(Sellers)}";
            return listing;
        }

        /// <summary>장비 시장 매물: 처음엔 새로 깔고, 급료일마다 3분의 1쯤 바뀐다</summary>
        public static void RefreshGearMarket(GameState s, Rng rng, bool full = false)
        {
            var market = GearMarketOf(s);
            var keep = full ? new List<GearListing>() : market.Where(_ => rng.Chance(0.65)).ToList();
            const int want = 48;
            while (keep.Count < want)
            {
                string nation = rng.Chance(0.4) ? s.nation : rng.Pick(Data.Nations).id;
                keep.Add(MakeListing(s, rng, nation));
            }
            s.gearMarket = keep;
        }

        /// <summary>사투에서 우리 검투사가 적을 죽이면 그 장비를 거둔다</summary>
        public static List<string> LootFallen(GameState s, Gladiator killer, Gladiator dead)
        {
            var e = dead.equipment;
            var got = new[] { e.main, e.off, e.armor, e.helmet }.Where(x => !string.IsNullOrEmpty(x) && !Basic.Contains(x)).ToList();
            if (got.Count == 0) return got;
            ArmoryOf(s).AddRange(got);
            StateUtil.PushMessage(s, "info", $"전리품: {dead.name}의 장비",
                $"{Josa.J(killer.name, "이/가")} 쓰러뜨린 {dead.name}의 장비를 거뒀다.\n{string.Join(", ", got.Select(ItemName))}\n\n막사 → 검투사 화면에서 착용하거나, 이적 시장 → 창고에서 팔 수 있다.");
            return got;
        }

        /// <summary>오늘 절대 일수</summary>
        public static int Today(GameState s) => Calendar.AbsDay(s.season, s.day);
    }
}
