// 대륙 정세 (웹판 WorldScreen.tsx + components/WarPanel.tsx): 나라 탭(나라 카드·설명·리그 특징·관계)과
// 전쟁·용병 탭(진행 중인 전쟁, 전장에 나간 검투사, 용병 의뢰 게시판, 나라별 호감도). 줄은 하이어라키의 비활성 원본을 복제한다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class WorldScreen : ScreenBase
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);
        static readonly Color Warn = new Color32(0xe0, 0x8b, 0x80, 0xff);
        static readonly Color CardLine = new Color32(0x4a, 0x3b, 0x2c, 0xff);

        [SerializeField] TMP_Text sub;
        [SerializeField] Button tabNations;
        [SerializeField] Button tabWar;

        [Header("나라")]
        [SerializeField] GameObject nationsRoot;
        [Tooltip("나라 카드 원본: Fill, Crest, Info/Name, Info/League")]
        [SerializeField] Button cardTemplate;
        [SerializeField] TMP_Text nationTitle;
        [SerializeField] TMP_Text nationMeta;
        [SerializeField] TMP_Text nationLore;
        [SerializeField] TMP_Text rulesText;
        [SerializeField] TMP_Text stylesText;
        [Tooltip("관계 줄 원본: Name, Value, Label")]
        [SerializeField] RectTransform relTemplate;

        [Header("전쟁·용병")]
        [SerializeField] GameObject warRoot;
        [SerializeField] GameObject warEmpty;
        [Tooltip("전쟁 줄 원본: SideA(Crest, Name), Bar(A, B), SideB, Remain")]
        [SerializeField] RectTransform warTemplate;
        [SerializeField] GameObject deployedRoot;
        [Tooltip("파견 줄 원본: Desc, Names/GladTemplate, Hint")]
        [SerializeField] RectTransform deployTemplate;
        [SerializeField] GameObject boardEmpty;
        [Tooltip("의뢰 카드 원본: Head, Blocked, Picks/PickTemplate, Err, SendRow/Send")]
        [SerializeField] RectTransform contractTemplate;
        [Tooltip("호감도 줄 원본: Name, Value, Hint")]
        [SerializeField] RectTransform favorTemplate;

        TemplateList<Button> cards;
        TemplateList<RectTransform> relRows, warRows, deployRows, contractRows, favorRows;
        // 줄 안의 목록 (파견된 검투사, 의뢰 카드의 고르기): 줄마다 원본이 하나씩
        readonly Dictionary<Button, TemplateList<Button>> nested = new();
        readonly Dictionary<string, Sprite> crests = new();
        // 화면 상태 (웹판 useState): 탭, 고른 나라, 의뢰별 고른 검투사·오류
        string tab = "nations", sel;
        readonly Dictionary<string, List<string>> picked = new();
        readonly Dictionary<string, string> errs = new();

        void Awake()
        {
            cards = new TemplateList<Button>(cardTemplate);
            relRows = new TemplateList<RectTransform>(relTemplate);
            warRows = new TemplateList<RectTransform>(warTemplate);
            deployRows = new TemplateList<RectTransform>(deployTemplate);
            contractRows = new TemplateList<RectTransform>(contractTemplate);
            favorRows = new TemplateList<RectTransform>(favorTemplate);
            tabNations.onClick.AddListener(() => { tab = "nations"; Refresh(); });
            tabWar.onClick.AddListener(() => { tab = "war"; Refresh(); });
        }

        public override void Refresh()
        {
            sel ??= S.nation;
            var home = Data.Nation(S.nation);
            sub.text = $"베르시아 대륙의 여섯 나라. 지금 우리 루두스는 {home.name}의 {home.league}에서 싸운다.";
            int wars = WarLogic.ActiveWars(S).Count;
            Seg(tabNations, tab == "nations");
            Seg(tabWar, tab == "war");
            tabWar.GetComponentInChildren<TMP_Text>().text = "전쟁·용병" + (wars > 0 ? $" ({wars})" : "");
            nationsRoot.SetActive(tab == "nations");
            warRoot.SetActive(tab == "war");
            if (tab == "war") DrawWar();
            else DrawNations();
        }

        // ── 나라 ──

        static Color RelColor(double v) => v <= -50 ? new Color32(0xe0, 0x6a, 0x5a, 0xff) : v < 0 ? new Color32(0xc9, 0xa0, 0x7a, 0xff) : v >= 30 ? Theme.Good : Theme.Text;
        static string RelLabel(double v) => v <= -50 ? "적대" : v < 0 ? "긴장" : v >= 30 ? "우호" : "중립";

        void DrawNations()
        {
            var list = Data.Nations;
            cards.Sync(list.Count, (b, i) =>
            {
                var x = list[i];
                bool on = x.id == sel;
                b.transform.Find("Crest").GetComponent<Image>().sprite = Crest(x.id);
                T(b.transform, "Info/Name").text = x.@short;
                T(b.transform, "Info/League").text = (x.id == S.nation ? $"<color={Theme.Hex(Theme.Torch)}>우리 나라</color> · " : "") + x.league;
                // 웹판 .nation-card: 나라 색이 옅게 깔린 바탕, 고르면 횃불색 테두리
                b.GetComponent<Image>().color = on ? Theme.Torch : CardLine;
                b.transform.Find("Fill").GetComponent<Image>().color = Color.Lerp(Theme.Bg2, NationColor(x.id), on ? 0.45f : 0.3f);
                string id = x.id;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => { sel = id; Refresh(); });
            });

            var n = Data.Nation(sel);
            nationTitle.text = "◆  " + n.name;
            nationMeta.text = $"수도 {n.capital} · 대표 투기장 {n.arena} · 리그 「{n.league}」";
            nationLore.text = n.lore;
            rulesText.text = string.Join("\n", n.ruleText.Select(t => "· " + t));
            stylesText.text = string.Join("   ", n.styles.Select(st => Data.Style(st)?.name ?? st));

            var others = list.Where(x => x.id != sel).ToList();
            relRows.Sync(others.Count, (row, i) =>
            {
                var x = others[i];
                double v = WarLogic.Relation(S, sel, x.id);
                var c = RelColor(v);
                T(row, "Name").text = x.@short;
                var val = T(row, "Value");
                val.text = v > 0 ? $"+{N(v)}" : N(v);
                val.color = c;
                var label = T(row, "Label");
                label.text = RelLabel(v);
                label.color = c;
            });
        }

        // ── 전쟁·용병 ──

        void DrawWar()
        {
            int t = Calendar.AbsDay(S.season, S.day);
            var wars = WarLogic.ActiveWars(S);
            warEmpty.SetActive(wars.Count == 0);
            warRows.Sync(wars.Count, (row, i) =>
            {
                var w = wars[i];
                float pct = Mathf.Clamp01((float)((w.front + 100) / 200));
                row.Find("SideA/Crest").GetComponent<Image>().sprite = Crest(w.a);
                T(row, "SideA/Name").text = Data.Nation(w.a).@short;
                row.Find("SideB/Crest").GetComponent<Image>().sprite = Crest(w.b);
                T(row, "SideB/Name").text = Data.Nation(w.b).@short;
                var a = (RectTransform)row.Find("Bar/A");
                a.anchorMax = new Vector2(pct, 1);
                a.GetComponent<Image>().color = NationColor(w.a);
                var b = (RectTransform)row.Find("Bar/B");
                b.anchorMin = new Vector2(pct, 0);
                b.GetComponent<Image>().color = NationColor(w.b);
                double left = w.endsAt - t;
                T(row, "Remain").text = left > 0 ? $"최대 {N(left)}일 더" : "곧 끝난다";
            });

            // 전장에 나간 검투사
            var active = S.contracts.Where(c => c.status == "active").ToList();
            deployedRoot.SetActive(active.Count > 0);
            deployRows.Sync(active.Count, (row, i) =>
            {
                var c = active[i];
                T(row, "Desc").text = $"{Data.Nation(c.employer).@short} · {SpecName(c.type)}";
                var gl = c.assigned.Select(id => Glad(id)).Where(g => g != null && g.alive).ToList();
                Nested(row, "Names/GladTemplate").Sync(gl.Count, (b, j) =>
                {
                    var g = gl[j];
                    b.GetComponentInChildren<PortraitView>(true).Show(g);
                    T(b.transform, "Name").text = g.name;
                    string id = g.id;
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(() => G.OpenGladiator(id));
                });
                T(row, "Hint").text = $"전공 {N(c.merit)} · {Calendar.DateLabel(Calendar.FromAbs(c.endsAt ?? 0).day)} 귀환";
            });

            // 용병 의뢰 게시판
            var open = S.contracts.Where(c => c.status == "open").ToList();
            boardEmpty.SetActive(open.Count == 0);
            var roster = S.ludi[S.playerLudusId].gladiators.Select(id => Glad(id)).Where(g => g != null && g.alive).ToList();
            contractRows.Sync(open.Count, (card, i) => DrawContract(card, open[i], roster, t));

            // 나라별 호감도
            var nations = Data.Nations;
            favorRows.Sync(nations.Count, (row, i) =>
            {
                var n = nations[i];
                double f = WarLogic.FavorOf(S, n.id);
                T(row, "Name").text = n.@short + (n.id == S.nation ? $"  <color={Theme.Hex(Theme.TextDim)}><size=80%>[조국]</size></color>" : "");
                var val = T(row, "Value");
                val.text = f > 0 ? $"+{N(f)}" : N(f);
                val.color = f >= 30 ? Theme.Good : f <= -30 ? Warn : Theme.Text;
                T(row, "Hint").text = f <= -60 ? "적대: 그 나라 관중이 야유한다" : f >= 50 ? "우호: 의뢰를 먼저 받는다" : "";
            });
        }

        /// <summary>의뢰 카드 (웹판 ContractCard): 고른 검투사는 의뢰마다 따로 기억한다</summary>
        void DrawContract(RectTransform card, Contract c, List<Gladiator> roster, int t)
        {
            WarLogic.ContractTypes.TryGetValue(c.type, out var spec);
            bool levy = c.type == "levy";
            card.GetComponent<Image>().color = levy ? Theme.Blood : Theme.Line;
            card.Find("Head/Crest").GetComponent<Image>().sprite = Crest(c.employer);
            T(card, "Head/Info/Name").text = $"{Data.Nation(c.employer).@short} · {spec?.name ?? c.type}" +
                (levy ? $"  <size=75%><color={Theme.Hex(Neg)}>[징발령]</color></size>" : "");
            T(card, "Head/Info/Desc").text = $"{spec?.desc} · 적: {Data.Nation(c.enemy).@short}";
            T(card, "Head/Terms/Pay").text = $"1인당 <b>{c.pay:N0} D</b> (선금 30%)";
            T(card, "Head/Terms/Detail").text = $"{N(c.slots)}명까지 · {N(c.days)}일 · CA {N(c.minCA)} 이상 · 마감 {N(c.expiresAt - t)}일";

            string blocked = WarLogic.CanTake(S, c);
            var blockedText = T(card, "Blocked");
            blockedText.gameObject.SetActive(blocked != null);
            blockedText.text = blocked ?? "";
            card.Find("Picks").gameObject.SetActive(blocked == null);
            card.Find("SendRow").gameObject.SetActive(blocked == null);

            if (!picked.TryGetValue(c.id, out var pick)) picked[c.id] = pick = new List<string>();
            errs.TryGetValue(c.id, out var err);
            var errText = T(card, "Err");
            errText.gameObject.SetActive(blocked == null && err != null);
            errText.text = err ?? "";
            string cid = c.id;

            Nested(card, "Picks/PickTemplate").Sync(blocked == null ? roster.Count : 0, (b, j) =>
            {
                var g = roster[j];
                string why = WarLogic.Eligible(S, g, c);
                bool on = pick.Contains(g.id);
                T(b.transform, "Check").text = on ? "■" : "□";
                b.GetComponentInChildren<PortraitView>(true).Show(g);
                T(b.transform, "Name").text = g.name;
                // 웹판은 title(마우스 올림)로 이유를 보였다. 여기선 별 자리에 옅게
                T(b.transform, "Stars").text = why != null ? $"<color={Theme.Hex(Theme.TextFaint)}>{why}</color>" : Widgets.Stars(GladiatorRules.ComputeCA(g));
                b.interactable = why == null;
                b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
                b.transform.Find("Fill").GetComponent<Image>().color = on ? Color.Lerp(Theme.Bg2, Theme.Torch, 0.12f) : Theme.Bg2;
                string id = g.id;
                double slots = c.slots;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() =>
                {
                    if (pick.Contains(id)) pick.Remove(id);
                    else if (pick.Count < slots) pick.Add(id);
                    Refresh();
                });
            });

            var send = card.Find("SendRow/Send").GetComponent<Button>();
            send.GetComponentInChildren<TMP_Text>().text = $"{pick.Count}명 파견";
            send.interactable = pick.Count > 0;
            // 대리 결투는 웹판 .btn.danger
            bool duel = c.type == "duel";
            send.GetComponent<Image>().color = duel ? Theme.Blood : Theme.Torch;
            send.transform.Find("Fill").GetComponent<Image>().color = duel ? Theme.BloodDark : Theme.ContinueBg;
            send.onClick.RemoveAllListeners();
            send.onClick.AddListener(() => Send(cid));
        }

        void Send(string cid)
        {
            var c = S.contracts.FirstOrDefault(x => x.id == cid);
            if (c == null || !picked.TryGetValue(cid, out var pick)) return;
            foreach (var id in pick)
            {
                var g = Glad(id);
                string why = g == null ? "검투사가 없다" : WarLogic.Eligible(S, g, c);
                if (why != null) { errs[cid] = $"{g?.name ?? id}: {why}"; Refresh(); return; }
            }
            errs.Remove(cid);
            var ids = pick.ToList();
            var e = G.Try(s => WarLogic.TakeContract(s, cid, ids));
            if (e != null) errs[cid] = e;
            else picked.Remove(cid);
            Refresh();
        }

        // ── 공용 ──

        Gladiator Glad(string id) => id != null && S.gladiators.TryGetValue(id, out var g) ? g : null;

        static string SpecName(string type) => WarLogic.ContractTypes.TryGetValue(type ?? "", out var sp) ? sp.name : type;

        TemplateList<Button> Nested(Transform row, string path)
        {
            var tpl = row.Find(path).GetComponent<Button>();
            if (!nested.TryGetValue(tpl, out var list)) nested[tpl] = list = new TemplateList<Button>(tpl);
            return list;
        }

        Sprite Crest(string nation)
        {
            if (!crests.TryGetValue(nation, out var sp)) crests[nation] = sp = Resources.Load<Sprite>($"Art/crest/{nation}");
            return sp;
        }

        static Color NationColor(string id) => ColorUtility.TryParseHtmlString(Data.Nation(id).color, out var c) ? c : Theme.Bronze;

        static string N(double v) => System.Math.Round(v).ToString();
        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        /// <summary>웹판 .seg 버튼: 고른 것만 횃불색</summary>
        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
