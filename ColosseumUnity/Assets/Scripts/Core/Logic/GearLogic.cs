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
        /// <summary>slot: main | off | armor | helmet | arms | legs (팔·다리는 Unity판)</summary>
        public static readonly Dictionary<string, string> SlotNames = new() { ["main"] = "주무기", ["off"] = "보조", ["armor"] = "갑옷", ["helmet"] = "투구", ["arms"] = "팔", ["legs"] = "다리" };

        /// <summary>버려도 아깝지 않은 기본 장비: 벗기면 창고에 남기지 않는다</summary>
        static readonly HashSet<string> Basic = new() { "loincloth", "cestus" };
        /// <summary>빈손일 때 대신 드는 것</summary>
        static readonly Dictionary<string, string> Fallback = new() { ["main"] = "cestus", ["armor"] = "loincloth" };

        public static readonly Dictionary<string, string> ItemKindNames = new() { ["weapon"] = "무기", ["armor"] = "갑옷", ["helmet"] = "투구", ["shield"] = "방패", ["arms"] = "팔 보호구", ["legs"] = "다리 보호구" };

        /// <summary>weapon | armor | helmet | shield | arms | legs, 아니면 null</summary>
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
            else if (Uniques.IsLimb(a?.slot)) bs = 45 + (a.defense ?? 0) * 60;
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

        /// <summary>우리 창고. FM 세계는 우리 루두스의 창고 (옛 전역 창고가 남아 있으면 옮긴다)</summary>
        public static List<string> ArmoryOf(GameState s)
        {
            if (!FmRules.Enabled || string.IsNullOrEmpty(s.playerLudusId) || !s.ludi.TryGetValue(s.playerLudusId, out var me)) return s.armory ??= new List<string>();
            return ArmoryOf(s, me);
        }

        /// <summary>그 루두스의 창고 (FM 세계). 웹판 규칙이면 우리 루두스만 전역 창고를 쓴다</summary>
        public static List<string> ArmoryOf(GameState s, Ludus l)
        {
            if (l == null) return new List<string>();
            if (!FmRules.Enabled) return l.isPlayer ? (s.armory ??= new List<string>()) : (l.armory ??= new List<string>());
            l.armory ??= new List<string>();
            if (l.isPlayer && s.armory != null && s.armory.Count > 0) { l.armory.AddRange(s.armory); s.armory = null; }
            return l.armory;
        }

        static string GetSlot(Equipment e, string slot) => slot switch
        {
            "main" => e.main, "off" => e.off, "armor" => e.armor, "helmet" => e.helmet, "arms" => e.arms, "legs" => e.legs, _ => null,
        };

        static void SetSlot(Equipment e, string slot, string v)
        {
            switch (slot)
            {
                case "main": e.main = v; break;
                case "off": e.off = v; break;
                case "armor": e.armor = v; break;
                case "helmet": e.helmet = v; break;
                case "arms": e.arms = v; break;
                case "legs": e.legs = v; break;
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
                    if (w == null) return "무기가 아닙니다";
                    if (w.offhandOnly) return "보조 무기로만 쓸 수 있습니다";
                    return null;
                case "off":
                    if (Data.Weapon(g.equipment.main)?.twoHanded == true) return "양손 무기를 들고 있습니다";
                    if (a?.slot == "shield") return null;
                    if (w != null && (w.offhandOnly || w.@class == "dagger")) return null;
                    return "방패나 보조 무기만 됩니다";
                case "armor":
                    if (a?.slot != "armor") return "갑옷이 아닙니다";
                    if (g.race == "minotaur" && a.weight == "light") return "미노타우로스 몸에 맞지 않습니다";
                    return null;
                case "helmet":
                    return a?.slot == "helmet" ? null : "투구가 아닙니다";
                case "arms":
                    return a?.slot == "arms" ? null : "팔 보호구가 아닙니다";
                case "legs":
                    return a?.slot == "legs" ? null : "다리 보호구가 아닙니다";
            }
            return null;
        }

        /// <summary>장비 착용: 창고에서 꺼내 끼고, 원래 것은 창고로. item이 null이면 벗기만 한다</summary>
        public static string EquipItem(GameState s, string gId, string slot, string item)
        {
            s.gladiators.TryGetValue(gId, out var g);
            if (g == null || !g.alive) return "검투사가 없습니다";
            if (g.deployment != null) return "파견 중이라 장비를 바꿀 수 없습니다";
            // FM 세계: 그 검투사가 속한 루두스의 창고에서 (AI 루두스도 같은 함수)
            var owner = FmRules.Enabled ? Career.LudusOf(s, gId) : null;
            var armory = owner != null ? ArmoryOf(s, owner) : ArmoryOf(s);
            if (item != null)
            {
                string why = CannotEquip(g, slot, item);
                if (why != null) return why;
                int at = armory.IndexOf(item);
                if (at < 0) return "창고에 없는 장비입니다";
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
            // FM 세계: 시스템이 즉시 사 주지 않는다. 다른 루두스가 실제로 사야 팔린다 (위탁)
            if (FmRules.Enabled) return "장비는 다른 루두스가 사야 팔립니다. 위탁으로 내놓으세요";
            var armory = ArmoryOf(s);
            int at = armory.IndexOf(item);
            if (at < 0) return "창고에 없습니다";
            armory.RemoveAt(at);
            Economy.AddMoney(s, s.ludi[s.playerLudusId], JsMath.Round(ItemPrice(item) * 0.35), $"{ItemName(item)} 판매");
            return null;
        }

        public static List<GearListing> GearMarketOf(GameState s) => s.gearMarket ??= new List<GearListing>();

        public static string BuyListing(GameState s, string listingId)
        {
            var market = GearMarketOf(s);
            var l = market.Find(x => x.id == listingId);
            if (l == null) return "이미 팔린 매물입니다";
            var me = s.ludi[s.playerLudusId];
            if (me.treasury < l.price) return "금고에 돈이 모자랍니다";
            Economy.AddMoney(s, me, -l.price, $"{ItemName(l.item)} 구입 ({l.seller})");
            ArmoryOf(s).Add(l.item);
            s.gearMarket = market.Where(x => x != l).ToList();
            PaySeller(s, l, l.price, null);
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
            if (l == null) return new HaggleResult { status = "error", line = "이미 팔린 매물입니다" };
            var me = s.ludi[s.playerLudusId];
            cash = Math.Max(0, cash);
            if (cash > me.treasury) return new HaggleResult { status = "error", line = "금고에 돈이 모자랍니다" };
            var armory = ArmoryOf(s);
            var left = armory.ToList();
            foreach (var t in tradeIns)
                if (!left.Remove(t)) return new HaggleResult { status = "error", line = $"창고에 {Josa.J(ItemName(t), "이/가")} 없습니다" };
            if (cash <= 0 && tradeIns.Count == 0) return new HaggleResult { status = "error", line = "아무것도 내놓지 않았습니다" };

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
            PaySeller(s, l, cash, tradeIns);
            if (tradeIns.Count > 0) res.line += $" ({Josa.J(string.Join(", ", tradeIns.Select(ItemName)), "을/를")} 넘겼습니다)";
            return res;
        }

        /// <summary>FM 세계: AI 루두스가 내놓은 매물이 팔리면 돈과 맞교환 장비는 그 루두스로 (상인 매물이면 아무것도 안 한다)</summary>
        public static void PaySeller(GameState s, GearListing l, double cash, List<string> tradeIns)
        {
            if (l.sellerLudus == null || !s.ludi.TryGetValue(l.sellerLudus, out var seller)) return;
            if (cash > 0) Economy.AddMoney(s, seller, cash, $"{ItemName(l.item)} 판매");
            if (tradeIns != null) ArmoryOf(s, seller).AddRange(tradeIns);
        }

        /// <summary>FM 세계: AI 루두스가 쓰지 않는 장비를 장비 시장에 내놓는다 (유저·다른 AI가 산다)</summary>
        public static void ListForSale(GameState s, Ludus l, string item, double price)
        {
            GearMarketOf(s).Add(new GearListing { id = $"G{s.nextId++}", item = item, nation = l.nation, price = price, seller = l.name, sellerLudus = l.id });
        }

        /// <summary>FM 세계: AI 루두스가 장비 시장 매물을 산다 (상인·다른 AI 매물). 돈은 판 쪽으로</summary>
        public static void AiBuyListing(GameState s, Ludus buyer, GearListing l)
        {
            GearMarketOf(s).Remove(l);
            Economy.AddMoney(s, buyer, -l.price, $"{ItemName(l.item)} 구매");
            PaySeller(s, l, l.price, null);
            BuyAndEquip(s, buyer, l.item);
        }

        public static List<Consignment> ConsignmentsOf(GameState s) => s.consignments ??= new List<Consignment>();

        /// <summary>내놓을 때 권하는 값: 기준가의 80% (중개인이 있으면 조금 더)</summary>
        public static double SuggestedPrice(GameState s, string item) => JsMath.Round(ItemPrice(item) * 0.8 * StaffLogic.SellMult(AgentRating(s)) / 10) * 10;

        /// <summary>창고 장비를 시장에 내놓는다. 성공하면 null</summary>
        public static string Consign(GameState s, string item, double price, bool viaAgent)
        {
            var armory = ArmoryOf(s);
            if (!armory.Contains(item)) return "창고에 없습니다";
            if (price <= 0) return "먼저 값을 정해야 합니다";
            if (viaAgent && StaffLogic.StaffOf(s, s.ludi[s.playerLudusId], "agent") == null) return "중개인이 없습니다. 식솔 화면에서 중개인을 들이거나 직접 내놓아야 합니다";
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
            (c.seller != null && s.ludi.TryGetValue(c.seller, out var l) ? ArmoryOf(s, l) : ArmoryOf(s)).Add(c.item);
        }

        // ── FM 세계 단계 6: 실거래 시장. 위탁품은 다른 루두스가 실제로 사야 팔린다 ──

        /// <summary>장비가 들어갈 칸</summary>
        public static string SlotFor(string item)
        {
            var w = Data.Weapon(item);
            if (w != null) return w.offhandOnly ? "off" : "main";
            var a = Data.Armor(item);
            return a?.slot switch { "shield" => "off", "armor" => "armor", "helmet" => "helmet", "arms" => "arms", "legs" => "legs", _ => null };
        }

        /// <summary>그 루두스 주전(상위 5) 중 이 장비로 가장 크게 나아지는 사람과 나아지는 정도 (0~1, 지금 장비 대비 값 차이)</summary>
        /// <summary>주전 (상위 5, 출전 가능한 사람). 장비 판단을 여러 번 할 때는 한 번만 구해 넘긴다</summary>
        public static List<Gladiator> StartersOf(GameState s, Ludus l) =>
            l.gladiators.Select(id => s.gladiators.TryGetValue(id, out var x) ? x : null).Where(x => x != null && x.alive && x.deployment == null)
                .OrderByDescending(GladiatorRules.ComputeCA).Take(5).ToList();

        public static (Gladiator who, double gain) BestUse(GameState s, Ludus l, string item, List<Gladiator> starters = null)
        {
            string slot = SlotFor(item);
            if (slot == null) return (null, 0);
            double p = ItemPrice(item);
            Gladiator best = null;
            double bestGain = 0;
            starters ??= StartersOf(s, l);
            foreach (var g in starters)
            {
                if (CannotEquip(g, slot, item) != null) continue;
                string cur = GetSlot(g.equipment, slot);
                double now = string.IsNullOrEmpty(cur) ? 0 : ItemPrice(cur);
                double gain = Math.Min(1, (p - now) / Math.Max(1, p));
                if (gain > bestGain) { bestGain = gain; best = g; }
            }
            return (best, bestGain);
        }

        /// <summary>이 루두스가 이 장비에 낼 마음이 있는 값 (0 = 안 산다): 정가 × 향상도 × 금고 여유 × 성격. 금고에 주급 6주치는 남긴다</summary>
        public static double GearDemand(GameState s, Ludus l, string item, double bonus = 0, List<Gladiator> starters = null, double? weeklyWages = null)
        {
            if (l == null || l.isPlayer) return 0;
            var (who, gain) = BestUse(s, l, item, starters);
            if (who == null || gain < 0.15) return 0;
            double wages = weeklyWages ?? Economy.WeeklyWages(s, l);
            double wealth = Math.Max(0.5, Math.Min(1.2, l.treasury / Math.Max(100, wages * 12)));
            double persona = l.aiPersonality switch { "merchant" => 0.85, "aggressive" => 1.15, _ => 1.0 };
            double pay = ItemPrice(item) * (0.55 + 0.6 * gain) * wealth * persona * (1 + bonus);
            if (l.treasury - pay < wages * 6) return 0;
            return JsMath.Round(pay);
        }

        /// <summary>산 루두스가 바로 주전에게 입힌다 (벗은 장비는 그 창고로)</summary>
        static void BuyAndEquip(GameState s, Ludus buyer, string item)
        {
            ArmoryOf(s, buyer).Add(item);
            var (who, _) = BestUse(s, buyer, item);
            if (who != null) EquipItem(s, who.id, SlotFor(item), item);
        }

        /// <summary>그 루두스가 위탁품을 산다: 돈은 판 쪽으로(중개인이면 수수료를 떼고), 물건은 산 쪽 창고·주전에게</summary>
        static void Sell(GameState s, Consignment c, Ludus buyer)
        {
            s.consignments.Remove(c);
            Economy.AddMoney(s, buyer, -c.price, $"{ItemName(c.item)} 구매");
            BuyAndEquip(s, buyer, c.item);
            var seller = c.seller != null && s.ludi.TryGetValue(c.seller, out var sl) ? sl : s.ludi[s.playerLudusId];
            double fee = c.viaAgent ? JsMath.Round(c.price * AgentCommission) : 0;
            Economy.AddMoney(s, seller, c.price - fee, $"{ItemName(c.item)} 판매{(c.viaAgent ? " (중개인)" : "")}");
            if (seller.isPlayer)
                StateUtil.PushMessage(s, "info", $"장비가 팔렸습니다: {ItemName(c.item)}",
                    $"{buyer.name}({Data.Nation(buyer.nation).@short} {buyer.tier}부)이 {c.price:N0} D에 샀습니다.{(fee > 0 ? $" 중개인 수수료 {fee:N0} D를 뗐습니다." : "")}");
        }

        /// <summary>이 위탁품을 지금 값에 살 마음이 있는 루두스 수 (중개인 힌트). 중개인은 능력이 높으면 다른 나라까지 안다</summary>
        public static int InterestedBuyers(GameState s, Consignment c)
        {
            var me = s.ludi[s.playerLudusId];
            double nego = StaffLogic.AgentNego(s, me);
            return s.ludi.Values.Count(l => !l.isPlayer && l.id != c.seller && (l.nation == me.nation || nego >= 14) && GearDemand(s, l, c.item, AgentBonus(nego)) >= c.price);
        }

        /// <summary>중개인 웃돈: 협상 능력만큼 +5~15%</summary>
        static double AgentBonus(double nego) => 0.05 + Math.Min(1, nego / 20) * 0.10;

        /// <summary>오늘이 이 루두스의 주간 차례인가 (480팀을 매일 다 돌리지 않는다: id 해시로 요일을 나눈다)</summary>
        public static bool TurnDay(GameState s, Ludus l)
        {
            uint h = 2166136261;
            foreach (char ch in l.id) { h ^= ch; h *= 16777619; }
            return h % 7 == Today(s) % 7;
        }

        /// <summary>FM 세계: 하루 위탁 시장. ① 중개인 위탁은 중개인이 매일 살 만한 루두스 1~4곳에 직접 제안 ② 직접 위탁은 AI 루두스가 주간 차례에 같은 나라 매물을 둘러보고 지불 의사가 가격 이상이면 산다</summary>
        static void DailyMarketFm(GameState s)
        {
            if (s.consignments == null || s.consignments.Count == 0) return;
            var me = s.ludi[s.playerLudusId];
            foreach (var c in s.consignments.Where(x => x.viaAgent).ToList())
            {
                var owner = c.seller != null && s.ludi.TryGetValue(c.seller, out var sl) ? sl : me;
                double nego = StaffLogic.AgentNego(s, owner);
                int calls = 1 + (int)Math.Min(3, nego / 6);
                double bonus = AgentBonus(nego);
                // 살 마음이 큰 곳부터 (능력이 높으면 다른 나라까지)
                var buyer = s.ludi.Values.Where(l => !l.isPlayer && l != owner && (l.nation == owner.nation || nego >= 14))
                    .Select(l => (l, pay: GearDemand(s, l, c.item, bonus))).Where(x => x.pay > 0)
                    .OrderByDescending(x => x.pay).Take(calls).FirstOrDefault(x => x.pay >= c.price);
                if (buyer.l != null) Sell(s, c, buyer.l);
            }
            foreach (var l in s.ludi.Values)
            {
                if (l.isPlayer || !TurnDay(s, l)) continue;
                var pick = s.consignments.Where(c => !c.viaAgent && c.seller != l.id && NationOf(s, c) == l.nation)
                    .Select(c => (c, pay: GearDemand(s, l, c.item))).Where(x => x.pay >= x.c.price)
                    .OrderByDescending(x => x.pay - x.c.price).FirstOrDefault();
                if (pick.c != null) Sell(s, pick.c, l);
            }
        }

        static string NationOf(GameState s, Consignment c) =>
            c.seller != null && s.ludi.TryGetValue(c.seller, out var l) ? l.nation : s.nation;

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
            if (FmRules.Enabled) { DailyMarketFm(s); return; }
            var me = s.ludi[s.playerLudusId];
            foreach (var c in s.consignments.ToList())
            {
                if (!rng.Chance(DailySaleChance(s, c))) continue;
                s.consignments.Remove(c);
                double fee = c.viaAgent ? JsMath.Round(c.price * AgentCommission) : 0;
                Economy.AddMoney(s, me, c.price - fee, $"{ItemName(c.item)} 판매{(c.viaAgent ? " (중개인)" : "")}");
                StateUtil.PushMessage(s, "info", $"장비가 팔렸습니다: {ItemName(c.item)}",
                    $"{c.price:N0} D에 팔렸습니다.{(fee > 0 ? $" 중개인 수수료 {fee:N0} D를 뗐습니다." : "")}");
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
        static bool allItemsExtended;
        /// <summary>데이터가 읽힌 뒤 처음 쓸 때 만든다</summary>
        static List<string> AllItems
        {
            get
            {
                if (allItems == null || allItems.Count == 0 || allItemsExtended != Uniques.Enabled)
                {
                    var l = new List<string>();
                    foreach (var w in Data.Weapons) l.Add(w.id);
                    foreach (var a in Data.Armors) l.Add(a.id);
                    allItems = l.Where(id => !Basic.Contains(id) && Uniques.InPool(id)).ToList();
                    allItemsExtended = Uniques.Enabled;
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
            string item = Uniques.Assign(rng, q == "0" ? b : $"{b}+{q}");
            var listing = new GearListing { id = $"G{s.nextId++}", item = item, nation = nation };
            listing.price = JsMath.Round((ItemPrice(item) * rng.Range(0.85, 1.3)) / 10) * 10;
            listing.seller = $"{Data.Nation(nation).@short} {rng.Pick(Sellers)}";
            return listing;
        }

        /// <summary>장비 시장 매물: 처음엔 새로 깔고, 급료일마다 3분의 1쯤 바뀐다</summary>
        public static void RefreshGearMarket(GameState s, Rng rng, bool full = false)
        {
            var market = GearMarketOf(s);
            // FM 세계: AI 루두스가 내놓은 매물은 팔릴 때까지 남는다 (장비가 사라지지 않게). 상인 매물만 바뀐다
            var listed = FmRules.Enabled ? market.Where(x => x.sellerLudus != null).ToList() : new List<GearListing>();
            var keep = full ? new List<GearListing>() : market.Where(x => x.sellerLudus == null && rng.Chance(0.65)).ToList();
            // 상인 매물 수: FM 세계는 AI도 사 가므로 팀 수에 비례
            int want = FmRules.Enabled ? 48 * FmRules.TeamsPerTier / 10 : 48;
            while (keep.Count < want)
            {
                string nation = rng.Chance(0.4) ? s.nation : rng.Pick(Data.Nations).id;
                keep.Add(MakeListing(s, rng, nation));
            }
            keep.AddRange(listed);
            s.gearMarket = keep;
        }

        /// <summary>사투에서 우리 검투사가 적을 죽이면 그 장비를 거둔다</summary>
        public static List<string> LootFallen(GameState s, Gladiator killer, Gladiator dead)
        {
            var e = dead.equipment;
            var got = new[] { e.main, e.off, e.armor, e.helmet, e.arms, e.legs }.Where(x => !string.IsNullOrEmpty(x) && !Basic.Contains(x)).ToList();
            if (got.Count == 0) return got;
            // FM 세계: 이긴 쪽 루두스 창고로 (AI도 거둔다). 쓰러진 자의 장비는 벗긴다 (장비가 두 곳에 있지 않게)
            var winner = FmRules.Enabled ? Career.LudusOf(s, killer.id) : null;
            if (FmRules.Enabled && winner == null) return new List<string>();
            (winner != null ? ArmoryOf(s, winner) : ArmoryOf(s)).AddRange(got);
            if (FmRules.Enabled) { e.main = Fallback["main"]; e.armor = Fallback["armor"]; e.off = e.helmet = e.arms = e.legs = null; }
            if (winner != null && !winner.isPlayer) return got;
            StateUtil.PushMessage(s, "info", $"전리품: {dead.name}의 장비",
                $"{Josa.J(killer.name, "이/가")} 쓰러뜨린 {dead.name}의 장비를 거뒀습니다.\n{string.Join(", ", got.Select(ItemName))}\n\n막사 → 검투사 화면에서 착용하거나, 이적 시장 → 창고에서 팔 수 있습니다.");
            return got;
        }

        /// <summary>오늘 절대 일수</summary>
        public static int Today(GameState s) => Calendar.AbsDay(s.season, s.day);
    }
}
