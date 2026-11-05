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

        // ── FM 세계: 스폰서·후원자 목표 줄 (씬 다시 조립 없이 금고 안내 아래에 코드로 붙인다) ──
        TMP_Text sponsorText;
        RectTransform sponsorButtons;

        void DrawSponsor(Ludus me)
        {
            if (!FmRules.Enabled) return;
            if (sponsorText == null)
            {
                var parent = treasuryHint.transform.parent;
                var go = Instantiate(treasuryHint.gameObject, parent);
                go.name = "Sponsor";
                sponsorText = go.GetComponent<TMP_Text>();
                sponsorText.raycastTarget = true;
                var row = new GameObject("SponsorButtons", typeof(RectTransform));
                row.transform.SetParent(parent, false);
                var h = row.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 8; h.childForceExpandWidth = false; h.childControlWidth = h.childControlHeight = true; h.childForceExpandHeight = false;
                var le = row.AddComponent<LayoutElement>();
                le.minHeight = le.preferredHeight = 34;
                sponsorButtons = (RectTransform)row.transform;
            }
            var goal = me.boardGoal;
            string goalLine = goal != null && goal.season == S.season ? $"<color={Theme.Hex(Theme.BronzeHi)}>후원자 목표</color> {Patrons.GoalText(goal, me.tier)} · 시즌 시작 전력 {goal.expectedRank}위" : "";
            var offers = me.sponsor == null ? me.sponsorOffers : null;
            sponsorText.text = goalLine + "\n" + (me.sponsor != null
                ? $"<color={Theme.Hex(Theme.BronzeHi)}>스폰서</color> {me.sponsor.name} · 급료일마다 {me.sponsor.weekly:N0} D · 시즌 끝 순위 보너스 최대 {me.sponsor.rankBonus:N0} D"
                : offers != null && offers.Count > 0 ? $"<color={Theme.Hex(Theme.Torch)}>스폰서를 고르세요</color> ({Calendar.DateLabel(Patrons.SponsorPickUntil)}까지, 안 고르면 첫 제안)" : "");
            // 후보 버튼: 사람 수만큼 만들고 남는 것은 숨긴다
            int n = offers?.Count ?? 0;
            while (sponsorButtons.childCount < n)
            {
                var b = new GameObject("Offer", typeof(RectTransform));
                b.transform.SetParent(sponsorButtons, false);
                var img = b.AddComponent<Image>();
                img.color = Theme.Panel2;
                b.AddComponent<Button>().targetGraphic = img;
                var ble = b.AddComponent<LayoutElement>();
                ble.minWidth = ble.preferredWidth = 250; ble.minHeight = ble.preferredHeight = 34;
                var t = Instantiate(treasuryHint.gameObject, b.transform).GetComponent<TMP_Text>();
                t.raycastTarget = false;
                t.fontSize = 13;
                t.alignment = TextAlignmentOptions.Center;
                var tr = (RectTransform)t.transform;
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(6, 2); tr.offsetMax = new Vector2(-6, -2);
                var tle = t.GetComponent<LayoutElement>();
                if (tle != null) tle.ignoreLayout = true;
            }
            sponsorButtons.gameObject.SetActive(n > 0);
            for (int i = 0; i < sponsorButtons.childCount; i++)
            {
                var c = sponsorButtons.GetChild(i);
                c.gameObject.SetActive(i < n);
                if (i >= n) continue;
                var o = offers[i];
                c.GetComponentInChildren<TMP_Text>().text = $"{o.name}\n<size=85%>주 {o.weekly:N0} D · 보너스 최대 {o.rankBonus:N0} D</size>";
                var btn = c.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                string id = o.id;
                btn.onClick.AddListener(() => G.Mutate(s => Patrons.PickSponsor(s, id)));
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
