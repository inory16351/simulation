// 던전 (웹판 DungeonScreen.tsx): 나라의 토벌 의뢰, 던전 고르기, 원정대(최대 5명)·진형을 꾸려 출발, 진행 중·지난 원정.
// 카드·줄·패널은 하이어라키의 비활성 원본을 복제한다.
// FM 세계: 급료일마다 위험도 1~10의 던전 자리 22~28곳이 열린다. 누가 먼저 원정을 보내면 그 주에는 막힌다.
// 카드는 격자(AutoGrid)로 늘어놓고, 던전 그림·위험도·우리 기준 어림(생존·완주)·차지한 루두스를 보여 준다 (씬 다시 조립 없이 코드로 붙인다).
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class DungeonScreen : ScreenBase
    {
        static readonly string[] FormationIds = Tactics.Policies;
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);
        static readonly Color CardLine = new Color32(0x6b, 0x4a, 0x34, 0xff);
        static readonly Color FormLine = new Color32(0x5a, 0x3f, 0x32, 0xff);
        static readonly Color FormFill = new Color32(0x2a, 0x1c, 0x1a, 0xff);
        static readonly Color FormFillOn = new Color32(0x4a, 0x2a, 0x20, 0xff);
        static readonly Color QuestOn = new Color(142 / 255f, 42 / 255f, 34 / 255f, 0.25f);
        static readonly Color Picked = new Color(242 / 255f, 163 / 255f, 58 / 255f, 0.1f);
        /// <summary>어림 난이도 색 (쉬움 → 무모함)</summary>
        static readonly Color[] OddsColors =
        {
            new Color32(0x8f, 0xa2, 0x5a, 0xff), new Color32(0xc9, 0xb8, 0x6a, 0xff), new Color32(0xe3, 0x9a, 0x3b, 0xff),
            new Color32(0xd9, 0x6a, 0x4a, 0xff), new Color32(0xc0, 0x3a, 0x2e, 0xff),
        };

        [SerializeField] TMP_Text sub;
        [Header("토벌 의뢰")]
        [SerializeField] GameObject questPanel;
        [SerializeField] RectTransform questTemplate;
        [Header("던전 카드 (원본은 하이어라키에 비활성)")]
        [SerializeField] Button cardTemplate;
        [Header("원정대")]
        [SerializeField] TMP_Text partyTitle;
        [SerializeField] GameObject partyEmpty;
        [SerializeField] Button partyTemplate;
        [Header("진형과 출발")]
        [SerializeField] Button formationTemplate;
        [SerializeField] TMP_Text wages;
        [SerializeField] TMP_Text msg;
        [SerializeField] Button start;
        [Header("진행 중 원정 (패널째 복제) · 지난 원정")]
        [SerializeField] RectTransform activeTemplate;
        [SerializeField] GameObject donePanel;
        [SerializeField] RectTransform doneTemplate;

        TemplateList<RectTransform> quests, actives, dones;
        TemplateList<Button> cards, partyRows, formations;
        // 화면 상태 (웹판 useState)
        string dungeon = "sewer", questId, formation = "focus", msgText;
        readonly List<string> party = new();
        /// <summary>FM 세계 카드 거르기: all 모두 · open 열린 곳 · fit 우리에게 맞는 곳(어려움까지)</summary>
        string filter = "open";
        TMP_Text oddsHead;
        RectTransform filterRow;
        static readonly Dictionary<string, Sprite> artCache = new();

        void Awake()
        {
            quests = new TemplateList<RectTransform>(questTemplate);
            PrepareCardTemplate();
            cards = new TemplateList<Button>(cardTemplate);
            partyRows = new TemplateList<Button>(partyTemplate);
            formations = new TemplateList<Button>(formationTemplate);
            actives = new TemplateList<RectTransform>(activeTemplate);
            dones = new TemplateList<RectTransform>(doneTemplate);
            start.onClick.AddListener(Go);
        }

        // ── 카드 원본 손질: 그림·위험도 띠·어림·상태 줄을 붙이고 줄 하나였던 카드 칸을 격자로 ──

        void PrepareCardTemplate()
        {
            var card = cardTemplate.transform;
            var name = T(card, "Name");
            // 그림 (카드 맨 위)
            var art = new GameObject("Art", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            art.transform.SetParent(card, false);
            art.transform.SetSiblingIndex(1); // Fill 다음
            var le = art.GetComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 118; le.flexibleHeight = 0;
            var img = art.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = false;
            // 그림 위 띠: 위험도 (왼쪽 위, 어두운 바탕 위에) · 차지한 루두스 (가운데)
            var badgeBg = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badgeBg.transform.SetParent(art.transform, false);
            var bb = (RectTransform)badgeBg.transform;
            bb.anchorMin = bb.anchorMax = new Vector2(0, 1); bb.pivot = new Vector2(0, 1);
            bb.anchoredPosition = new Vector2(6, -6); bb.sizeDelta = new Vector2(92, 26);
            badgeBg.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.03f, 0.85f);
            badgeBg.GetComponent<Image>().raycastTarget = false;
            var badge = Instantiate(name.gameObject, badgeBg.transform);
            badge.name = "Grade";
            var bt = badge.GetComponent<TMP_Text>();
            bt.fontSize = 14; bt.fontStyle = FontStyles.Bold; bt.alignment = TextAlignmentOptions.Center; bt.raycastTarget = false;
            var ble = badge.GetComponent<LayoutElement>(); if (ble != null) ble.ignoreLayout = true;
            var br = (RectTransform)badge.transform;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;
            var veil = new GameObject("Veil", typeof(RectTransform), typeof(Image));
            veil.transform.SetParent(art.transform, false);
            var vr = (RectTransform)veil.transform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;
            veil.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.03f, 0.72f);
            veil.GetComponent<Image>().raycastTarget = false;
            var claim = Instantiate(name.gameObject, veil.transform);
            claim.name = "Claim";
            var ct = claim.GetComponent<TMP_Text>();
            ct.fontSize = 14; ct.alignment = TextAlignmentOptions.Center; ct.raycastTarget = true; ct.color = Theme.Parchment;
            var cle = claim.GetComponent<LayoutElement>(); if (cle != null) cle.ignoreLayout = true;
            var cr = (RectTransform)claim.transform; cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = new Vector2(8, 4); cr.offsetMax = new Vector2(-8, -4);
            badgeBg.transform.SetAsLastSibling();
            // 어림 줄 (Meta 다음)
            var meta = T(card, "Meta");
            var odds = Instantiate(meta.gameObject, card);
            odds.name = "Odds";
            odds.transform.SetSiblingIndex(meta.transform.GetSiblingIndex() + 1);
            odds.GetComponent<TMP_Text>().fontSize = 13;
            // 설명은 두 줄까지 (자세한 것은 이름 위 툴팁)
            var desc = T(card, "Desc");
            desc.fontSize = 12;
            desc.overflowMode = TextOverflowModes.Ellipsis;
            var dle = desc.GetComponent<LayoutElement>() ?? desc.gameObject.AddComponent<LayoutElement>();
            dle.minHeight = dle.preferredHeight = 32;
            name.fontSize = 16;
            var v = card.GetComponent<VerticalLayoutGroup>();
            if (v != null) { v.spacing = 4; v.padding = new RectOffset(8, 8, 8, 10); v.childForceExpandHeight = false; v.childControlHeight = true; }

            // 카드 칸: 가로 줄 → 격자
            var grid = card.parent;
            var h = grid.GetComponent<HorizontalLayoutGroup>();
            if (h != null) DestroyImmediate(h);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.spacing = new Vector2(10, 10);
            g.startAxis = GridLayoutGroup.Axis.Horizontal;
            g.childAlignment = TextAnchor.UpperLeft;
            var ag = grid.gameObject.AddComponent<AutoGrid>();
            ag.minCell = 250; ag.cellHeight = 266; ag.maxColumns = 6;

            // 카드 위: 거르기 단추와 어림 기준 안내 (FM 세계만)
            var row = new GameObject("DungeonFilter", typeof(RectTransform));
            row.transform.SetParent(grid.parent, false);
            row.transform.SetSiblingIndex(grid.GetSiblingIndex());
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.childForceExpandWidth = false; hl.childControlWidth = hl.childControlHeight = true; hl.childAlignment = TextAnchor.MiddleLeft;
            var rle = row.AddComponent<LayoutElement>(); rle.minHeight = rle.preferredHeight = 32;
            filterRow = (RectTransform)row.transform;
            foreach (var (id, label) in new[] { ("open", "열린 곳"), ("fit", "갈 만한 곳"), ("all", "모두") })
            {
                var b = new GameObject("F_" + id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                b.transform.SetParent(row.transform, false);
                var bl = b.GetComponent<LayoutElement>(); bl.minWidth = bl.preferredWidth = 108; bl.minHeight = bl.preferredHeight = 30;
                b.GetComponent<Button>().targetGraphic = b.GetComponent<Image>();
                var t = Instantiate(wages.gameObject, b.transform).GetComponent<TMP_Text>();
                t.text = label; t.fontSize = 14; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
                var tle = t.GetComponent<LayoutElement>(); if (tle != null) tle.ignoreLayout = true;
                var tr = (RectTransform)t.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
                string f = id;
                b.GetComponent<Button>().onClick.AddListener(() => { filter = f; Refresh(); });
            }
            oddsHead = Instantiate(wages.gameObject, row.transform).GetComponent<TMP_Text>();
            oddsHead.name = "OddsHead";
            oddsHead.fontSize = 13;
            var ole = oddsHead.GetComponent<LayoutElement>() ?? oddsHead.gameObject.AddComponent<LayoutElement>();
            ole.flexibleWidth = 1; ole.ignoreLayout = false;
        }

        static Sprite ArtOf(DungeonDef d)
        {
            if (!artCache.TryGetValue(d.Art, out var sp)) artCache[d.Art] = sp = Resources.Load<Sprite>("Art/dungeon/" + d.Art);
            return sp;
        }

        Ludus Player => S.ludi[S.playerLudusId];

        void Go()
        {
            var q = CurrentQuest();
            string did = (q?.dungeon) ?? dungeon, f = formation, qid = q?.id;
            // StartExpedition은 받은 목록을 원정에 그대로 넣으니 부를 때마다 새 목록을 준다
            var ids = party.ToList();
            msgText = G.Try(s => DungeonLogic.StartExpedition(s, did, ids.ToList(), f, qid));
            if (msgText == null)
            {
                party.Clear();
                questId = null;
            }
            Refresh();
        }

        DungeonQuest CurrentQuest() =>
            questId == null ? null : DungeonLogic.QuestsOf(S).FirstOrDefault(q => q.id == questId && q.status == "open");

        /// <summary>어림에 쓸 원정대: 고른 사람이 있으면 그들, 없으면 보낼 수 있는 가장 강한 다섯</summary>
        List<Gladiator> OddsParty(List<Gladiator> roster, out bool picked)
        {
            picked = party.Count > 0;
            if (picked) return party.Select(id => S.gladiators[id]).ToList();
            return roster.Where(g => DungeonLogic.CannotSend(S, g) == null).OrderByDescending(GladiatorRules.ComputeCA).Take(DungeonLogic.MaxParty).ToList();
        }

        public override void Refresh()
        {
            int t = Calendar.AbsDay(S.season, S.day);
            var me = Player;
            var quest = CurrentQuest();
            if (quest == null) questId = null;
            bool fm = FmRules.Enabled;
            var roster = me.gladiators.Select(id => S.gladiators.TryGetValue(id, out var g) ? g : null).Where(g => g?.alive == true)
                .OrderByDescending(g => Scouting.CaRange(S, g).mid).ToList();
            // 떠났거나 못 가게 된 검투사는 원정대에서 뺀다
            party.RemoveAll(id => !roster.Any(g => g.id == id) || DungeonLogic.CannotSend(S, S.gladiators[id]) != null);
            var oddsParty = OddsParty(roster, out bool picked);

            // 이번 주에 고를 수 있는 던전: FM 세계는 열린 자리(막힌 곳도 보여 준다), 아니면 등급별 순환. 토벌 의뢰의 던전은 따로 끼워 보여 준다
            var sites = fm ? DungeonLogic.Sites(S) : null;
            var ids = fm ? sites.Select(x => x.id).ToList() : DungeonLogic.Rotation(S);
            bool Free(string id) => !fm || DungeonLogic.SiteOf(S, id)?.claimedBy == null;
            if (!ids.Contains(dungeon) || !Free(dungeon)) dungeon = ids.FirstOrDefault(Free) ?? ids[0];
            var d = DungeonLogic.GetDungeon(quest?.dungeon ?? dungeon);
            double wageSum = party.Sum(id => S.gladiators.TryGetValue(id, out var g) ? g.wage : 0) * Math.Ceiling(d.days / 7.0);
            var all = DungeonLogic.ExpeditionsOf(S);
            var active = all.Where(x => x.status == "active").ToList();
            var done = all.Where(x => x.status != "active").Reverse().Take(4).ToList();
            var open = DungeonLogic.QuestsOf(S).Where(q => q.status == "open").ToList();

            int freeCount = fm ? sites.Count(x => x.claimedBy == null) : ids.Count;
            sub.text = $"검투사를 최대 {DungeonLogic.MaxParty}명까지 보내 장비를 얻어 옵니다. 돈이나 명성은 거의 못 얻습니다. 층마다 적 다섯과 난전을 벌이고, 쓰러진 검투사는 그 자리에서 생사가 갈립니다. 적보다 약할수록 잘 죽습니다.\n" +
                (fm ? $"<color={Theme.Hex(Theme.Torch)}>이번 주 던전 {sites.Count}곳 중 {freeCount}곳이 비어 있습니다. 다른 루두스가 먼저 원정을 보낸 곳은 그 주에 들어갈 수 없습니다 · 다음 교체 {Calendar.NextPaydayLabel(S.day)}</color>"
                    : $"<color={Theme.Hex(Theme.Torch)}>열린 던전은 급료일마다 바뀝니다 · 다음 교체 {Calendar.NextPaydayLabel(S.day)}</color>");

            // ── 토벌 의뢰 ──
            questPanel.SetActive(open.Count > 0);
            quests.Sync(open.Count, (row, i) =>
            {
                var q = open[i];
                bool on = questId == q.id;
                row.Find("Fill").GetComponent<Image>().color = on ? QuestOn : new Color(0, 0, 0, 0);
                var qd = DungeonLogic.GetDungeon(q.dungeon);
                var qo = DungeonLogic.Estimate(qd, oddsParty, q.hard, true);
                T(row, "Info/Title").text = $"{Tips.Nation(q.nation, true)}: {Tips.Dungeon(q.dungeon)} 토벌{(qd.grade > 0 ? $" · 위험도 {qd.grade}" : "")}";
                T(row, "Info/Hint").text = $"몬스터 실력 ×{q.hard:0.00} · 현상금 {q.bounty:N0} D · RP +{q.rp} · {q.expiresAt - t}일 안에 출발" + (qo != null ? "   " + OddsLine(qo) : "");
                var b = row.Find("Pick").GetComponent<Button>();
                b.GetComponentInChildren<TMP_Text>().text = on ? "의뢰 취소" : "이 의뢰 맡기";
                string qid = q.id;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => { questId = questId == qid ? null : qid; msgText = null; Refresh(); });
            });

            // ── 거르기·어림 기준 ──
            filterRow.gameObject.SetActive(fm);
            if (fm)
            {
                for (int i = 0; i < filterRow.childCount; i++)
                {
                    var c = filterRow.GetChild(i);
                    if (!c.name.StartsWith("F_")) continue;
                    bool on = c.name == "F_" + filter;
                    c.GetComponent<Image>().color = on ? new Color(Theme.Torch.r, Theme.Torch.g, Theme.Torch.b, 0.35f) : Theme.Panel2;
                    c.GetComponentInChildren<TMP_Text>().color = on ? Theme.Parchment : Theme.TextDim;
                }
                oddsHead.text = oddsParty.Count == 0 ? $"<color={Theme.Hex(Theme.TextDim)}>보낼 수 있는 검투사가 없어 어림을 못 합니다</color>"
                    : $"<color={Theme.Hex(Theme.TextDim)}>생존·완주 어림 기준: {(picked ? $"고른 원정대 {oddsParty.Count}명" : $"보낼 수 있는 상위 {oddsParty.Count}명")} (대략)</color>";
            }

            // ── 던전 카드 ──
            var defs = ids.Concat(quest != null && !ids.Contains(quest.dungeon) ? new[] { quest.dungeon } : new string[0])
                .Select(DungeonLogic.GetDungeon).ToList();
            var odds = defs.ToDictionary(x => x.id, x => DungeonLogic.Estimate(x, oddsParty, quest != null && quest.dungeon == x.id ? quest.hard : 1, quest != null && quest.dungeon == x.id));
            if (fm && quest == null)
                defs = defs.Where(x =>
                {
                    var site = DungeonLogic.SiteOf(S, x.id);
                    if (x.id == d.id) return true;
                    if (filter == "open") return site?.claimedBy == null;
                    if (filter == "fit") return site?.claimedBy == null && odds[x.id] != null && odds[x.id].level <= 2;
                    return true;
                }).ToList();
            defs = defs.OrderBy(x => x.grade > 0 ? x.grade : x.tier).ThenBy(x => x.name).ToList();
            cards.Sync(defs.Count, (b, i) => BindCard(b, defs[i], d, quest, odds[defs[i].id]));

            // ── 원정대 ──
            partyTitle.text = $"◆  원정대 · {party.Count}/{DungeonLogic.MaxParty}명 → {Tips.Dungeon(d.id)}{(d.grade > 0 ? $" (위험도 {d.grade})" : "")}{(quest != null ? " (토벌 의뢰)" : "")}";
            partyEmpty.SetActive(roster.Count == 0);
            partyRows.Sync(roster.Count, (row, i) => BindMember(row, roster[i]));

            // ── 진형과 출발 ──
            formations.Sync(FormationIds.Length, (b, i) =>
            {
                string f = FormationIds[i];
                var def = DungeonLogic.Formations[f];
                bool on = formation == f;
                b.GetComponent<Image>().color = on ? Theme.Torch : FormLine;
                b.transform.Find("Fill").GetComponent<Image>().color = on ? FormFillOn : FormFill;
                T(b.transform, "Name").text = def.name;
                T(b.transform, "Desc").text = def.desc;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => { formation = f; Refresh(); });
            });
            var mine = party.Count > 0 ? DungeonLogic.Estimate(d, party.Select(id => S.gladiators[id]).ToList(), quest?.hard ?? 1, quest != null) : null;
            wages.text = (mine != null ? $"이 원정대로 {d.name}: {OddsLine(mine)}\n" : "") +
                $"원정을 간 동안에도 급료 약 {wageSum:N0} D는 루두스에서 나갑니다. 던전에서 얻는 돈은 층마다 동전 몇십 D뿐입니다.";
            msg.gameObject.SetActive(msgText != null);
            msg.text = msgText ?? "";
            msg.color = Neg;
            bool blocked = fm && quest == null && !Free(d.id);
            start.interactable = party.Count > 0 && !blocked;
            start.GetComponentInChildren<TMP_Text>().text = quest != null ? "토벌대 출발" : "원정 출발";

            // ── 진행 중 원정 ──
            actives.Sync(active.Count, (panel, i) => BindActive(panel, active[i], t));

            // ── 지난 원정 ──
            donePanel.SetActive(done.Count > 0);
            dones.Sync(done.Count, (row, i) =>
            {
                var ex = done[i];
                var dd = DungeonLogic.GetDungeon(ex.dungeon);
                T(row, "Dungeon").text = Tips.Dungeon(dd.id) + (string.IsNullOrEmpty(ex.questId) ? "" : " (토벌)");
                T(row, "Result").text = (ex.status == "done" ? $"<color={Theme.Hex(Theme.Good)}>완주</color>" : $"<color={Theme.Hex(Neg)}>퇴각</color>") + $" · {ex.floor}/{dd.floors}층";
                var doneLoot = T(row, "Loot");
                doneLoot.raycastTarget = true;
                string fights = FightLinks(ex);
                doneLoot.text = LootLine(ex.loot) + (fights.Length > 0 ? (ex.loot.Count > 0 ? "\n" : "") + fights : "");
            });
        }

        static string OddsLine(DungeonLogic.Odds o) =>
            $"<color={Theme.Hex(OddsColors[o.level])}><b>{o.label}</b></color> · 생존 {Pct(o.survive)} · 완주 {Pct(o.clear)}";

        /// <summary>어림이니 5% 단위로 (아주 높거나 낮으면 99%·1%로 막는다)</summary>
        static string Pct(double p)
        {
            double v = Math.Round(p * 20) * 5;
            if (p > 0.99) return "99%+";
            return $"{Math.Clamp(v, 1, 99)}%";
        }

        void BindCard(Button b, DungeonDef x, DungeonDef selected, DungeonQuest quest, DungeonLogic.Odds o)
        {
            bool fm = FmRules.Enabled;
            var site = fm ? DungeonLogic.SiteOf(S, x.id) : null;
            var owner = site?.claimedBy != null && S.ludi.TryGetValue(site.claimedBy, out var l) ? l : null;
            bool ours = owner != null && owner.id == S.playerLudusId;
            bool on = selected.id == x.id;
            bool locked = (quest != null && quest.dungeon != x.id) || owner != null;
            b.GetComponent<Image>().color = on ? Theme.Torch : CardLine;
            b.transform.Find("Fill").GetComponent<Image>().color = TierFill(x.tier);
            b.interactable = !locked;
            b.GetComponent<CanvasGroup>().alpha = locked && owner == null ? 0.35f : 1;
            var art = b.transform.Find("Art").GetComponent<Image>();
            art.sprite = ArtOf(x);
            art.color = art.sprite == null ? new Color(0, 0, 0, 0.4f) : owner != null ? new Color(0.45f, 0.45f, 0.45f) : Color.white;
            var veil = b.transform.Find("Art/Veil").gameObject;
            veil.SetActive(owner != null);
            if (owner != null)
                T(b.transform, "Art/Veil/Claim").text = ours ? $"<color={Theme.Hex(Theme.Torch)}>우리 원정대가 탐험 중</color>"
                    : $"{Tips.Ludus(owner)}\n<size=85%><color={Theme.Hex(Theme.TextDim)}>{Tips.Nation(owner.nation, true)} {owner.tier}부 · 원정 중</color></size>";
            T(b.transform, "Art/Badge/Grade").text = x.grade > 0
                ? $"<color={Theme.Hex(GradeColor(x.grade))}>위험도 {x.grade}</color>"
                : $"{x.tier}등급";
            T(b.transform, "Name").text = x.name;
            T(b.transform, "Desc").text = x.desc;
            T(b.transform, "Meta").text = $"{x.floors}층 · {x.days}일 · 적 <color={Theme.Hex(Theme.Torch)}>{Widgets.Stars(DungeonLogic.FoeCa(x))}</color>";
            T(b.transform, "Odds").text = o == null ? $"<color={Theme.Hex(Theme.TextDim)}>보낼 사람이 없습니다</color>" : OddsLine(o);
            double total = x.loot.Sum();
            var loot = new List<string>();
            for (int k = 0; k < x.loot.Length; k++)
            {
                if (x.loot[k] <= 0) continue;
                string qn = k < Data.QualityNames.Length && Data.QualityNames[k] != "" ? Data.QualityNames[k] : "보통";
                loot.Add($"<color={Theme.Hex(Widgets.QualityColors[Math.Min(k, 3)])}>{qn} {JsMath.Round(x.loot[k] / total * 100)}%</color>");
            }
            T(b.transform, "Loot").text = string.Join("  ", loot);
            string id = x.id;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => { dungeon = id; msgText = null; Refresh(); });
        }

        /// <summary>위험도 색: 낮으면 풀빛, 높으면 핏빛</summary>
        static Color GradeColor(int g) => g <= 2 ? OddsColors[0] : g <= 4 ? OddsColors[1] : g <= 6 ? OddsColors[2] : g <= 8 ? OddsColors[3] : OddsColors[4];

        /// <summary>원정대 후보 한 줄: 누르면 넣고 뺀다 (보낼 수 없으면 흐리게)</summary>
        void BindMember(Button row, Gladiator g)
        {
            string why = DungeonLogic.CannotSend(S, g);
            bool on = party.Contains(g.id);
            row.GetComponent<Image>().color = on ? Picked : new Color(0, 0, 0, 0);
            row.GetComponent<CanvasGroup>().alpha = why != null ? 0.5f : 1;
            row.interactable = why == null;
            T(row.transform, "Check").text = on ? "■" : "□";
            row.GetComponentInChildren<PortraitView>(true).Show(g);
            T(row.transform, "Name/Text").text = Tips.Name(g);
            T(row.transform, "Ca").text = Widgets.CaStars(S, g);
            T(row.transform, "Why").text = why ?? (g.injury != null ? g.injury.desc : "");
            T(row.transform, "Wage").text = $"{g.wage} D";
            string id = g.id;
            row.onClick.RemoveAllListeners();
            row.onClick.AddListener(() =>
            {
                if (!party.Remove(id) && party.Count < DungeonLogic.MaxParty) party.Add(id);
                Refresh();
            });
        }

        void BindActive(RectTransform panel, Expedition ex, int t)
        {
            var dd = DungeonLogic.GetDungeon(ex.dungeon);
            string form = DungeonLogic.Formations[DungeonLogic.PolicyOf(ex.formation)].name;
            T(panel, "Head/Text").text = $"◆  진행 중: {dd.name}{(dd.grade > 0 ? $" (위험도 {dd.grade})" : "")} · {ex.floor}/{dd.floors}층 · {Math.Max(0, ex.endsAt - t)}일 뒤 귀환 · {form}";
            var members = ex.party.Select(id => S.gladiators.TryGetValue(id, out var g) ? g : null).Where(g => g != null).ToList();
            var slots = panel.Find("Pad/Party");
            for (int k = 0; k < slots.childCount; k++)
            {
                var slot = slots.GetChild(k);
                bool has = k < members.Count;
                slot.gameObject.SetActive(has);
                if (has) slot.GetComponentInChildren<PortraitView>(true).Show(members[k]);
            }
            var loot = T(panel, "Pad/Loot");
            loot.gameObject.SetActive(ex.loot.Count > 0);
            loot.text = "전리품: " + LootLine(ex.loot);
            var logText = T(panel, "Pad/Log/Text");
            logText.raycastTarget = true;
            string fights = FightLinks(ex);
            logText.text = (fights.Length > 0 ? fights + "\n" : "") + (ex.log.Count > 0
                ? string.Join("\n", ex.log)
                : $"<color={Theme.Hex(Theme.TextDim)}>아직 첫 층에 도착하지 않았습니다.</color>");
        }

        /// <summary>층별 전투 다시 보기 링크 (누르면 모래 마당 중계 화면에서 그 층 싸움을 보여 준다)</summary>
        static string FightLinks(Expedition ex)
        {
            if (ex.battles == null || ex.battles.Count == 0) return "";
            string torch = Theme.Hex(Theme.Torch);
            // 옛 방식 기록(시드 없음)은 다시 볼 수 없다
            var links = ex.battles.Select((b, i) => (b, i)).Where(x => x.b.seed != null).Select(x =>
                $"<link=\"{DungeonLogic.FightLink(ex, x.i)}\"><color={torch}><u>{x.b.floor}층{(x.b.boss ? " 우두머리" : "")}</u></color></link>").ToList();
            return links.Count == 0 ? "" : "전투 보기:  " + string.Join("   ", links);
        }

        /// <summary>전리품 이름을 등급 색으로 늘어놓는다 (웹판 .tag ItemName)</summary>
        static string LootLine(List<string> items) => string.Join("   ", items.Select(Tips.Item));

        /// <summary>웹판 .dungeon-card.tierN 바탕 (그라디언트 대신 가운데 색)</summary>
        static Color TierFill(int tier) => tier switch
        {
            3 => new Color32(0x26, 0x1e, 0x2d, 0xff),
            4 => new Color32(0x38, 0x1b, 0x13, 0xff),
            5 => new Color32(0x18, 0x1f, 0x2c, 0xff),
            _ => new Color32(0x2f, 0x1f, 0x1d, 0xff),
        };

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();
    }
}
