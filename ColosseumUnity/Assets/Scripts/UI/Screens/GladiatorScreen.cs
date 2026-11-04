// 검투사 상세 (웹판 GladiatorScreen.tsx): 신상·컨디션, 능력치 네 묶음, 무기 숙련, 기술.
// 패널은 따로 둔다: 영입(AcquirePanel), 전투 교범(DoctrinePanel), 장비(EquipPanel), 경력(CareerPanel).
// 신상 아래는 탭 셋(정보 / 장비 / 전술)으로 나눠 한 번에 하나만 보인다.
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class GladiatorScreen : ScreenBase
    {
        static readonly Dictionary<string, string> StatusNames = new() { ["slave"] = "노예", ["free"] = "자유민", ["freedman"] = "해방민" };
        static readonly Color Warn = new Color32(0xe0, 0x8b, 0x80, 0xff);
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        [SerializeField] Button backButton;
        [SerializeField] PortraitView portrait;
        [SerializeField] TMP_Text bigName;
        [SerializeField] TMP_Text subLine;
        [Tooltip("키-값 줄들의 부모. 줄 이름 = 항목 id, 값은 Value(TMP), 막대는 Bar")]
        [SerializeField] RectTransform kv;
        [SerializeField] TMP_Text traits;
        [Tooltip("능력치 값 칸들의 부모들. 값 칸 이름 = 능력치 id")]
        [SerializeField] List<RectTransform> attrBlocks = new();
        [SerializeField] RectTransform profBlock;
        [Tooltip("빈 칸 안내 한 줄")]
        [SerializeField] TMP_Text skills;
        [Tooltip("기술 칸 원본: Icon(Image), Name(TMP, skill: 링크)")]
        [SerializeField] RectTransform skillTemplate;
        TemplateList<RectTransform> skillRows;
        [Tooltip("남의 검투사일 때만 보입니다")]
        [SerializeField] AcquirePanel acquire;
        [Tooltip("우리 검투사(살아 있을 때)만 고칠 수 있습니다")]
        [SerializeField] DoctrinePanel doctrine;
        [SerializeField] EquipPanel equip;
        [SerializeField] CareerPanel career;
        [Header("탭 (정보 / 장비 / 전술)")]
        [SerializeField] List<Button> tabButtons = new();
        [SerializeField] List<GameObject> tabRoots = new();

        int tab;

        void Awake()
        {
            backButton.onClick.AddListener(() => G.Back());
            skillRows = new TemplateList<RectTransform>(skillTemplate);
            for (int i = 0; i < tabButtons.Count; i++)
            {
                int k = i;
                tabButtons[i].onClick.AddListener(() => { tab = k; ShowTab(); });
            }
        }

        void ShowTab()
        {
            for (int i = 0; i < tabRoots.Count; i++)
            {
                tabRoots[i].SetActive(i == tab);
                var b = tabButtons[i];
                b.transform.Find("Fill").GetComponent<Image>().color = i == tab ? Theme.ContinueBg : Theme.Panel2;
                b.GetComponent<Image>().color = i == tab ? Theme.Torch : Theme.Line;
            }
        }

        /// <summary>다른 화면에서 특정 탭으로 열 때 (0 정보, 1 장비, 2 전술)</summary>
        public void SetTab(int i) { tab = i; ShowTab(); }

        public override void Refresh()
        {
            if (G.SelectedGladiator == null || !S.gladiators.TryGetValue(G.SelectedGladiator, out var g)) return;
            var owner = S.ludi.Values.FirstOrDefault(l => l.gladiators.Contains(g.id));
            bool isMine = owner?.isPlayer ?? S.fallen.Contains(g.id);
            int ca = GladiatorRules.ComputeCA(g);
            var style = Data.Style(g.style);
            string dim = Theme.Hex(Theme.TextDim), faint = Theme.Hex(Theme.TextFaint);

            ShowTab();
            portrait.Show(g);
            acquire.Show(g);
            doctrine.Show(g, isMine && g.alive);
            equip.Show(g, isMine && g.alive);
            career.Show(g);
            bigName.text = g.name + (string.IsNullOrEmpty(g.nickname) ? "" : $" <size=60%><color={dim}>\"{g.nickname}\"</color></size>");
            backButton.GetComponentInChildren<TMP_Text>().text = "← " + (Shell.Menu.FirstOrDefault(m => m.id == G.ReturnScreen).label ?? "막사");
            subLine.text = $"{Tips.Race(g.race)} · {Tips.Nation(g.nation, true)} 출신 · {g.age}세 ({GladiatorRules.AgeStageNames[GladiatorRules.AgeStage(g)]}) · {Tips.Ludus(owner)}";

            Kv("Style", $"{Tips.Style(g.style)} — <color={dim}>{style?.desc}</color>");
            // FM식: 신분 대신 계약 (노예·자유민 구분 없음)
            Kv("Status", FmRules.Enabled
                ? (g.contract != null ? $"시즌 {g.contract.untilSeason}까지" + (g.contract.untilSeason <= S.season ? $" <color={Theme.Hex(Neg)}>(이번 시즌 만료)</color>" : "") : S.freeAgents.Contains(g.id) ? "무소속" : "—")
                : StatusNames.TryGetValue(g.status, out var st) ? st : g.status);
            Kv("Wage", $"{g.wage} D" + (g.unpaidWeeks > 0 ? $"  <color={Theme.Hex(Neg)}>급료 {g.unpaidWeeks}회 밀림</color>" : ""));
            if (isMine) Kv("Ca", $"<color={Theme.Hex(Theme.Torch)}>{Widgets.CaStars(S, g)}</color> <color={faint}>(독토레 평가)</color>");
            else { var r = Scouting.CaRange(S, g); Kv("Ca", RangeStars(r.lo, r.hi)); }
            var pr = Scouting.PaRange(S, g);
            Kv("Pa", RangeStars(pr.lo, pr.hi));
            KvBar("Morale", g.morale, $"{g.morale}");
            KvBar("Popularity", g.popularity, $"{g.popularity}", Theme.Torch);
            KvBar("Fitness", 100 - g.fatigue, $"{Math.Round(100 - g.fatigue)}");
            KvBar("Sharpness", g.sharpness, $"{Math.Round(g.sharpness)}", Theme.BronzeHi);
            KvBar("Wear", g.wear, $"{Math.Round(g.wear)}" + (g.wear >= 85 ? $"  <color={Theme.Hex(Neg)}>탈진</color>" : ""),
                g.wear >= 85 ? Theme.Blood : g.wear >= 50 ? Theme.Torch : Theme.Good);

            double perf = GladiatorRules.Performance(g);
            var pc = perf >= 1 ? Theme.Good : perf >= 0.85 ? Theme.Parchment : Warn;
            Kv("Perf", $"<b><color={Theme.Hex(pc)}>{Math.Round(perf * 100)}%</color></b>" +
                string.Concat(GladiatorRules.PerformanceParts(g).Select(p => $"  <color={dim}>{p.label} {(p.value > 0 ? "+" : "")}{Math.Round(p.value * 100)}%</color>")));
            Kv("State", !g.alive ? $"<color={Theme.Hex(Neg)}>사망 — 시즌 {g.diedAt?.season} {Calendar.DateLabel(g.diedAt?.day ?? 1)}, {g.diedAt?.cause}</color>"
                : (g.injury != null ? $"<color=#f0b3aa>{GladiatorTable.InjuryLabel(g)}</color>" : "출전 가능") + (Grievances.Line(S, g) is string grief ? $"  <color=#f0b3aa>{grief}</color>" : ""));
            Kv("Record", $"{g.record.wins}승 {g.record.losses}패 · 살해 {g.record.kills}" + (Tips.AwardsOf(S, g) is var aw && aw.Length > 0 ? $" · {aw}" : ""));

            var race = Data.Race(g.race);
            var tags = new List<string> { Tips.Racial(g.race, Theme.Hex(Theme.Parchment)) };
            foreach (var t in g.traits)
            {
                var def = Data.Trait(t);
                if (def == null) continue;
                if (def.hidden && !isMine) tags.Add($"<color={faint}>?</color>");
                else tags.Add(Tips.Trait(def.id, Theme.Hex(def.negative ? Neg : def.hidden ? Theme.TextFaint : Theme.Text)));
            }
            traits.text = string.Join("   ", tags);

            // 능력치: 남의 검투사는 정찰한 만큼만 보인다
            foreach (var block in attrBlocks)
            {
                int i = 0;
                foreach (Transform row in block)
                {
                    var val = row.Find("Value");
                    if (val == null) continue;
                    int v = GladiatorRules.EffectiveAttr(g, row.name);
                    var r = Scouting.AttrRange(S, g, v, i + 3);
                    var tx = val.GetComponent<TMP_Text>();
                    // 정찰이 끝나 값이 보일 때만 오르내림도 보인다
                    tx.textWrappingMode = TextWrappingModes.NoWrap;
                    tx.text = r.lo == r.hi ? Widgets.TrendArrow(S, g, row.name) + v : $"{r.lo}~{r.hi}";
                    tx.color = AttrColor(r.mid);
                    tx.fontStyle = r.mid > 15 ? FontStyles.Bold : FontStyles.Normal;
                    i++;
                }
            }
            foreach (Transform row in profBlock)
            {
                var val = row.Find("Value");
                if (val == null) continue;
                int p = g.prof.TryGetValue(row.name, out var pv) ? pv : 0;
                Widgets.SetBar((RectTransform)row.Find("Bar"), p, 20, Theme.BronzeHi);
                var tx = val.GetComponent<TMP_Text>();
                // 칸이 좁아 화살표가 붙어도 줄이 바뀌지 않게
                tx.textWrappingMode = TextWrappingModes.NoWrap;
                tx.text = Widgets.TrendArrow(S, g, "prof:" + row.name) + p;
                tx.color = AttrColor(p);
            }

            // 기술 칸: 능동 먼저, 그다음 반응·상시. 아이콘은 기술 테이블의 icon
            var all = g.skills.active.Concat(g.skills.passive).ToList();
            skillRows.Sync(all.Count, (row, i) =>
            {
                string id = all[i];
                var icon = row.Find("Icon").GetComponent<Image>();
                icon.sprite = Tips.SkillIcon(id);
                icon.enabled = icon.sprite != null;
                var r = SkillBook.Row(id);
                string kind = r == null ? "" : r.type == "Active" ? "능동" : r.type == "Reaction" ? "반응" : "상시";
                row.Find("Name").GetComponent<TMP_Text>().text = $"{Tips.Skill(id)}\n<size=80%><color={faint}>{kind}{(r != null ? " · " + new string('★', r.tier) : "")}</color></size>";
            });
            int freeActive = Math.Max(0, (int)SkillBook.C("ActiveSlots", 4) - g.skills.active.Count);
            skills.text = $"빈 칸: 능동 {freeActive}" + (isMine && SkillBook.Loaded ? $"   ·   습득 조건을 채운 기술 {SkillBook.Ready(g).Count}개 (달마다 습득 기회)" : "");
        }

        void Kv(string id, string value) => kv.Find(id + "/Value").GetComponent<TMP_Text>().text = value;

        void KvBar(string id, double v, string label, Color? color = null)
        {
            Widgets.SetBar((RectTransform)kv.Find(id + "/Bar"), v, 100, color);
            Kv(id, label);
        }

        /// <summary>웹판 attrClass: a1~a4</summary>
        static Color AttrColor(double v) => v <= 5 ? new Color32(0x6b, 0x62, 0x55, 0xff) : v <= 10 ? Theme.Parchment : v <= 15 ? Theme.BronzeHi : Theme.Torch;

        /// <summary>웹판 RangeStars: 정찰 범위 별점</summary>
        static string RangeStars(double lo, double hi)
        {
            string a = Widgets.Stars(lo), b = Widgets.Stars(hi);
            return $"<color={Theme.Hex(Theme.Torch)}>{a}</color>" + (b != a ? $"<color={Theme.Hex(Theme.TextFaint)}>~{b}</color>" : "");
        }
    }
}
