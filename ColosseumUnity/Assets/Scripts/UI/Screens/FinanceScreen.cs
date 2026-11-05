// 금고 (웹판 FinanceScreen.tsx): 금고·매주 나가는 돈·버틸 기간, 밀린 급료, 장부(최근 80줄). 장부 줄은 하이어라키의 비활성 원본을 복제한다.
using System;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class FinanceScreen : ScreenBase
    {
        static readonly Color Neg = new Color32(0xe0, 0x8b, 0x80, 0xff);
        const string TagNeg = "#d9a29b";

        [Header("요약")]
        [SerializeField] TMP_Text treasury;
        [SerializeField] TMP_Text treasuryHint;
        [SerializeField] TMP_Text weekly;
        [SerializeField] TMP_Text weeklyHint;
        [SerializeField] TMP_Text runway;
        [SerializeField] TMP_Text runwayHint;
        [Header("밀린 급료")]
        [SerializeField] GameObject unpaidRoot;
        [SerializeField] TMP_Text unpaidTags;
        [Header("장부")]
        [SerializeField] GameObject empty;
        [SerializeField] RectTransform entryTemplate;

        TemplateList<RectTransform> entries;

        void Awake()
        {
            entries = new TemplateList<RectTransform>(entryTemplate);
        }

        // ── FM 세계: 스폰서·후원자 목표 (씬 다시 조립 없이 요약 패널 아래에 코드로 붙인다) ──
        // 스폰서 제안은 계약서 카드 셋(문장·종류 표식·이번 시즌 받을 돈 어림·계약 단추). 계약한 뒤에는 그 계약서 한 장에 도장을 찍어 둔다.
        static readonly Color Ink = new Color32(0x2b, 0x1d, 0x12, 0xff);
        static readonly Color InkDim = new Color32(0x45, 0x30, 0x1c, 0xff);
        static readonly Color InkRed = new Color32(0x7a, 0x1e, 0x1a, 0xff);
        static readonly System.Collections.Generic.Dictionary<string, Sprite> sprites = new();
        static Sprite Spr(string path)
        {
            if (!sprites.TryGetValue(path, out var sp)) sprites[path] = sp = Resources.Load<Sprite>("Art/" + path);
            return sp;
        }

        RectTransform patronPanel, cardRow;
        TMP_Text patronHead, patronGoal;

        TMP_Text NewText(Transform parent, string name, float size, Color color, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var t = Instantiate(treasuryHint.gameObject, parent).GetComponent<TMP_Text>();
            t.name = name; t.fontSize = size; t.color = color; t.fontStyle = style; t.alignment = align; t.raycastTarget = false; t.text = "";
            var le = t.GetComponent<LayoutElement>(); if (le != null) le.ignoreLayout = false;
            return t;
        }

        static Image NewImage(Transform parent, string name, Sprite sp, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sp; img.preserveAspect = true; img.raycastTarget = false;
            var le = go.GetComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w; le.minHeight = le.preferredHeight = h; le.flexibleWidth = le.flexibleHeight = 0;
            return img;
        }

        static T Layout<T>(GameObject go, float spacing, RectOffset pad, TextAnchor align) where T : HorizontalOrVerticalLayoutGroup
        {
            var l = go.GetComponent<T>() ?? go.AddComponent<T>();
            l.spacing = spacing; l.padding = pad; l.childAlignment = align;
            l.childControlWidth = l.childControlHeight = true; l.childForceExpandWidth = l.childForceExpandHeight = false;
            return l;
        }

        void BuildPatronPanel()
        {
            var grid = transform.Find("Grid");
            var panel = new GameObject("Patrons", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            panel.transform.SetParent(transform, false);
            panel.transform.SetSiblingIndex(grid != null ? grid.GetSiblingIndex() + 1 : 1);
            var frame = panel.GetComponent<Image>();
            frame.sprite = Spr("ui/brush_frame"); frame.type = Image.Type.Sliced; frame.color = Theme.PanelFrame;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            fill.transform.SetParent(panel.transform, false);
            fill.GetComponent<LayoutElement>().ignoreLayout = true;
            var fr = (RectTransform)fill.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(9, 9); fr.offsetMax = new Vector2(-9, -9);
            var fi = fill.GetComponent<Image>(); fi.sprite = Spr("ui/brush_fill"); fi.type = Image.Type.Sliced; fi.color = Theme.Panel; fi.raycastTarget = false;
            Layout<VerticalLayoutGroup>(panel, 8, new RectOffset(20, 20, 14, 16), TextAnchor.UpperLeft).childForceExpandWidth = true;
            panel.GetComponent<LayoutElement>().flexibleHeight = 0;
            patronPanel = (RectTransform)panel.transform;
            patronHead = NewText(panel.transform, "Head", 17, Theme.Parchment, FontStyles.Bold);
            patronHead.raycastTarget = true;
            patronGoal = NewText(panel.transform, "Goal", 14, Theme.Text);
            patronGoal.raycastTarget = true;
            var row = new GameObject("Cards", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(panel.transform, false);
            Layout<HorizontalLayoutGroup>(row, 16, new RectOffset(0, 0, 2, 2), TextAnchor.UpperLeft);
            cardRow = (RectTransform)row.transform;
        }

        /// <summary>계약서 카드 하나: 문장 · 이름 · 종류 · 조건 · 시즌 어림 · 계약 단추(또는 도장)</summary>
        RectTransform NewContract()
        {
            var go = new GameObject("Contract", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(cardRow, false);
            var img = go.GetComponent<Image>();
            img.sprite = Spr("sponsor/card"); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2f; img.raycastTarget = true;
            var le = go.GetComponent<LayoutElement>(); le.minWidth = le.preferredWidth = 300; le.minHeight = 280; le.flexibleWidth = 1; // 높이는 내용에 맞춘다 (고정하면 글줄이 겹친다)
            Layout<VerticalLayoutGroup>(go, 4, new RectOffset(30, 30, 46, 40), TextAnchor.UpperLeft).childForceExpandWidth = true;
            // 머리: 문장 + 이름·종류
            var head = new GameObject("Head", typeof(RectTransform));
            head.transform.SetParent(go.transform, false);
            Layout<HorizontalLayoutGroup>(head, 10, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);
            NewImage(head.transform, "Emblem", null, 58, 58);
            var names = new GameObject("Names", typeof(RectTransform), typeof(LayoutElement));
            names.transform.SetParent(head.transform, false);
            names.GetComponent<LayoutElement>().flexibleWidth = 1;
            Layout<VerticalLayoutGroup>(names, 2, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft).childForceExpandWidth = true;
            NewText(names.transform, "Name", 15, Ink, FontStyles.Bold);
            var styleRow = new GameObject("Style", typeof(RectTransform));
            styleRow.transform.SetParent(names.transform, false);
            Layout<HorizontalLayoutGroup>(styleRow, 4, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);
            NewImage(styleRow.transform, "Icon", null, 22, 22);
            NewText(styleRow.transform, "Text", 13, InkRed, FontStyles.Bold);
            // 조건과 어림
            NewText(go.transform, "Terms", 14, Ink).lineSpacing = 8;
            NewText(go.transform, "Hint", 12, InkDim);
            // 단추 또는 도장
            var foot = new GameObject("Foot", typeof(RectTransform), typeof(LayoutElement));
            foot.transform.SetParent(go.transform, false);
            foot.GetComponent<LayoutElement>().flexibleHeight = 1;
            Layout<HorizontalLayoutGroup>(foot, 8, new RectOffset(0, 0, 4, 0), TextAnchor.LowerCenter);
            var btn = new GameObject("Sign", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btn.transform.SetParent(foot.transform, false);
            var bi = btn.GetComponent<Image>(); bi.sprite = Spr("ui/brush_button"); bi.type = Image.Type.Sliced; bi.color = Theme.BloodDark;
            btn.GetComponent<Button>().targetGraphic = bi;
            var colors = btn.GetComponent<Button>().colors; colors.highlightedColor = new Color(1.3f, 1.25f, 1.2f); btn.GetComponent<Button>().colors = colors;
            var ble = btn.GetComponent<LayoutElement>(); ble.minWidth = ble.preferredWidth = 190; ble.minHeight = ble.preferredHeight = 36;
            Layout<HorizontalLayoutGroup>(btn, 6, new RectOffset(8, 12, 3, 3), TextAnchor.MiddleCenter);
            NewImage(btn.transform, "Seal", Spr("sponsor/seal"), 28, 28);
            NewText(btn.transform, "Label", 14, Theme.ContinueText, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            var stamp = NewImage(foot.transform, "Stamp", Spr("sponsor/seal"), 54, 54);
            NewText(foot.transform, "Signed", 14, InkRed, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            stamp.transform.localRotation = Quaternion.Euler(0, 0, -12);
            return (RectTransform)go.transform;
        }

        void DrawSponsor(Ludus me)
        {
            if (!FmRules.Enabled) return;
            if (patronPanel == null) BuildPatronPanel();
            var goal = me.boardGoal;
            var offers = me.sponsor == null ? me.sponsorOffers : null;
            bool picking = offers != null && offers.Count > 0;
            var deals = picking ? offers : me.sponsor != null ? new System.Collections.Generic.List<SponsorDeal> { me.sponsor } : new System.Collections.Generic.List<SponsorDeal>();
            patronPanel.gameObject.SetActive(deals.Count > 0 || (goal != null && goal.season == S.season));
            int left = Patrons.SponsorPickUntil - S.day;
            patronHead.text = picking
                ? $"<color={Theme.Hex(Theme.Torch)}>◆ 스폰서를 고르세요</color>  <size=80%><color={Theme.Hex(Theme.TextDim)}>{Calendar.DateLabel(Patrons.SponsorPickUntil)}까지{(left >= 0 ? $" (D-{left})" : "")} · 고르지 않으면 첫 제안(안정형)을 맺습니다</color></size>"
                : $"<color={Theme.Hex(Theme.BronzeHi)}>◆ 후원자와 스폰서</color>";
            patronGoal.text = goal != null && goal.season == S.season
                ? $"<color={Theme.Hex(Theme.BronzeHi)}>후원자 목표</color>  {Patrons.GoalText(goal, me.tier)} · 시즌 시작 전력 {goal.expectedRank}위" +
                  $"  <color={Theme.Hex(Theme.TextDim)}>달성하면 보너스와 명성, 못 하면 다음 시즌 스폰서 지원이 줄어듭니다</color>"
                : "";
            if (picking) patronGoal.text += $"\n<color={Theme.Hex(Theme.TextDim)}>카드의 '이번 시즌 받을 돈'은 남은 급료일 {Patrons.PaydaysLeft(S)}번과 예상 순위 {goal?.expectedRank ?? FmRules.TeamsPerTier / 2}위로 어림한 값입니다</color>";
            double best = deals.Count > 1 ? deals.Max(d => Patrons.SeasonValue(S, me, d)) : -1;
            while (cardRow.childCount < deals.Count) NewContract();
            for (int i = 0; i < cardRow.childCount; i++)
            {
                var c = cardRow.GetChild(i);
                c.gameObject.SetActive(i < deals.Count);
                if (i >= deals.Count) continue;
                var d = deals[i];
                double season = Patrons.SeasonValue(S, me, d);
                c.Find("Head/Emblem").GetComponent<Image>().sprite = Spr("sponsor/" + Patrons.IconOf(d));
                c.Find("Head/Names/Name").GetComponent<TMP_Text>().text = d.name;
                int style = Math.Clamp(d.style, 0, 2);
                c.Find("Head/Names/Style/Icon").GetComponent<Image>().sprite = Spr("sponsor/" + Patrons.StyleIcons[style]);
                c.Find("Head/Names/Style/Text").GetComponent<TMP_Text>().text = Patrons.StyleNames[style] + (picking && season >= best && best > 0 ? $"  <color=#{ColorUtility.ToHtmlStringRGB(InkDim)}>· 이번 시즌 가장 많음</color>" : "");
                c.Find("Terms").GetComponent<TMP_Text>().text =
                    $"급료일마다  <b>{d.weekly:N0} D</b>\n시즌 끝 순위 보너스  최대 <b>{d.rankBonus:N0} D</b>\n" +
                    $"이번 시즌 받을 돈  ≈ <b>{season:N0} D</b>";
                c.Find("Hint").GetComponent<TMP_Text>().text = Patrons.StyleDesc[style];
                var sign = c.Find("Foot/Sign");
                sign.gameObject.SetActive(picking);
                c.Find("Foot/Stamp").gameObject.SetActive(!picking);
                var signed = c.Find("Foot/Signed").GetComponent<TMP_Text>();
                signed.gameObject.SetActive(!picking);
                signed.text = $"계약 중 · {d.season}시즌";
                if (picking)
                {
                    sign.Find("Label").GetComponent<TMP_Text>().text = "이 스폰서와 계약";
                    var btn = sign.GetComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    string id = d.id;
                    btn.onClick.AddListener(() => G.Mutate(s => Patrons.PickSponsor(s, id)));
                }
            }
        }

        public override void Refresh()
        {
            var me = S.ludi[S.playerLudusId];
            // 급료일에 실제로 나가는 돈 전부 (Economy.Payday와 같은 계산)
            var bill = Economy.NextPayBill(S, me);
            double week = bill.Weekly;
            double weeks = week > 0 ? Math.Floor(Math.Max(0, me.treasury - bill.arrears) / week) : 99;

            treasury.text = $"{me.treasury:N0} D";
            treasury.color = me.treasury < 0 ? Neg : Theme.Parchment;
            string due = $"다음 급료일 {Calendar.NextPaydayLabel(S.day)}에 {bill.Total:N0} D가 나갑니다";
            if (bill.grant > 0) due += $" ({(FmRules.Enabled ? "지원금·스폰서" : "하부 리그 지원금")} +{bill.grant:N0} D가 먼저 들어옵니다)";
            treasuryHint.text = Tips.Pay(bill.Net > me.treasury ? $"<color={Theme.Hex(Neg)}>{due} · {bill.Net - me.treasury:N0} D 모자랍니다</color>" : due);
            weekly.text = $"{week:N0} D";
            weeklyHint.text = $"주급 {bill.wages:N0} · 식비 {bill.food:N0} · 시설 {bill.upkeep:N0} · 식솔 {bill.staff:N0} D" +
                (bill.arrears > 0 ? $"\n<color={Theme.Hex(Neg)}>밀린 급료 {bill.arrears:N0} D도 다음 급료일에 함께 나갑니다</color>" : "");
            runway.text = $"{StateUtil.Num(weeks)}주";
            runway.color = weeks < 4 ? Neg : Theme.Parchment;
            runwayHint.text = "수입 없이 매주 나가는 돈만 낼 때";

            DrawSponsor(me);

            var unpaid = me.gladiators.Select(id => S.gladiators[id]).Where(g => g.unpaidWeeks > 0).ToList();
            unpaidRoot.SetActive(unpaid.Count > 0);
            unpaidTags.text = string.Join("   ", unpaid.Select(g =>
                $"<color={TagNeg}>{Tips.Name(g)} {StateUtil.Num(g.unpaidWeeks)}회 · {StateUtil.Num(g.wage * g.unpaidWeeks)} D</color>"));

            var list = S.ledger.Take(80).ToList();
            empty.SetActive(list.Count == 0);
            entries.Sync(list.Count, (row, i) =>
            {
                var e = list[i];
                var d = Calendar.FromAbs(e.at);
                T(row, "Date").text = $"시즌 {d.season} · {Calendar.DateLabel(d.day)}";
                T(row, "Desc").text = e.desc;
                var amount = T(row, "Amount");
                amount.text = (e.amount >= 0 ? "+" : "") + e.amount.ToString("N0");
                amount.color = e.amount >= 0 ? Theme.Good : Neg;
            });
        }

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();
    }
}
