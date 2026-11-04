// 경력 탭 (웹판 components/CareerPanel.tsx, FM 경력·일대기처럼): 시즌별 기록 표, 수상·우승, 일대기.
// 일대기는 웹판처럼 안쪽 스크롤을 두지 않고 최근 것만 보이다가 '전부 보기'로 펼친다 (바깥 스크롤과 겹치지 않게).
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class CareerPanel : MonoBehaviour
    {
        const int LogPreview = 25;
        static readonly Color Warn = new Color32(0xe0, 0x8b, 0x80, 0xff);
        static readonly Color Death = new Color32(0xff, 0x8a, 0x7a, 0xff);
        // 폰트에 ⚔ ✚ ✝가 없어 비슷한 글자로
        static readonly Dictionary<string, string> KindIcon = new()
        {
            ["join"] = "◇", ["match"] = "×", ["milestone"] = "★", ["injury"] = "+", ["death"] = "†", ["other"] = "·",
        };

        [Tooltip("시즌 줄 원본: Season, Ludus, Apps, Wins, Losses, Kills, Injuries, Ca, Pop, Earnings, Awards")]
        [SerializeField] RectTransform seasonTemplate;
        [Tooltip("통산 줄 (칸 이름은 시즌 줄과 같습니다)")]
        [SerializeField] RectTransform totalRow;
        [SerializeField] TMP_Text log;
        [SerializeField] GameObject logEmpty;
        [SerializeField] Button logMore;
        [Tooltip("받은 상과 소속 루두스가 그 시즌 들어 올린 우승컵")]
        [SerializeField] TMP_Text honours;

        TemplateList<RectTransform> seasonRows;
        string gid;
        bool showAll;

        void Awake()
        {
            seasonRows = new TemplateList<RectTransform>(seasonTemplate);
            logMore.onClick.AddListener(() =>
            {
                showAll = !showAll;
                if (gid != null && GameController.I.State.gladiators.TryGetValue(gid, out var g)) Show(g);
            });
        }

        public void Show(Gladiator g)
        {
            if (gid != g.id) { gid = g.id; showAll = false; }
            string faint = Theme.Hex(Theme.TextFaint);

            var history = g.history.OrderBy(h => h.season).ToList();
            int last = history.Count > 0 ? history[history.Count - 1].season : -1;
            var s = GameController.I.State;
            var awards = (s.awards ?? new List<AwardEntry>()).Where(a => a.gladiatorId == g.id).ToList();
            // FM식: 능력 숫자는 숨긴다. 별점(우리 검투사는 독토레 평가, 남은 정찰 범위)과 오르내림 화살표만
            var now = Scouting.CaRange(s, g);
            double blur = (now.hi - now.lo) / 2;
            seasonRows.Sync(history.Count, (row, i) =>
            {
                var h = history[i];
                double caEnd = h.season == last && g.alive ? GladiatorRules.ComputeCA(g) : h.caEnd;
                double delta = caEnd - h.caStart;
                var dc = delta > 0 ? Theme.Good : delta < 0 ? Warn : Theme.TextFaint;
                string ds = delta > 0 ? $"+{N(delta)}" : delta < 0 ? N(delta) : "±0";
                if (FmRules.Enabled)
                {
                    string stars = h.season == last && g.alive ? Widgets.CaStars(s, g) : Widgets.RangeStars(caEnd - blur, caEnd + blur);
                    // 시즌 동안 별 반 개(CA 10) 넘게 움직였을 때만 화살표
                    string arrow = delta >= 10 ? $" <color={Theme.Hex(Theme.Good)}>▲</color>" : delta <= -10 ? $" <color={Theme.Hex(Warn)}>▼</color>" : "";
                    T(row, "Ca").text = $"<size=85%><color={Theme.Hex(Theme.Torch)}>{stars}</color>{arrow}</size>";
                }
                T(row, "Season").text = h.season.ToString();
                T(row, "Ludus").text = $"{h.ludus} <color={faint}><size=80%>{(h.tier != 0 ? $"{N(h.tier)}부" : "")}</size></color>";
                T(row, "Apps").text = N(h.apps);
                T(row, "Wins").text = N(h.wins);
                T(row, "Losses").text = N(h.losses);
                T(row, "Kills").text = N(h.kills);
                T(row, "Rating").text = RatingText(Ratings.Average(h.ratingSum, h.rated));
                T(row, "Injuries").text = N(h.injuries);
                if (!FmRules.Enabled) T(row, "Ca").text = $"{N(caEnd)} <size=75%><color={Theme.Hex(dc)}>{ds}</color></size>";
                T(row, "Pop").text = N(h.popularity);
                T(row, "Earnings").text = h.earnings.ToString("N0");
                int won = awards.Count(a => a.season == h.season);
                T(row, "Awards").text = won > 0 ? $"<link=\"awards:g:{g.id}\"><color={Theme.Hex(Theme.Torch)}>★{won}</color></link>" : "";
            });
            T(totalRow, "Apps").text = N(history.Sum(h => h.apps));
            T(totalRow, "Wins").text = N(history.Sum(h => h.wins));
            T(totalRow, "Losses").text = N(history.Sum(h => h.losses));
            T(totalRow, "Kills").text = N(history.Sum(h => h.kills));
            T(totalRow, "Rating").text = RatingText(Ratings.Average(history.Sum(h => h.ratingSum ?? 0), history.Sum(h => h.rated ?? 0)));
            T(totalRow, "Earnings").text = history.Sum(h => h.earnings).ToString("N0");
            T(totalRow, "Awards").text = awards.Count > 0 ? $"<link=\"awards:g:{g.id}\">★{awards.Count}</link>" : "";

            // 수상 · 우승: 받은 상, 그다음 뛰던 루두스가 그 시즌 들어 올린 우승컵 (FM 경력의 Honours처럼)
            var lines = awards.OrderByDescending(a => a.season)
                .Select(a => $"<color={Theme.Hex(Theme.Torch)}>★</color> <color={faint}><size=80%>시즌 {a.season}</size></color>  {InfoCards.CompLinkOf(a.comp)} <b>{Awards.KindNames[a.kind]}</b>").ToList();
            foreach (var h in Enumerable.Reverse(history))
            {
                var club = s.ludi.Values.FirstOrDefault(l => l.name == h.ludus);
                var cups = club?.seasons?.FirstOrDefault(x => x.season == h.season)?.trophies;
                if (cups == null) continue;
                foreach (var t in cups) lines.Add($"<color={Theme.Hex(Theme.BronzeHi)}>◆</color> <color={faint}><size=80%>시즌 {h.season}</size></color>  {InfoCards.CompLink(t)} 우승 <color={faint}>({Tips.Ludus(club)})</color>");
            }
            honours.text = lines.Count == 0 ? $"<color={faint}>아직 받은 상이나 우승컵이 없습니다.</color>" : string.Join("\n", lines);

            // 일대기: 최근 것부터
            var events = Enumerable.Reverse(g.career).ToList();
            logEmpty.SetActive(events.Count == 0);
            log.gameObject.SetActive(events.Count > 0);
            var shown = showAll ? events : events.Take(LogPreview).ToList();
            log.text = string.Join("\n", shown.Select(e =>
            {
                var ic = e.kind == "milestone" ? Theme.Torch : e.kind == "injury" ? Warn : e.kind == "death" ? Death : Theme.BronzeHi;
                string icon = KindIcon.TryGetValue(e.kind ?? "", out var k) ? k : "·";
                // 일대기 속 이름도 링크로 (FM처럼 눌러서 따라간다)
                string linked = Tips.AutoLink(s, e.text);
                string text = e.kind == "death" ? $"<color={Theme.Hex(Death)}>{linked}</color>" : linked;
                return $"<color={Theme.Hex(ic)}>{icon}</color>  <color={faint}><size=80%>시즌 {e.season} · {Calendar.DateLabel(e.day)}</size></color>   {text}";
            }));
            logMore.gameObject.SetActive(events.Count > LogPreview);
            logMore.GetComponentInChildren<TMP_Text>().text = showAll ? "접기" : $"전부 보기 ({events.Count})";
        }

        /// <summary>평균 평점 (FM처럼 높으면 녹색, 낮으면 붉은빛). 기록이 없으면 —</summary>
        public static string RatingText(double? r) => r is double v
            ? $"<color={Theme.Hex(v >= 7.5 ? Theme.Good : v >= 6.5 ? Theme.Parchment : Warn)}>{v:0.00}</color>"
            : $"<color={Theme.Hex(Theme.TextFaint)}>—</color>";

        static string N(double v) => Math.Round(v).ToString();
        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();
    }
}
