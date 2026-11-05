// 안뜰 (웹판 OverviewScreen.tsx): 재정·리그·숙소 요약과 주력 검투사 셋.
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class OverviewScreen : ScreenBase
    {
        [SerializeField] TMP_Text title;
        [Header("재정")]
        [SerializeField] TMP_Text treasury;
        [SerializeField] TMP_Text wages;
        [SerializeField] TMP_Text runway;
        [Header("리그")]
        [SerializeField] TMP_Text rank;
        [SerializeField] TMP_Text rpGap;
        [SerializeField] Button leagueButton;
        [Header("숙소")]
        [SerializeField] RectTransform moraleBar;
        [SerializeField] TMP_Text morale;
        [SerializeField] TMP_Text fit;
        [SerializeField] TMP_Text injured;
        [Header("주력 검투사 (원본은 하이어라키에 비활성)")]
        [SerializeField] Button bestTemplate;

        TemplateList<Button> best;

        void Awake()
        {
            best = new TemplateList<Button>(bestTemplate);
            leagueButton.onClick.AddListener(() => G.Go("league"));
        }

        public override void Refresh()
        {
            var player = S.ludi[S.playerLudusId];
            var roster = player.gladiators.Select(id => S.gladiators[id]).ToList();
            double wageSum = roster.Sum(g => g.wage);
            int avgMorale = roster.Count > 0 ? (int)JsMath.Round(roster.Sum(g => g.morale) / roster.Count) : 0;
            var tier = S.ludi.Values.Where(l => l.tier == player.tier && l.nation == S.nation).OrderByDescending(l => l.rp).ToList();
            int r = tier.FindIndex(l => l.id == player.id);
            int weeks = wageSum > 0 ? (int)System.Math.Floor(player.treasury / wageSum) : 99;

            title.text = Tips.Ludus(player);
            treasury.text = $"{player.treasury:N0} D";
            wages.text = $"주급 합계 {wageSum} D/주";
            runway.text = $"지금 금고로 <b>{weeks}주</b> 동안 버틸 수 있습니다";
            runway.color = weeks < 8 ? new Color32(0xe0, 0x8b, 0x80, 0xff) : Theme.Text;

            rank.text = $"{player.tier}부 {r + 1}위";
            rpGap.text = $"RP {player.rp} · 1위와 {tier[0].rp - player.rp}점 차이" +
                (player.boardGoal != null && player.boardGoal.season == S.season ? $"\n<color={Theme.Hex(Theme.BronzeHi)}>후원자 목표</color> {Patrons.GoalText(player.boardGoal, player.tier)}" : "");

            Widgets.SetBar(moraleBar, avgMorale);
            morale.text = avgMorale.ToString();
            fit.text = $"출전 가능 {roster.Count(GladiatorRules.CanFight)} / {roster.Count}명";
            injured.text = $"부상자 {roster.Count(g => g.injury != null)}명";

            var top = roster.OrderByDescending(g => Scouting.CaRange(S, g).mid).Take(3).ToList();
            best.Sync(top.Count, (b, i) =>
            {
                var g = top[i];
                b.transform.Find("Name").GetComponent<TMP_Text>().text = g.name;
                b.transform.Find("Stars").GetComponent<TMP_Text>().text = Widgets.CaStars(S, g);
                b.GetComponentInChildren<PortraitView>(true).Show(g);
                b.onClick.RemoveAllListeners();
                string id = g.id;
                b.onClick.AddListener(() => G.ShowGladiator(id));
            });
        }
    }
}
