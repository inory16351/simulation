// 금고 (웹판 FinanceScreen.tsx): 금고·매주 나가는 돈·버틸 기간, 밀린 급료, 장부(최근 80줄). 장부 줄은 하이어라키의 비활성 원본을 복제한다.
using System;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;

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
            if (bill.grant > 0) due += $" (하부 리그 지원금 +{bill.grant:N0} D가 먼저 들어옵니다)";
            treasuryHint.text = Tips.Pay(bill.Net > me.treasury ? $"<color={Theme.Hex(Neg)}>{due} · {bill.Net - me.treasury:N0} D 모자랍니다</color>" : due);
            weekly.text = $"{week:N0} D";
            weeklyHint.text = $"주급 {bill.wages:N0} · 식비 {bill.food:N0} · 시설 {bill.upkeep:N0} · 식솔 {bill.staff:N0} D" +
                (bill.arrears > 0 ? $"\n<color={Theme.Hex(Neg)}>밀린 급료 {bill.arrears:N0} D도 다음 급료일에 함께 나갑니다</color>" : "");
            runway.text = $"{StateUtil.Num(weeks)}주";
            runway.color = weeks < 4 ? Neg : Theme.Parchment;
            runwayHint.text = "수입 없이 매주 나가는 돈만 낼 때";

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
