// 전략 화면 (Unity판): 탭 셋 — 개인 전술(검투사별 교범) / 3:3 / 5:5 단체 전술(팀 방침 + 사람마다 역할).
// 단체 전술은 Ludus.teamTactics["3"/"5"]에 기본값으로 저장하고, 난전 때 Tactics.TeamFor가 출전자에 맞춰 쓴다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class StrategyScreen : ScreenBase
    {
        [Header("탭 (개인 / 3:3 / 5:5)")]
        [SerializeField] List<Button> tabButtons = new();
        [SerializeField] GameObject personalRoot;
        [SerializeField] GameObject teamRoot;

        [Header("개인 전술")]
        [Tooltip("왼쪽 검투사 목록 줄 원본 (자식 Text)")]
        [SerializeField] Button gladTemplate;
        [SerializeField] DoctrinePanel doctrine;

        [Header("단체 전술")]
        [SerializeField] TMP_Text teamTitle;
        [Tooltip("팀 방침 버튼들 (이름 = focus/spread/weak/strong)")]
        [SerializeField] RectTransform policyGrid;
        [SerializeField] TMP_Text policyDesc;
        [Tooltip("출전자 줄 원본: Name, Roles(버튼 이름 = 역할), Guard(버튼), Desc")]
        [SerializeField] RectTransform memberTemplate;
        [SerializeField] TMP_Text teamEmpty;
        [SerializeField] Button resetTeam;

        TemplateList<Button> glads;
        TemplateList<RectTransform> members;
        int tab;
        string selected;

        string Format => tab == 1 ? "3" : "5";
        Ludus Player => S.ludi[S.playerLudusId];

        void Awake()
        {
            glads = new TemplateList<Button>(gladTemplate);
            members = new TemplateList<RectTransform>(memberTemplate);
            for (int i = 0; i < tabButtons.Count; i++)
            {
                int k = i;
                tabButtons[i].onClick.AddListener(() => { tab = k; Refresh(); });
            }
            foreach (Transform c in policyGrid)
            {
                string k = c.name;
                c.GetComponent<Button>().onClick.AddListener(() => G.Mutate(s => Tactics.SavedTeam(s.ludi[s.playerLudusId], Format).policy = k));
            }
            resetTeam.onClick.AddListener(() => G.Mutate(s => s.ludi[s.playerLudusId].teamTactics?.Remove(Format)));
        }

        public override void Refresh()
        {
            if (S == null) return;
            for (int i = 0; i < tabButtons.Count; i++) Seg(tabButtons[i], i == tab);
            personalRoot.SetActive(tab == 0);
            teamRoot.SetActive(tab != 0);
            if (tab == 0) DrawPersonal();
            else DrawTeam();
        }

        List<Gladiator> Mine() => Player.gladiators.Select(id => S.gladiators[id]).Where(g => g.alive).ToList();

        void DrawPersonal()
        {
            var list = Mine();
            if (selected == null || list.All(g => g.id != selected)) selected = list.FirstOrDefault()?.id;
            glads.Sync(list.Count, (b, i) =>
            {
                var g = list[i];
                var d = g.doctrine;
                string faint = Theme.Hex(Theme.TextFaint);
                b.GetComponentInChildren<TMP_Text>().text = $"{g.name}\n<size=80%><color={faint}>{Tips.Style(g.style)} · {(d != null && Tactics.StanceNames.TryGetValue(d.stance ?? "", out var sn) ? sn : "")}</color></size>";
                Seg(b, g.id == selected);
                b.onClick.RemoveAllListeners();
                string id = g.id;
                b.onClick.AddListener(() => { selected = id; Refresh(); });
            });
            doctrine.gameObject.SetActive(selected != null);
            if (selected != null) doctrine.Show(S.gladiators[selected], true);
        }

        void DrawTeam()
        {
            int n = tab == 1 ? 3 : 5;
            var lineup = SeriesLogic.AutoLineup(S, Player, n, ChallengeLogic.Now(S));
            bool saved = Player.teamTactics != null && Player.teamTactics.ContainsKey(Format);
            var t = Tactics.TeamFor(Player, lineup);
            string faint = Theme.Hex(Theme.TextFaint);
            teamTitle.text = $"◆  {n}:{n} 단체 전술" + (saved ? "" : $"  <size=80%><color={faint}>(아직 자동 — 고르면 저장됩니다)</color></size>");
            foreach (Transform c in policyGrid) Seg(c.GetComponent<Button>(), c.name == t.policy);
            policyDesc.text = Tactics.PolicyDesc.TryGetValue(t.policy, out var pd) ? pd : "";
            resetTeam.gameObject.SetActive(saved);

            teamEmpty.gameObject.SetActive(lineup.Count == 0);
            teamEmpty.text = "출전할 수 있는 검투사가 없습니다.";
            members.Sync(lineup.Count, (row, i) =>
            {
                var g = lineup[i];
                string role = t.roles[g.id];
                bool chosen = saved && Player.teamTactics[Format].roles.ContainsKey(g.id);
                row.Find("Name").GetComponent<TMP_Text>().text = $"{Tips.Name(g)}\n<size=80%><color={faint}>{Tips.Style(g.style)} · CA {GladiatorRules.ComputeCA(g)}</color></size>";
                foreach (Transform c in row.Find("Roles"))
                {
                    var b = c.GetComponent<Button>();
                    Seg(b, c.name == role);
                    // 올리면 역할 설명 (버튼 이름 = 역할 id)
                    b.GetComponentInChildren<TMP_Text>().text = Tips.Role(c.name);
                    b.onClick.RemoveAllListeners();
                    string r = c.name, id = g.id;
                    b.onClick.AddListener(() => G.Mutate(s => Tactics.SavedTeam(s.ludi[s.playerLudusId], Format).roles[id] = r));
                }
                // 호위: 지킬 동료를 누를 때마다 다음 동료로 (자동 → 동료1 → 동료2 …)
                var guard = row.Find("Guard").GetComponent<Button>();
                guard.gameObject.SetActive(role == "guard");
                if (role == "guard")
                {
                    var allies = lineup.Where(x => x != g).Select(x => x.id).ToList();
                    string cur = t.guardOf.TryGetValue(g.id, out var w) ? w : null;
                    guard.GetComponentInChildren<TMP_Text>().text = "지킬 동료: " + (cur != null && S.gladiators.TryGetValue(cur, out var wg) ? wg.name : "자동 (가장 약한 동료)");
                    guard.onClick.RemoveAllListeners();
                    string id = g.id;
                    guard.onClick.AddListener(() => G.Mutate(s =>
                    {
                        var st = Tactics.SavedTeam(s.ludi[s.playerLudusId], Format);
                        int at = cur == null ? -1 : allies.IndexOf(cur);
                        if (at + 1 >= allies.Count) st.guardOf.Remove(id);
                        else st.guardOf[id] = allies[at + 1];
                    }));
                }
                row.Find("Desc").GetComponent<TMP_Text>().text = (chosen ? "" : $"<color={faint}>자동</color> ") + Tactics.RoleDesc[role];
            });
        }

        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
