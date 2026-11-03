// 이적 시장 (웹판 MarketScreen.tsx + GearPanels.tsx의 GearSearch·ArmoryPanel):
// 선수 찾기, 관심 명단, 협상·제안, 우리 검투사(방출), 장비 시장, 창고. 줄은 하이어라키의 비활성 원본을 복제한다.
// Unity판에만 있는 것: 장비 흥정(현금 + 창고 장비 맞교환), 개인 계약 흥정, 창고 장비 위탁 판매(중개인 또는 직접), 등급 색.
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class MarketScreen : ScreenBase
    {
        static readonly string[] Tabs = { "search", "shortlist", "deals", "mine", "gear", "armory" };
        static readonly int[] CaSteps = { 0, 40, 60, 80, 100, 120 };
        static readonly int[] PaSteps = { 0, 60, 80, 100, 120, 140, 160 };
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        [SerializeField] TMP_Text sub;
        [Tooltip("탭 순서: 선수 찾기, 관심 명단, 협상·제안, 우리 검투사, 장비 시장, 창고")]
        [SerializeField] List<Button> tabButtons = new();
        [SerializeField] List<GameObject> tabRoots = new();

        [Header("선수 찾기")]
        [SerializeField] TMP_Dropdown scope;
        [SerializeField] TMP_Dropdown nation;
        [SerializeField] TMP_Dropdown race;
        [SerializeField] TMP_Dropdown style;
        [SerializeField] TMP_InputField maxAge;
        [SerializeField] TMP_Dropdown minCa;
        [SerializeField] TMP_Dropdown minPa;
        [SerializeField] TMP_InputField maxWage;
        [SerializeField] TMP_InputField nameFilter;
        [Tooltip("이적 관심도 하한 (FM처럼): 제한 없음 / 낮음 이상 / 보통 이상 / 높음 이상 / 매우 높음")]
        [SerializeField] TMP_Dropdown minInterest;
        [SerializeField] TMP_Dropdown sortBy;
        [SerializeField] TMP_Text searchTitle;
        [SerializeField] TMP_Text searchMsg;
        [SerializeField] GameObject searchEmpty;
        [SerializeField] RectTransform searchTemplate;

        [Header("관심 명단")]
        [SerializeField] TMP_Text shortTitle;
        [SerializeField] TMP_Text shortMsg;
        [SerializeField] GameObject shortEmpty;
        [SerializeField] RectTransform shortTemplate;

        [Header("협상·제안")]
        [SerializeField] TMP_Text dealsMsg;
        [SerializeField] GameObject dealEmpty;
        [SerializeField] RectTransform dealTemplate;
        [SerializeField] GameObject offerEmpty;
        [SerializeField] RectTransform offerTemplate;

        [Header("우리 검투사")]
        [SerializeField] RectTransform mineTemplate;

        [Header("장비 시장")]
        [SerializeField] TMP_Dropdown gearKind;
        [SerializeField] TMP_Dropdown gearQuality;
        [SerializeField] TMP_Dropdown gearNation;
        [SerializeField] TMP_InputField gearMaxPrice;
        [SerializeField] TMP_InputField gearName;
        [SerializeField] TMP_Dropdown gearSort;
        [SerializeField] TMP_Text gearTitle;
        [SerializeField] TMP_Text gearMsg;
        [SerializeField] GameObject gearEmpty;
        [SerializeField] RectTransform gearTemplate;
        [Header("장비 흥정 (매물 줄의 흥정 버튼으로 엽니다)")]
        [SerializeField] GameObject haggleRoot;
        [SerializeField] TMP_Text haggleTitle;
        [SerializeField] TMP_Text haggleStatus;
        [SerializeField] TMP_InputField haggleCash;
        [SerializeField] TMP_Text haggleTotal;
        [SerializeField] GameObject tradeEmpty;
        [Tooltip("맞교환 장비 줄 원본 (누르면 넣고 뺍니다)")]
        [SerializeField] Button tradeTemplate;
        [SerializeField] Button haggleOffer;
        [SerializeField] Button haggleClose;

        [Header("창고")]
        [SerializeField] TMP_Text armoryTitle;
        [SerializeField] TMP_Text armoryMsg;
        [SerializeField] GameObject armoryEmpty;
        [SerializeField] RectTransform armoryTemplate;
        [Header("장비 내놓기")]
        [SerializeField] GameObject consignRoot;
        [SerializeField] TMP_Text consignTitle;
        [SerializeField] TMP_InputField consignPrice;
        [SerializeField] TMP_Text consignHint;
        [SerializeField] Button consignAgent;
        [SerializeField] Button consignSelf;
        [SerializeField] Button consignClose;
        [SerializeField] GameObject consignedRoot;
        [SerializeField] RectTransform consignedTemplate;

        TemplateList<RectTransform> searchRows, shortRows, dealRows, offerRows, mineRows, gearRows, armoryRows, consignedRows;
        TemplateList<Button> tradeRows;
        // 흥정 중인 매물과 맞교환으로 고른 창고 칸(순서), 위탁할 장비
        string haggleId, consignItem;
        readonly List<int> tradePicks = new();
        // 화면 상태 (웹판 useState)
        string tab = "search", msg, confirmRelease;
        string nationId;
        readonly Dictionary<string, (double wage, int years)> offers = new();
        List<string> kindKeys = new();

        void Awake()
        {
            searchRows = new TemplateList<RectTransform>(searchTemplate);
            shortRows = new TemplateList<RectTransform>(shortTemplate);
            dealRows = new TemplateList<RectTransform>(dealTemplate);
            offerRows = new TemplateList<RectTransform>(offerTemplate);
            mineRows = new TemplateList<RectTransform>(mineTemplate);
            gearRows = new TemplateList<RectTransform>(gearTemplate);
            armoryRows = new TemplateList<RectTransform>(armoryTemplate);
            consignedRows = new TemplateList<RectTransform>(consignedTemplate);
            tradeRows = new TemplateList<Button>(tradeTemplate);
            haggleClose.onClick.AddListener(() => { haggleId = null; Refresh(); });
            haggleCash.onEndEdit.AddListener(_ => Refresh());
            haggleOffer.onClick.AddListener(OfferGear);
            consignClose.onClick.AddListener(() => { consignItem = null; Refresh(); });
            consignPrice.onEndEdit.AddListener(_ => Refresh());
            consignAgent.onClick.AddListener(() => DoConsign(true));
            consignSelf.onClick.AddListener(() => DoConsign(false));
            for (int i = 0; i < tabButtons.Count; i++)
            {
                string t = Tabs[i];
                tabButtons[i].onClick.AddListener(() => { tab = t; msg = null; confirmRelease = null; haggleId = null; consignItem = null; Refresh(); });
            }

            // 고정 선택지
            Fill(scope, new[] { "전체", "무소속만", "다른 루두스만" }, 0);
            Fill(race, new[] { "전부" }.Concat(Data.Races.Select(r => r.name)), 0);
            Fill(style, new[] { "전부" }.Concat(Data.Styles.Select(s => s.name)), 0);
            Fill(minCa, CaSteps.Select(v => v == 0 ? "제한 없음" : "★" + v / 40.0), 0);
            Fill(minPa, PaSteps.Select(v => v == 0 ? "제한 없음" : "★" + v / 40.0), 0);
            Fill(minInterest, new[] { "제한 없음", "낮음 이상", "보통 이상", "높음 이상", "매우 높음" }, 0);
            Fill(sortBy, new[] { "추정 능력 순", "추정 잠재 순", "관심도 순", "주급 낮은 순", "나이 어린 순" }, 0);
            kindKeys = GearLogic.ItemKindNames.Keys.ToList();
            Fill(gearKind, new[] { "전부" }.Concat(kindKeys.Select(k => GearLogic.ItemKindNames[k])), 0);
            Fill(gearQuality, GearLogic.QualityNames.Take(3).Select(q => q == "" ? "보통" : q), 0);
            Fill(gearNation, new[] { "전부" }.Concat(Data.Nations.Select(n => n.name)), 0);
            Fill(gearSort, new[] { "품질 높은 순", "싼 순" }, 0);

            foreach (var d in new[] { scope, race, style, minCa, minPa, minInterest, sortBy, gearKind, gearQuality, gearNation, gearSort })
                d.onValueChanged.AddListener(_ => Refresh());
            nation.onValueChanged.AddListener(i => { nationId = i == 0 ? "" : Data.Nations[i - 1].id; Refresh(); });
            foreach (var f in new[] { maxAge, maxWage, nameFilter, gearMaxPrice, gearName })
                f.onEndEdit.AddListener(_ => Refresh());
        }

        Ludus Player => S.ludi[S.playerLudusId];

        public override void Refresh()
        {
            var me = Player;
            var t = Transfers.TransferOf(S);
            double rep = Reputation.LudusReputation(S, me);
            sub.text = $"우리 명성 {rep} ({Reputation.ReputationNames(rep)}) · 막사 {me.gladiators.Count}/{Economy.RosterCapacity(me)}명 · " +
                (Transfers.InWindow(S)
                    ? $"<b><color={Theme.Hex(Theme.Good)}>{Tips.Window(Transfers.WindowLine(S))}</color></b>"
                    : $"<b><color=#d9a29b>{Tips.Window(Transfers.WindowLine(S))}</color></b> · 그때까지 다른 루두스 검투사에게는 제안할 수 없습니다. 무소속은 언제든 됩니다") +
                $"\n<color={Theme.Hex(Theme.TextFaint)}>제안을 보내면 하루~사흘 뒤 서판으로 답이 옵니다. 그사이 다른 루두스가 끼어들 수 있습니다</color>";

            string[] labels =
            {
                "선수 찾기", $"관심 명단 ({S.shortlist?.Count ?? 0})", $"협상·제안 ({t.deals.Count + t.offers.Count})",
                "우리 검투사", "장비 시장", $"창고 ({S.armory?.Count ?? 0})",
            };
            for (int i = 0; i < tabButtons.Count; i++)
            {
                tabButtons[i].GetComponentInChildren<TMP_Text>().text = labels[i];
                Seg(tabButtons[i], tab == Tabs[i]);
                tabRoots[i].SetActive(tab == Tabs[i]);
            }

            switch (tab)
            {
                case "search": DrawSearch(); break;
                case "shortlist":
                    var list = (S.shortlist ?? new List<string>()).Select(id => S.gladiators.TryGetValue(id, out var g) ? g : null).Where(g => g?.alive == true).ToList();
                    DrawResults(list, shortRows, shortTitle, shortMsg, shortEmpty);
                    break;
                case "deals": DrawDeals(); break;
                case "mine": DrawMine(); break;
                case "gear": DrawGear(); break;
                case "armory": DrawArmory(); break;
            }
        }

        // ── 선수 찾기 ──

        void DrawSearch()
        {
            if (nationId == null) nationId = S.nation;
            Fill(nation, new[] { "전부" }.Concat(Data.Nations.Select(n => n.name)), nationId == "" ? 0 : Data.Nations.FindIndex(n => n.id == nationId) + 1);
            var me = Player;
            var free = S.freeAgents.Select(id => S.gladiators.TryGetValue(id, out var g) ? g : null).Where(g => g != null);
            // 다른 나라 루두스도 찾아볼 수 있다
            var clubs = S.ludi.Values.Where(l => !l.isPlayer && (nationId == "" || l.nation == nationId)).SelectMany(l => l.gladiators.Select(id => S.gladiators[id]));
            var all = scope.value switch { 1 => free, 2 => clubs, _ => free.Concat(clubs) };
            var pool = all.Where(g => g?.alive == true && !me.gladiators.Contains(g.id) && GladiatorRules.AgeStage(g) != "youth");

            string raceId = race.value == 0 ? null : Data.Races[race.value - 1].id;
            string styleId = style.value == 0 ? null : Data.Styles[style.value - 1].id;
            double? ageCap = Num(maxAge), wageCap = Num(maxWage);
            string name = nameFilter.text;
            int caMin = CaSteps[minCa.value], paMin = PaSteps[minPa.value], interestMin = minInterest.value;
            double rep = Reputation.LudusReputation(S, me);
            var matched = pool
                .Where(g => raceId == null || g.race == raceId)
                .Where(g => styleId == null || g.style == styleId)
                .Where(g => ageCap == null || g.age <= ageCap)
                .Where(g => wageCap == null || g.wage <= wageCap)
                .Where(g => name == "" || g.name.Contains(name))
                .Select(g => (g, ca: Scouting.CaRange(S, g), pa: Scouting.PaRange(S, g), interest: Reputation.InterestIn(S, g, rep)))
                .Where(r => r.ca.mid >= caMin && r.pa.mid >= paMin && r.interest >= interestMin);
            var results = (sortBy.value switch
                {
                    1 => matched.OrderByDescending(r => r.pa.mid),
                    2 => matched.OrderByDescending(r => r.interest).ThenByDescending(r => r.ca.mid),
                    3 => matched.OrderBy(r => r.g.wage),
                    4 => matched.OrderBy(r => r.g.age).ThenByDescending(r => r.pa.mid),
                    _ => matched.OrderByDescending(r => r.ca.mid),
                })
                .Take(80)
                .Select(r => r.g)
                .ToList();
            DrawResults(results, searchRows, searchTitle, searchMsg, searchEmpty);
        }

        void DrawResults(List<Gladiator> list, TemplateList<RectTransform> rows, TMP_Text title, TMP_Text msgText, GameObject empty)
        {
            var sc = Scouting.ScoutingOf(S);
            title.text = $"◆  {list.Count}명 · 정찰 {sc.assignments.Count}/{Scouting.ScoutSlots(S)}";
            ShowMsg(msgText);
            empty.SetActive(list.Count == 0);
            rows.Sync(list.Count, (row, i) => BindResult(row, list[i], sc));
        }

        void BindResult(RectTransform row, Gladiator g, ScoutingState sc)
        {
            var owner = Career.LudusOf(S, g.id);
            var ca = Scouting.CaRange(S, g);
            var pa = Scouting.PaRange(S, g);
            double k = Scouting.KnowledgeOf(S, g);
            bool shortlisted = S.shortlist?.Contains(g.id) == true;
            bool scouting = sc.assignments.Contains(g.id);

            NameCell(row, g);
            T(row, "Race").text = Tips.Race(g.race);
            T(row, "Age").text = g.age.ToString();
            T(row, "Style").text = Tips.Style(g.style);
            T(row, "Owner").text = owner != null ? Tips.Ludus(owner) : $"<color={Theme.Hex(Theme.Good)}>무소속</color>";
            T(row, "Ca").text = Widgets.RangeStars(ca.lo, ca.hi);
            T(row, "Pa").text = Widgets.RangeStars(pa.lo, pa.hi);
            T(row, "Interest").text = InterestLabel(Reputation.InterestIn(S, g, Reputation.LudusReputation(S, Player)));
            Widgets.SetBar((RectTransform)row.Find("Info/Bar"), k, 100, Theme.BronzeHi);
            T(row, "Wage").text = $"{g.wage} D";
            T(row, "Value").text = owner != null ? $"{JsMath.Round(Economy.MarketValue(g) / 50) * 50:N0} D" : "-";

            string id = g.id;
            var star = row.Find("Star").GetComponent<Button>();
            star.GetComponentInChildren<TMP_Text>().text = shortlisted ? "★" : "☆";
            star.onClick.RemoveAllListeners();
            star.onClick.AddListener(() => G.Mutate(d =>
            {
                d.shortlist ??= new List<string>();
                if (d.shortlist.Contains(id)) d.shortlist = d.shortlist.Where(x => x != id).ToList();
                else d.shortlist.Add(id);
            }));
            var scout = row.Find("Scout").GetComponent<Button>();
            scout.GetComponentInChildren<TMP_Text>().text = scouting ? "정찰 중" : "정찰";
            scout.interactable = !scouting && k < 100;
            scout.onClick.RemoveAllListeners();
            scout.onClick.AddListener(() => { msg = G.Try(d => Scouting.AssignScout(d, id)); Refresh(); });
        }

        // ── 협상·제안 ──

        void DrawDeals()
        {
            var t = Transfers.TransferOf(S);
            ShowMsg(dealsMsg);
            dealEmpty.SetActive(t.deals.Count == 0);
            var deals = t.deals.ToList();
            dealRows.Sync(deals.Count, (row, i) => BindDeal(row, deals[i]));

            offerEmpty.SetActive(t.offers.Count == 0);
            var offs = t.offers.ToList();
            offerRows.Sync(offs.Count, (row, i) =>
            {
                var o = offs[i];
                var g = S.gladiators[o.gladiatorId];
                NameCell(row, g);
                T(row, "From").text = Tips.Ludus(S.ludi[o.fromLudus]);
                T(row, "Fee").text = $"{o.fee:N0} D";
                string oid = o.id;
                var accept = row.Find("Accept").GetComponent<Button>();
                accept.onClick.RemoveAllListeners();
                accept.onClick.AddListener(() => G.Mutate(s => Transfers.AcceptOffer(s, oid)));
                var reject = row.Find("Reject").GetComponent<Button>();
                reject.onClick.RemoveAllListeners();
                reject.onClick.AddListener(() => G.Mutate(s => Transfers.RejectOffer(s, oid)));
            });
        }

        /// <summary>웹판 Negotiate: 원하는 주급을 보여 주고 주급·기간을 제안한다</summary>
        void BindDeal(RectTransform row, Deal deal)
        {
            var g = S.gladiators[deal.gladiatorId];
            NameCell(row, g);
            T(row, "Info/Desc").text = $"{Tips.Ludus(S.ludi[deal.fromLudus])} · 이적료 {deal.fee:N0} D";
            var demand = Transfers.TransferDemand(S, g);
            var refuse = T(row, "Refuse");
            var nego = row.Find("Negotiate");
            refuse.gameObject.SetActive(demand.refuse);
            nego.gameObject.SetActive(!demand.refuse);
            refuse.text = demand.refuse ? $"관심 없음 · {demand.reason}" : "";
            if (demand.refuse) return;

            string did = deal.id;
            if (!offers.ContainsKey(did)) offers[did] = (demand.wage, 2);
            var (wage, years) = offers[did];
            T(nego, "Want").text = $"요구 주급 {demand.wage} D";
            var mood = T(row, "Info/Mood");
            var pending = Negotiations.Pending(S, "terms", did);
            string line = Widgets.BidLine(S, Transfers.DealKey(did), pending, g.id);
            mood.gameObject.SetActive(line != null);
            mood.text = line ?? "";
            var input = nego.Find("Wage").GetComponent<TMP_InputField>();
            input.onEndEdit.RemoveAllListeners();
            input.SetTextWithoutNotify(wage.ToString());
            input.onEndEdit.AddListener(v => { offers[did] = (double.TryParse(v, out var w) ? w : 0, offers[did].years); Refresh(); });
            var dd = nego.Find("Seasons").GetComponent<TMP_Dropdown>();
            Fill(dd, new[] { "1시즌", "2시즌", "3시즌", "4시즌" }, years - 1);
            dd.onValueChanged.RemoveAllListeners();
            dd.onValueChanged.AddListener(v => offers[did] = (offers[did].wage, v + 1));
            var offer = nego.Find("Offer").GetComponent<Button>();
            offer.interactable = wage > 0 && pending == null;
            offer.GetComponentInChildren<TMP_Text>().text = pending == null ? "계약 제안" : "답 기다림";
            offer.onClick.RemoveAllListeners();
            offer.onClick.AddListener(() =>
            {
                var (w, y) = offers[did];
                HaggleResult res = null;
                G.Act((s, rng) => res = Transfers.HaggleTransferWage(s, rng, did, w, y));
                msg = res.line;
                Refresh();
            });
        }

        // ── 우리 검투사 ──

        void DrawMine()
        {
            var list = Player.gladiators.Select(id => S.gladiators[id]).Where(g => g.alive).ToList();
            mineRows.Sync(list.Count, (row, i) =>
            {
                var g = list[i];
                NameCell(row, g);
                T(row, "Style").text = Tips.Style(g.style);
                T(row, "Wage").text = $"주급 {g.wage} D";
                T(row, "Value").text = $"추정 가치 {JsMath.Round(Economy.MarketValue(g) / 50) * 50:N0} D";
                // 웹판 confirm(): 한 번 더 누르면 방출
                string id = g.id;
                bool asking = confirmRelease == id;
                var b = row.Find("Release").GetComponent<Button>();
                b.interactable = g.deployment == null;
                b.GetComponentInChildren<TMP_Text>().text = asking ? $"정말 {Josa.J(g.name, "을/를")} 방출하겠습니까?" : "방출";
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() =>
                {
                    if (confirmRelease != id) { confirmRelease = id; Refresh(); return; }
                    confirmRelease = null;
                    G.Mutate(s => Economy.ReleaseGladiator(s, id));
                });
            });
        }

        // ── 장비 시장 · 창고 ──

        void DrawGear()
        {
            var me = Player;
            string kind = gearKind.value == 0 ? null : kindKeys[gearKind.value - 1];
            string nat = gearNation.value == 0 ? null : Data.Nations[gearNation.value - 1].id;
            double? priceCap = Num(gearMaxPrice);
            string name = gearName.text;
            int minQ = gearQuality.value;
            var filtered = GearLogic.GearMarketOf(S)
                .Where(l => kind == null || GearLogic.ItemKind(l.item) == kind)
                .Where(l => GearLogic.QualityOf(l.item) >= minQ)
                .Where(l => nat == null || l.nation == nat)
                .Where(l => priceCap == null || l.price <= priceCap)
                .Where(l => name == "" || GearLogic.ItemName(l.item).Contains(name));
            var list = (gearSort.value == 1
                ? filtered.OrderBy(l => l.price)
                : filtered.OrderByDescending(l => GearLogic.QualityOf(l.item)).ThenBy(l => l.price)).ToList();

            DrawHaggle();
            gearTitle.text = $"◆  매물 {list.Count}개 · 금고 {me.treasury:N0} D · 매물은 급료일마다 바뀝니다";
            ShowMsg(gearMsg);
            gearEmpty.SetActive(list.Count == 0);
            gearRows.Sync(list.Count, (row, i) =>
            {
                var l = list[i];
                ItemCells(row, l.item);
                T(row, "Seller").text = l.seller;
                var hag = row.Find("Haggle").GetComponent<Button>();
                bool locked = Haggling.IsLocked(S, GearLogic.HaggleKey(l.id));
                hag.interactable = !locked;
                hag.GetComponentInChildren<TMP_Text>().text = locked ? "결렬" : haggleId == l.id ? "흥정 중" : "흥정";
                string hid = l.id;
                hag.onClick.RemoveAllListeners();
                hag.onClick.AddListener(() => { haggleId = hid; tradePicks.Clear(); haggleCash.SetTextWithoutNotify(""); msg = null; Refresh(); });
                T(row, "Price").text = $"{l.price:N0} D";
                string lid = l.id;
                var buy = row.Find("Buy").GetComponent<Button>();
                buy.interactable = me.treasury >= l.price;
                buy.onClick.RemoveAllListeners();
                buy.onClick.AddListener(() => { msg = G.Try(s => GearLogic.BuyListing(s, lid)); Refresh(); });
            });
        }

        /// <summary>장비 흥정 패널: 현금 + 맞교환 장비로 값을 맞춘다</summary>
        void DrawHaggle()
        {
            var l = haggleId == null ? null : GearLogic.GearMarketOf(S).Find(x => x.id == haggleId);
            if (l == null) haggleId = null;
            haggleRoot.SetActive(l != null);
            if (l == null) return;
            var armory = GearLogic.ArmoryOf(S);
            tradePicks.RemoveAll(i => i >= armory.Count);
            haggleTitle.text = $"◆  흥정 — {Tips.Item(l.item)}  {Widgets.GradeTag(l.item)}  · 부른 값 {l.price:N0} D · {l.seller}";
            string key = GearLogic.HaggleKey(l.id);
            haggleStatus.text = Widgets.HaggleLine(S, key) ?? $"<color={Theme.Hex(Theme.TextDim)}>현금에 창고 장비를 보태 값을 맞춥니다. 맞교환 장비는 기준가의 {GearLogic.TradeInRate * 100:0}%로 쳐 줍니다. 값을 낮게 부를수록 상인이 짜증을 냅니다. 너무 깎으면 거래를 끊습니다.</color>";

            tradeEmpty.SetActive(armory.Count == 0);
            tradeRows.Sync(armory.Count, (b, i) =>
            {
                string id = armory[i];
                bool on = tradePicks.Contains(i);
                b.GetComponentInChildren<TMP_Text>().text = $"{(on ? "◆" : "◇")} {Widgets.ItemLabel(id)}  <color={Theme.Hex(Theme.TextDim)}>{GearLogic.TradeInValue(S, id):N0} D</color>";
                b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
                b.onClick.RemoveAllListeners();
                int idx = i;
                b.onClick.AddListener(() => { if (!tradePicks.Remove(idx)) tradePicks.Add(idx); Refresh(); });
            });
            double cash = Num(haggleCash) ?? 0;
            double trade = tradePicks.Sum(i => GearLogic.TradeInValue(S, armory[i]));
            haggleTotal.text = $"제안 합계 <b>{cash + trade:N0} D</b>  <color={Theme.Hex(Theme.TextDim)}>(현금 {cash:N0} + 장비 {trade:N0})</color>" +
                (cash + trade > l.price ? $"  <color=#d9a29b>부른 값보다 {cash + trade - l.price:N0} D 많습니다. 거스름돈은 없습니다</color>" : "");
            haggleOffer.interactable = !Haggling.IsLocked(S, key) && cash + trade > 0;
        }

        void OfferGear()
        {
            string lid = haggleId;
            var armory = GearLogic.ArmoryOf(S);
            var trade = tradePicks.Select(i => armory[i]).ToList();
            double cash = Num(haggleCash) ?? 0;
            HaggleResult res = null;
            G.Act((s, rng) => res = GearLogic.HaggleListing(s, rng, lid, cash, trade));
            msg = res.line;
            if (res.Accepted) { haggleId = null; tradePicks.Clear(); }
            else if (res.status == "countered")
                // 역제안에 맞춰 현금 칸을 채워 둔다 (맞교환 장비는 그대로)
                haggleCash.SetTextWithoutNotify(Math.Max(0, res.counter - trade.Sum(t => GearLogic.TradeInValue(S, t))).ToString());
            Refresh();
            // 성공 문구는 오류 색이 아니게
            if (res.Accepted) { gearMsg.gameObject.SetActive(true); gearMsg.text = res.line; gearMsg.color = Theme.Good; msg = null; }
        }

        void DoConsign(bool viaAgent)
        {
            string item = consignItem;
            double price = Num(consignPrice) ?? 0;
            msg = G.Try(s => GearLogic.Consign(s, item, price, viaAgent));
            if (msg == null) consignItem = null;
            Refresh();
        }

        /// <summary>위탁 패널과 내놓은 장비 목록</summary>
        void DrawConsign()
        {
            var armory = GearLogic.ArmoryOf(S);
            if (consignItem != null && !armory.Contains(consignItem)) consignItem = null;
            consignRoot.SetActive(consignItem != null);
            bool hasAgent = StaffLogic.StaffOf(S, Player, "agent") != null;
            if (consignItem != null)
            {
                double price = Num(consignPrice) ?? 0;
                consignTitle.text = $"◆  내놓기 — {Tips.Item(consignItem)}  {Widgets.GradeTag(consignItem)} · 기준가 {GearLogic.ItemPrice(consignItem):N0} D";
                var direct = new Consignment { item = consignItem, price = Math.Max(1, price) };
                var agent = new Consignment { item = consignItem, price = Math.Max(1, price), viaAgent = true };
                consignHint.text = price <= 0 ? "내놓을 값을 정합니다." :
                    $"직접: 하루 팔릴 확률 {GearLogic.DailySaleChance(S, direct) * 100:0.#}% · 수수료 없음\n" +
                    (hasAgent ? $"중개인: 하루 {GearLogic.DailySaleChance(S, agent) * 100:0.#}% · 수수료 {GearLogic.AgentCommission * 100:0}% ({JsMath.Round(price * GearLogic.AgentCommission):N0} D)"
                              : $"<color={Theme.Hex(Theme.TextFaint)}>고용한 중개인이 없습니다. 식솔 화면에서 먼저 고용해야 합니다</color>") +
                    $"\n<color={Theme.Hex(Theme.TextDim)}>싸게 내놓을수록 빨리 팔립니다. 판 돈에는 세금이 붙습니다.</color>";
                consignAgent.interactable = hasAgent && price > 0;
                consignSelf.interactable = price > 0;
            }
            var list = GearLogic.ConsignmentsOf(S).ToList();
            consignedRoot.SetActive(list.Count > 0);
            consignedRows.Sync(list.Count, (row, i) =>
            {
                var c = list[i];
                ItemCells(row, c.item);
                T(row, "Price").text = $"{c.price:N0} D";
                T(row, "Via").text = c.viaAgent ? "중개인" : "직접";
                T(row, "Chance").text = $"하루 {GearLogic.DailySaleChance(S, c) * 100:0.#}%";
                string cid = c.id;
                var b = row.Find("Withdraw").GetComponent<Button>();
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => G.Mutate(s => GearLogic.Withdraw(s, cid)));
            });
        }

        void DrawArmory()
        {
            DrawConsign();
            var items = GearLogic.ArmoryOf(S).ToList()
                .OrderByDescending(GearLogic.QualityOf).ThenBy(GearLogic.ItemName, StringComparer.CurrentCulture).ToList();
            armoryTitle.text = $"◆  창고 · {items.Count}점 — 장비는 막사의 검투사 화면에서 입힙니다";
            ShowMsg(armoryMsg);
            armoryEmpty.SetActive(items.Count == 0);
            armoryRows.Sync(items.Count, (row, i) =>
            {
                string id = items[i];
                ItemCells(row, id);
                T(row, "Sell").text = $"{JsMath.Round(GearLogic.ItemPrice(id) * 0.35):N0} D";
                var b = row.Find("SellButton").GetComponent<Button>();
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => { msg = G.Try(s => GearLogic.SellItem(s, id)); Refresh(); });
                var cb = row.Find("Consign").GetComponent<Button>();
                cb.onClick.RemoveAllListeners();
                cb.onClick.AddListener(() => { consignItem = id; consignPrice.SetTextWithoutNotify(GearLogic.SuggestedPrice(S, id).ToString()); msg = null; Refresh(); });
            });
        }

        void ItemCells(RectTransform row, string id)
        {
            T(row, "Item").text = Tips.Item(id);
            Widgets.SetSlot(row.Find("Slot"), id);
            var kind = GearLogic.ItemKind(id);
            T(row, "Kind").text = kind != null && GearLogic.ItemKindNames.TryGetValue(kind, out var k) ? k : "";
            T(row, "Stats").text = GearLogic.ItemStats(id);
            T(row, "Grade").text = Widgets.GradeTag(id);
            row.GetComponent<Image>().color = Widgets.GradeTint(id);
        }

        // ── 공용 ──

        void NameCell(RectTransform row, Gladiator g)
        {
            row.Find("Name").GetComponentInChildren<PortraitView>(true).Show(g);
            T(row, "Name/Text").text = g.name;
            var b = row.Find("Name").GetComponent<Button>();
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            string id = g.id;
            b.onClick.AddListener(() => G.ShowGladiator(id));
        }

        void ShowMsg(TMP_Text t)
        {
            t.gameObject.SetActive(msg != null);
            t.text = msg ?? "";
            t.color = Neg;
        }

        static double? Num(TMP_InputField f) => double.TryParse(f.text, out var v) ? v : null;
        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        static string InterestLabel(int i)
        {
            var c = i switch { 0 => Neg, 1 => Theme.TextFaint, 2 => Theme.Text, _ => Theme.Good };
            string t = Reputation.InterestNames[i];
            return $"<color={Theme.Hex(c)}>{(i == 4 ? $"<b>{t}</b>" : t)}</color>";
        }

        static void Fill(TMP_Dropdown d, IEnumerable<string> labels, int value)
        {
            var list = labels.ToList();
            bool same = d.options.Count == list.Count && d.options.Select(o => o.text).SequenceEqual(list);
            if (!same) { d.ClearOptions(); d.AddOptions(list); }
            d.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, list.Count - 1)));
        }

        /// <summary>웹판 .seg 버튼: 고른 것만 횃불색</summary>
        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
