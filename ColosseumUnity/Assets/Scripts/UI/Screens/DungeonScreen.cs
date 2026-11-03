// 던전 (웹판 DungeonScreen.tsx): 나라의 토벌 의뢰, 던전 고르기, 원정대(최대 5명)·진형을 꾸려 출발, 진행 중·지난 원정.
// 카드·줄·패널은 하이어라키의 비활성 원본을 복제한다.
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
        static readonly string[] FormationIds = { "shieldwall", "wedge", "flank", "spread" };
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);
        static readonly Color CardLine = new Color32(0x6b, 0x4a, 0x34, 0xff);
        static readonly Color FormLine = new Color32(0x5a, 0x3f, 0x32, 0xff);
        static readonly Color FormFill = new Color32(0x2a, 0x1c, 0x1a, 0xff);
        static readonly Color FormFillOn = new Color32(0x4a, 0x2a, 0x20, 0xff);
        static readonly Color QuestOn = new Color(142 / 255f, 42 / 255f, 34 / 255f, 0.25f);
        static readonly Color Picked = new Color(242 / 255f, 163 / 255f, 58 / 255f, 0.1f);

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
        string dungeon = "sewer", questId, formation = "shieldwall", msgText;
        readonly List<string> party = new();

        void Awake()
        {
            quests = new TemplateList<RectTransform>(questTemplate);
            cards = new TemplateList<Button>(cardTemplate);
            partyRows = new TemplateList<Button>(partyTemplate);
            formations = new TemplateList<Button>(formationTemplate);
            actives = new TemplateList<RectTransform>(activeTemplate);
            dones = new TemplateList<RectTransform>(doneTemplate);
            start.onClick.AddListener(Go);
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

        public override void Refresh()
        {
            int t = Calendar.AbsDay(S.season, S.day);
            var me = Player;
            var quest = CurrentQuest();
            if (quest == null) questId = null;
            var d = DungeonLogic.GetDungeon(quest?.dungeon ?? dungeon);
            var roster = me.gladiators.Select(id => S.gladiators.TryGetValue(id, out var g) ? g : null).Where(g => g?.alive == true)
                .OrderByDescending(GladiatorRules.ComputeCA).ToList();
            // 떠났거나 못 가게 된 검투사는 원정대에서 뺀다
            party.RemoveAll(id => !roster.Any(g => g.id == id) || DungeonLogic.CannotSend(S, S.gladiators[id]) != null);
            double wageSum = party.Sum(id => S.gladiators.TryGetValue(id, out var g) ? g.wage : 0) * Math.Ceiling(d.days / 7.0);
            var all = DungeonLogic.ExpeditionsOf(S);
            var active = all.Where(x => x.status == "active").ToList();
            var done = all.Where(x => x.status != "active").Reverse().Take(4).ToList();
            var open = DungeonLogic.QuestsOf(S).Where(q => q.status == "open").ToList();

            sub.text = $"검투사를 최대 {DungeonLogic.MaxParty}명까지 보내 장비를 얻어 옵니다. 돈이나 명성은 거의 못 얻고, 쓰러진 검투사는 목숨을 잃기도 합니다. 층마다 5:5로 진형을 짜서 싸웁니다.";

            // ── 토벌 의뢰 ──
            questPanel.SetActive(open.Count > 0);
            quests.Sync(open.Count, (row, i) =>
            {
                var q = open[i];
                bool on = questId == q.id;
                row.Find("Fill").GetComponent<Image>().color = on ? QuestOn : new Color(0, 0, 0, 0);
                T(row, "Info/Title").text = $"{Tips.Nation(q.nation, true)}: {Tips.Dungeon(q.dungeon)} 토벌";
                T(row, "Info/Hint").text = $"몬스터 실력 ×{q.hard:0.00} · 현상금 {q.bounty:N0} D · RP +{q.rp} · {q.expiresAt - t}일 안에 출발";
                var b = row.Find("Pick").GetComponent<Button>();
                b.GetComponentInChildren<TMP_Text>().text = on ? "의뢰 취소" : "이 의뢰 맡기";
                string qid = q.id;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => { questId = questId == qid ? null : qid; msgText = null; Refresh(); });
            });

            // ── 던전 카드 ──
            var defs = DungeonLogic.Dungeons;
            cards.Sync(defs.Count, (b, i) =>
            {
                var x = defs[i];
                bool on = d.id == x.id;
                bool locked = quest != null && quest.dungeon != x.id;
                b.GetComponent<Image>().color = on ? Theme.Torch : CardLine;
                b.transform.Find("Fill").GetComponent<Image>().color = TierFill(x.tier);
                b.interactable = !locked;
                b.GetComponent<CanvasGroup>().alpha = locked ? 0.35f : 1;
                T(b.transform, "Name").text = x.name;
                T(b.transform, "Desc").text = x.desc;
                T(b.transform, "Meta").text = $"난이도 <color={Theme.Hex(Theme.Torch)}>{Widgets.Stars(x.level * 10)}</color>   {x.floors}층 · {x.days}일   사망률 {JsMath.Round(x.death * 100)}%";
                double total = x.loot.Sum();
                var loot = new List<string>();
                for (int k = 0; k < x.loot.Length; k++)
                {
                    if (x.loot[k] <= 0) continue;
                    string qn = k < Data.QualityNames.Length && Data.QualityNames[k] != "" ? Data.QualityNames[k] : "보통";
                    loot.Add($"<color={Theme.Hex(Widgets.QualityColors[Math.Min(k, 3)])}>{qn} {JsMath.Round(x.loot[k] / total * 100)}%</color>");
                }
                T(b.transform, "Loot").text = string.Join("   ", loot);
                string id = x.id;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => { dungeon = id; Refresh(); });
            });

            // ── 원정대 ──
            partyTitle.text = $"◆  원정대 · {party.Count}/{DungeonLogic.MaxParty}명 → {d.name}{(quest != null ? " (토벌 의뢰)" : "")}";
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
            wages.text = $"원정을 간 동안에도 급료 약 {wageSum:N0} D는 루두스에서 나갑니다. 던전에서 얻는 돈은 층마다 동전 몇십 D뿐입니다.";
            msg.gameObject.SetActive(msgText != null);
            msg.text = msgText ?? "";
            msg.color = Neg;
            start.interactable = party.Count > 0;
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
                T(row, "Loot").text = LootLine(ex.loot);
            });
        }

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
            T(row.transform, "Name/Text").text = g.name;
            T(row.transform, "Ca").text = Widgets.Stars(GladiatorRules.ComputeCA(g));
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
            string form = DungeonLogic.Formations.TryGetValue(ex.formation, out var fd) ? fd.name : ex.formation;
            T(panel, "Head/Text").text = $"◆  진행 중: {dd.name} · {ex.floor}/{dd.floors}층 · {Math.Max(0, ex.endsAt - t)}일 뒤 귀환 · {form}";
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
            T(panel, "Pad/Log/Text").text = ex.log.Count > 0
                ? string.Join("\n", ex.log)
                : $"<color={Theme.Hex(Theme.TextDim)}>아직 첫 층에 도착하지 않았습니다.</color>";
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
