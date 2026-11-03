// 서열표 (웹판 LeagueScreen.tsx): 리그별 RP 순위. Unity판은 모든 나라 1~4부를 보고, 승강 구역과 지난 시즌 승강 기록을 함께 보여 준다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class LeagueScreen : ScreenBase
    {
        static readonly Dictionary<string, string> PersonalityNames = new()
        {
            ["aggressive"] = "공격형", ["stable"] = "안정형", ["grudge"] = "원한형", ["merchant"] = "상인형", ["developer"] = "육성형",
        };

        [SerializeField] Button tier1;
        [SerializeField] Button tier2;
        [SerializeField] Button tier3;
        [SerializeField] Button tier4;
        [SerializeField] TMP_Dropdown nation;
        [Header("줄 원본 (하이어라키에 비활성)")]
        [SerializeField] RectTransform rowTemplate;

        TemplateList<RectTransform> rows;
        int tier;
        string nationId;

        Button[] Tabs => new[] { tier1, tier2, tier3, tier4 };

        void Awake()
        {
            rows = new TemplateList<RectTransform>(rowTemplate);
            var tabs = Tabs;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                int t = i + 1;
                tabs[i].onClick.AddListener(() => { tier = t; Refresh(); });
            }
            nation.onValueChanged.AddListener(i => { nationId = Data.Nations[i].id; Refresh(); });
        }

        public override void Refresh()
        {
            var player = S.ludi[S.playerLudusId];
            if (tier == 0) tier = player.tier;
            nationId ??= S.nation;
            int maxTier = FmRules.MaxTier;
            // 웹판 규칙(2부까지)에서는 다른 나라 1부만 있다
            bool foreignOnlyTop = !FmRules.Enabled && nationId != S.nation;
            int showTier = foreignOnlyTop ? 1 : Mathf.Clamp(tier, 1, maxTier);

            var labels = Data.Nations.Select(n => n.name + (n.id == S.nation ? " (우리 나라)" : "")).ToList();
            if (!nation.options.Select(o => o.text).SequenceEqual(labels)) { nation.ClearOptions(); nation.AddOptions(labels); }
            nation.SetValueWithoutNotify(Data.Nations.FindIndex(n => n.id == nationId));
            var tabs = Tabs;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                tabs[i].gameObject.SetActive(i < maxTier);
                tabs[i].interactable = !foreignOnlyTop || i == 0;
                Seg(tabs[i], showTier == i + 1);
            }

            var ludi = S.ludi.Values.Where(l => l.tier == showTier && l.nation == nationId).OrderByDescending(l => l.rp).ToList();
            int promote = (int)(double)Data.Balance["league"]["promote"], relegate = (int)(double)Data.Balance["league"]["relegate"];
            rows.Sync(ludi.Count, (row, i) =>
            {
                var l = ludi[i];
                var alive = l.gladiators.Select(id => S.gladiators[id]).Where(g => g.alive).ToList();
                double avg = alive.Count > 0 ? alive.Sum(g => Scouting.CaRange(S, g).mid) / (double)alive.Count : 0;
                // 승강 구역: 위 두 자리는 한 부 위로, 아래 두 자리는 한 부 아래로
                bool up = !foreignOnlyTop && showTier > 1 && i < promote;
                bool down = !foreignOnlyTop && showTier < maxTier && i >= ludi.Count - relegate;
                row.Find("Zone").GetComponent<Image>().color = up ? Theme.Good : down ? Theme.Blood : Color.clear;
                row.GetComponent<Image>().color = l.isPlayer ? new Color(0.89f, 0.6f, 0.23f, 0.08f) : Color.clear;
                T(row, "Rank").text = (i + 1).ToString();
                var name = T(row, "Name");
                name.text = Tips.Ludus(l);
                name.fontStyle = l.isPlayer ? FontStyles.Bold : FontStyles.Normal;
                T(row, "Personality").text = l.isPlayer ? "—" : PersonalityNames.TryGetValue(l.aiPersonality ?? "", out var p) ? p : l.aiPersonality;
                T(row, "Count").text = alive.Count.ToString();
                T(row, "Avg").text = Widgets.Stars(avg);
                T(row, "Record").text = $"{l.record.wins}-{l.record.losses}";
                T(row, "Rp").text = l.rp.ToString();
                var last = row.Find("Last")?.GetComponent<TMP_Text>();
                if (last != null) last.text = LastSeason(l);
            });
        }

        /// <summary>지난 시즌 성적 한 칸: "▲ 3부 1위" / "▼ 1부 9위" / "우승 2부" / "2부 5위"</summary>
        static string LastSeason(Ludus l)
        {
            var s = l.seasons?.LastOrDefault();
            if (s == null) return "—";
            string good = Theme.Hex(Theme.Good), bad = Theme.Hex(Theme.Blood), gold = Theme.Hex(Theme.Torch);
            return s.result switch
            {
                "promoted" => $"<color={good}>▲</color> {s.tier}부 {s.rank}위",
                "relegated" => $"<color={bad}>▼</color> {s.tier}부 {s.rank}위",
                "champion" => $"<color={gold}>우승</color> {s.tier}부",
                _ => $"{s.tier}부 {s.rank}위",
            };
        }

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
