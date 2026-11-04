// 비석의 언덕 (Unity판 전용): FM 클럽 연혁·대회 역사처럼 지난 시즌들을 남긴다.
// 탭 넷: 연혁(루두스 하나의 시즌별 성적과 우승컵 진열장), 역대 우승(대회별 우승·준우승), 수상(대회별 개인·라니스타 상), 비석(우리 루두스에서 죽은 검투사).
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class HallScreen : ScreenBase
    {
        static readonly string[] Tabs = { "club", "honours", "awards", "graves" };

        [SerializeField] List<Button> tabButtons = new();
        [SerializeField] List<GameObject> tabRoots = new();

        [Header("연혁")]
        [SerializeField] TMP_Dropdown clubPick;
        [SerializeField] TMP_Text cabinet;
        [SerializeField] RectTransform cabinetIconTemplate;
        TemplateList<RectTransform> cabinetIcons;
        [SerializeField] GameObject clubEmpty;
        [SerializeField] RectTransform clubTemplate;

        [Header("역대 우승")]
        [SerializeField] TMP_Dropdown compPick;
        [SerializeField] GameObject honourEmpty;
        [SerializeField] RectTransform honourTemplate;

        [Header("수상")]
        [SerializeField] TMP_Dropdown awardPick;
        [SerializeField] TMP_Text ceremonyLink;
        [SerializeField] GameObject awardEmpty;
        [SerializeField] RectTransform awardTemplate;

        [Header("비석")]
        [SerializeField] TMP_Text graveTitle;
        [SerializeField] GameObject graveEmpty;
        [SerializeField] RectTransform graveTemplate;

        TemplateList<RectTransform> clubRows, honourRows, graveRows, awardRows;
        string tab = "club", clubId, awardFocus;
        // 대회 고르기: (null, 0) = 판테온 컵, (나라, 부) = 리그
        readonly List<(string nation, int tier)> comps = new();
        int compIdx;
        // 수상 탭 대회: (null, -3) = 시즌 시상(나라별·세계 올해의 검투사), 나머지는 comps와 같다
        readonly List<(string nation, int tier)> awardComps = new();
        int awardIdx;
        List<Ludus> clubList = new();

        void Awake()
        {
            clubRows = new TemplateList<RectTransform>(clubTemplate);
            honourRows = new TemplateList<RectTransform>(honourTemplate);
            graveRows = new TemplateList<RectTransform>(graveTemplate);
            awardRows = new TemplateList<RectTransform>(awardTemplate);
            awardPick.onValueChanged.AddListener(i => { awardIdx = i; Refresh(); });
            for (int i = 0; i < tabButtons.Count; i++)
            {
                string t = Tabs[i];
                tabButtons[i].onClick.AddListener(() => { tab = t; Refresh(); });
            }
            clubPick.onValueChanged.AddListener(i => { clubId = i < clubList.Count ? clubList[i].id : null; Refresh(); });
            compPick.onValueChanged.AddListener(i => { compIdx = i; Refresh(); });
        }

        public override void Refresh()
        {
            if (G.HallFocus != null) { clubId = G.HallFocus; G.HallFocus = null; tab = "club"; }
            if (G.HallAwardFocus != null) { awardFocus = G.HallAwardFocus; G.HallAwardFocus = null; tab = "awards"; }
            for (int i = 0; i < tabButtons.Count; i++)
            {
                Seg(tabButtons[i], tab == Tabs[i]);
                tabRoots[i].SetActive(tab == Tabs[i]);
            }
            switch (tab)
            {
                case "club": DrawClub(); break;
                case "honours": DrawHonours(); break;
                case "awards": DrawAwards(); break;
                case "graves": DrawGraves(); break;
            }
        }

        // ── 연혁 ──

        void DrawClub()
        {
            clubId ??= S.playerLudusId;
            // 우리 루두스 먼저, 그다음 우리 나라, 다른 나라 순
            clubList = S.ludi.Values
                .OrderByDescending(l => l.isPlayer).ThenByDescending(l => l.nation == S.nation).ThenBy(l => l.nation).ThenBy(l => l.tier).ThenByDescending(l => l.rp)
                .ToList();
            Fill(clubPick, clubList.Select(l => l.isPlayer ? $"{l.name} (우리)" : $"{l.name} · {Data.Nation(l.nation).@short} {l.tier}부"), clubList.FindIndex(l => l.id == clubId));
            var club = S.ludi.TryGetValue(clubId, out var c) ? c : S.ludi[S.playerLudusId];

            var trophies = Honours.TrophiesOf(S, club.id);
            string torch = Theme.Hex(Theme.Torch), dim = Theme.Hex(Theme.TextDim);
            if (cabinetIconTemplate != null)
            {
                cabinetIcons ??= new TemplateList<RectTransform>(cabinetIconTemplate);
                cabinetIcons.Sync(trophies.Count, (ic, i) =>
                {
                    var img = ic.Find("Icon").GetComponent<Image>();
                    img.sprite = InfoCards.TrophySprite(trophies[i].name);
                    img.enabled = img.sprite != null;
                    ic.Find("Count").GetComponent<TMP_Text>().text = $"×{trophies[i].seasons.Count}";
                });
            }
            cabinet.text = trophies.Count == 0
                ? $"<color={dim}>아직 든 우승컵이 없습니다.</color>"
                : string.Join("\n", trophies.Select(t => $"<color={torch}>★</color> <b>{InfoCards.CompLink(t.name)}</b> ×{t.seasons.Count}  <color={dim}>(시즌 {string.Join(", ", t.seasons)})</color>"));

            var seasons = (club.seasons ?? new List<LudusSeason>()).OrderByDescending(x => x.season).ToList();
            clubEmpty.SetActive(seasons.Count == 0);
            clubRows.Sync(seasons.Count, (row, i) =>
            {
                var x = seasons[i];
                bool champ = x.rank == 1;
                row.GetComponent<Image>().color = champ ? new Color(0.89f, 0.6f, 0.23f, 0.10f) : Color.clear;
                T(row, "Season").text = $"시즌 {x.season}";
                T(row, "League").text = Tips.League(x.nation ?? club.nation, x.tier);
                T(row, "Rank").text = $"{x.rank}위 <size=80%><color={dim}>/ {x.teams}</color></size>";
                T(row, "Record").text = $"{x.wins}-{x.losses}";
                T(row, "Rp").text = x.rp.ToString();
                T(row, "Result").text = x.result switch
                {
                    "champion" => $"<color={torch}><b>우승</b></color>",
                    "promoted" => $"<color={Theme.Hex(Theme.Good)}>승격</color>" + (champ ? $" · <color={torch}><b>우승</b></color>" : ""),
                    "relegated" => "<color=#d9a29b>강등</color>",
                    _ => "",
                };
                T(row, "Trophies").text = x.trophies == null ? "" : string.Join(", ", x.trophies.Select(t => $"<color={torch}>★</color> {InfoCards.CompLink(t)}"));
            });
        }

        // ── 역대 우승 ──

        void DrawHonours()
        {
            comps.Clear();
            comps.Add((null, 0));
            // tier: 0 판테온 컵, -1 국내 컵, -2 올림피카 컵, 1~ 리그
            comps.Add((null, -2));
            foreach (var n in Data.Nations.OrderByDescending(n => n.id == S.nation))
            {
                comps.Add((n.id, -1));
                for (int t = 1; t <= FmRules.MaxTier; t++) comps.Add((n.id, t));
            }
            compIdx = Mathf.Clamp(compIdx, 0, comps.Count - 1);
            Fill(compPick, comps.Select(x => x.tier == 0 ? Honours.CupName : x.tier == -2 ? OlympicaLogic.Name : x.tier == -1 ? NatCupLogic.Name(x.nation) : Honours.LeagueName(x.nation, x.tier)), compIdx);
            var (nation, tier) = comps[compIdx];
            bool olympica = tier == -2;
            var list = (S.honours ?? new List<HonourEntry>())
                .Where(h => tier == 0 ? h.kind == "cup" : olympica ? h.kind == "olympica" : tier == -1 ? h.kind == "natcup" && h.nation == nation : h.kind == "league" && h.nation == nation && h.tier == tier)
                .OrderByDescending(h => h.season)
                .ToList();
            honourEmpty.SetActive(list.Count == 0);
            string me = S.playerLudusId;
            honourRows.Sync(list.Count, (row, i) =>
            {
                var h = list[i];
                row.GetComponent<Image>().color = h.winnerId == me ? new Color(0.89f, 0.6f, 0.23f, 0.10f) : Color.clear;
                T(row, "Season").text = $"시즌 {h.season}";
                if (olympica)
                {
                    // 올림피카 컵 우승은 나라
                    T(row, "Winner").text = $"<color={Theme.Hex(Theme.Torch)}>★</color> {Tips.Nation(h.winnerId)}";
                    T(row, "RunnerUp").text = h.runnerUpId == null ? "—" : Tips.Nation(h.runnerUpId);
                    return;
                }
                T(row, "Winner").text = $"<color={Theme.Hex(Theme.Torch)}>★</color> " + (h.winnerId == me ? $"<b>{Tips.Ludus(h.winnerId, h.winnerName)}</b>" : Tips.Ludus(h.winnerId, h.winnerName)) + Nation(h.winnerId, nation == null);
                T(row, "RunnerUp").text = h.runnerUpName == null ? "—" : (h.runnerUpId == me ? $"<b>{Tips.Ludus(h.runnerUpId, h.runnerUpName)}</b>" : Tips.Ludus(h.runnerUpId, h.runnerUpName)) + Nation(h.runnerUpId, nation == null);
            });
        }

        string Nation(string ludusId, bool show) =>
            show && ludusId != null && S.ludi.TryGetValue(ludusId, out var l) ? $" <size=80%><color={Theme.Hex(Theme.TextDim)}>({Data.Nation(l.nation).@short})</color></size>" : "";

        // ── 수상 ──

        static readonly string[] KindOrder = { "world_best", "nation_best", "best", "top", "rookie", "lanista" };

        void DrawAwards()
        {
            awardComps.Clear();
            awardComps.Add((null, -3));
            awardComps.Add((null, 0));
            awardComps.Add((null, -2));
            foreach (var n in Data.Nations.OrderByDescending(n => n.id == S.nation))
            {
                for (int t = 1; t <= FmRules.MaxTier; t++) awardComps.Add((n.id, t));
                awardComps.Add((n.id, -1));
            }
            // 수상 카드에서 넘어왔으면 그 대회의 역대 수상자를 연다
            if (awardFocus != null)
            {
                var p = awardFocus.Split(':');
                (string nation, int tier) want = p[0] switch
                {
                    "pantheon" => (null, 0),
                    "olympica" => (null, -2),
                    "natcup" => (p[1], -1),
                    "league" => (p[1], int.Parse(p[2])),
                    _ => (null, -3),
                };
                awardIdx = Mathf.Max(0, awardComps.IndexOf(want));
                awardFocus = null;
            }
            awardIdx = Mathf.Clamp(awardIdx, 0, awardComps.Count - 1);
            Fill(awardPick, awardComps.Select(x => x.tier == -3 ? "시즌 시상 (올해의 검투사)" : x.tier == 0 ? Honours.CupName : x.tier == -2 ? OlympicaLogic.Name
                : x.tier == -1 ? NatCupLogic.Name(x.nation) : Honours.LeagueName(x.nation, x.tier)), awardIdx);
            var (nation, tier) = awardComps[awardIdx];
            bool Match(AwardEntry a) => tier switch
            {
                -3 => a.comp == "world" || a.comp.StartsWith("nation:"),
                0 => a.comp == "pantheon",
                -2 => a.comp == "olympica",
                -1 => a.comp == $"natcup:{nation}",
                _ => a.comp == $"league:{nation}:{tier}",
            };
            var all = S.awards ?? new List<AwardEntry>();
            int last = all.Count == 0 ? 0 : all.Max(a => a.season);
            ceremonyLink.text = last > 0 ? $"<color={Theme.Hex(Theme.TextDim)}>지난 시상식:</color> {Tips.Ceremony(last)}" : "";
            var list = all.Where(Match).OrderByDescending(a => a.season).ThenBy(a => System.Array.IndexOf(KindOrder, a.kind)).ToList();
            awardEmpty.SetActive(list.Count == 0);
            string me = S.playerLudusId;
            awardRows.Sync(list.Count, (row, i) =>
            {
                var a = list[i];
                row.GetComponent<Image>().color = a.ludusId == me ? new Color(0.89f, 0.6f, 0.23f, 0.10f) : Color.clear;
                T(row, "Season").text = $"시즌 {a.season}";
                T(row, "Kind").text = a.kind == "nation_best" ? $"{Tips.Nation(a.comp.Substring(7), true)} {InfoCards.AwardKindLink(a)}" : InfoCards.AwardKindLink(a);
                T(row, "Winner").text = $"<color={Theme.Hex(Theme.Torch)}>★</color> {InfoCards.AwardWho(S, a)}";
                T(row, "Note").text = a.note ?? "";
            });
        }

        // ── 비석 ──

        void DrawGraves()
        {
            var dead = S.fallen.Select(id => S.gladiators.TryGetValue(id, out var g) ? g : null).Where(g => g != null)
                .OrderByDescending(g => g.diedAt?.season ?? 0).ThenByDescending(g => g.diedAt?.day ?? 0).ToList();
            graveTitle.text = $"◆  모래 위에 잠든 우리 검투사 {dead.Count}명";
            graveEmpty.SetActive(dead.Count == 0);
            graveRows.Sync(dead.Count, (row, i) =>
            {
                var g = dead[i];
                T(row, "Name").text = Tips.Name(g) + (string.IsNullOrEmpty(g.nickname) ? "" : $" <size=80%><color={Theme.Hex(Theme.TextDim)}>\"{g.nickname}\"</color></size>");
                T(row, "Race").text = Tips.Race(g.race);
                T(row, "Age").text = $"{g.age}세";
                T(row, "Record").text = $"{g.record.wins}승 {g.record.losses}패 · 살해 {g.record.kills}";
                T(row, "When").text = g.diedAt != null ? $"시즌 {g.diedAt.season} · {g.diedAt.day}일" : "";
                T(row, "Cause").text = g.diedAt?.cause ?? "";
            });
        }

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        static void Fill(TMP_Dropdown d, IEnumerable<string> labels, int value)
        {
            var list = labels.ToList();
            if (!d.options.Select(o => o.text).SequenceEqual(list)) { d.ClearOptions(); d.AddOptions(list); }
            d.SetValueWithoutNotify(Mathf.Max(0, value));
        }

        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
