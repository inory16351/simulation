// 영입 패널 (웹판 components/AcquirePanel.tsx): 남의 검투사 상세 화면에서 정찰, 관심 명단, 무소속 영입, 이적료 제안.
// Unity판은 주급·이적료 모두 흥정(Haggling)이다: 여러 번 제안할 수 있지만 상대 인내심이 바닥나면 한동안 상대하지 않는다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class AcquirePanel : MonoBehaviour
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text info;
        [SerializeField] RectTransform infoBar;
        [SerializeField] Button scout;
        [SerializeField] Button shortlist;
        [SerializeField] TMP_Text msg;
        [Tooltip("흥정 상태 줄 (인내심·역제안)")]
        [SerializeField] TMP_Text mood;
        [Header("무소속")]
        [SerializeField] GameObject freeRoot;
        [SerializeField] TMP_Text freeHint;
        [SerializeField] TMP_Text want;
        [SerializeField] TMP_InputField wage;
        [Tooltip("1~4시즌 순서")]
        [SerializeField] List<Button> seasons = new();
        [SerializeField] Button sign;
        [Header("다른 루두스 소속")]
        [SerializeField] GameObject clubRoot;
        [SerializeField] TMP_Text value;
        [SerializeField] TMP_InputField fee;
        [SerializeField] Button bid;
        [SerializeField] GameObject dealRoot;
        [SerializeField] TMP_Text dealText;
        [SerializeField] Button dealGo;

        static GameController G => GameController.I;
        static GameState S => GameController.I.State;

        string gid, msgText;
        int years = 2;
        bool good;

        void Awake()
        {
            for (int i = 0; i < seasons.Count; i++) { int y = i + 1; seasons[i].onClick.AddListener(() => { years = y; Draw(); }); }
            scout.onClick.AddListener(() => { string id = gid; Say(G.Try(d => Scouting.AssignScout(d, id))); });
            shortlist.onClick.AddListener(() =>
            {
                string id = gid;
                G.Mutate(d =>
                {
                    d.shortlist ??= new List<string>();
                    if (d.shortlist.Contains(id)) d.shortlist = d.shortlist.Where(x => x != id).ToList();
                    else d.shortlist.Add(id);
                });
            });
            sign.onClick.AddListener(Sign);
            bid.onClick.AddListener(Bid);
            dealGo.onClick.AddListener(() => G.Go("market"));
        }

        /// <summary>검투사가 바뀌면 입력칸을 기본값으로 채운다. 우리 검투사면 숨긴다</summary>
        public void Show(Gladiator g)
        {
            var owner = Career.LudusOf(S, g.id);
            bool free = S.freeAgents.Contains(g.id);
            bool canAcquire = g.alive && (free || (owner != null && !owner.isPlayer));
            gameObject.SetActive(canAcquire);
            if (!canAcquire) return;
            if (gid != g.id)
            {
                gid = g.id;
                msgText = null;
                years = 2;
                wage.SetTextWithoutNotify(Economy.FreeAgentDemand(S, g).wage.ToString());
                fee.SetTextWithoutNotify((JsMath.Round(Economy.MarketValue(g) / 50) * 50).ToString());
            }
            Draw();
        }

        void Say(string text, bool ok = false) { msgText = text; good = ok; Draw(); }

        void Sign()
        {
            string id = gid;
            int y = years;
            double w = double.TryParse(wage.text, out var v) ? v : 0;
            HaggleResult res = null;
            G.Act((d, rng) => res = Economy.HaggleFreeAgent(d, rng, id, w, y));
            if (res.status == "countered") wage.SetTextWithoutNotify(res.counter.ToString());
            Say(res.Accepted ? "계약했다!" : res.line, res.Accepted);
        }

        void Bid()
        {
            string id = gid;
            double f = double.TryParse(fee.text, out var v) ? v : 0;
            HaggleResult res = null;
            G.Act((d, rng) => res = Transfers.HaggleFee(d, rng, id, f));
            if (res.status == "countered") fee.SetTextWithoutNotify(res.counter.ToString());
            Say(res.Accepted ? "이적료에 합의했다. 이제 개인 계약을 맺어라." : res.line, res.Accepted);
        }

        void Draw()
        {
            if (gid == null || !S.gladiators.TryGetValue(gid, out var g)) return;
            var owner = Career.LudusOf(S, g.id);
            bool free = S.freeAgents.Contains(g.id);
            title.text = "◆  " + (free ? "무소속 — 영입 협상" : $"{owner?.name} 소속 — 이적");

            double k = Scouting.KnowledgeOf(S, g);
            bool scouting = Scouting.ScoutingOf(S).assignments.Contains(g.id);
            info.text = $"정보량 {JsMath.Round(k)}";
            Widgets.SetBar(infoBar, k, 100, Theme.BronzeHi);
            scout.interactable = !scouting && k < 100;
            scout.GetComponentInChildren<TMP_Text>().text = scouting ? "정찰 중" : k >= 100 ? "정찰 끝" : "정찰을 맡긴다";
            bool shortOn = S.shortlist?.Contains(g.id) == true;
            shortlist.GetComponentInChildren<TMP_Text>().text = shortOn ? "★ 관심 명단에서 빼기" : "☆ 관심 명단";

            msg.gameObject.SetActive(msgText != null);
            msg.text = msgText ?? "";
            msg.color = good ? Theme.Good : new Color32(0xd9, 0xa2, 0x9b, 0xff);

            string key = free ? Economy.WageKey(g.id) : Transfers.FeeKey(g.id);
            string line = Widgets.HaggleLine(S, key);
            mood.gameObject.SetActive(line != null);
            mood.text = line ?? "";
            bool locked = Haggling.IsLocked(S, key);

            freeRoot.SetActive(free);
            if (free)
            {
                var d = Economy.FreeAgentDemand(S, g);
                freeHint.text = d.refuse
                    ? $"<color=#d9a29b>관심 없음 · {d.reason}</color> <color={Theme.Hex(Theme.TextFaint)}>(루두스 명성을 올리면 마음이 바뀔 수 있다)</color>"
                    : $"계약금은 제안 주급의 4주치({Economy.SigningFee(d.wage):N0} D 안팎)다. {d.reason}";
                want.text = $"원함 {d.wage} D";
                for (int i = 0; i < seasons.Count; i++)
                {
                    bool on = years == i + 1;
                    seasons[i].transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
                    seasons[i].GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
                }
                sign.interactable = !d.refuse && !locked;
            }

            bool club = owner != null && !owner.isPlayer;
            var deal = Transfers.TransferOf(S).deals.FirstOrDefault(x => x.gladiatorId == g.id);
            clubRoot.SetActive(club && deal == null);
            dealRoot.SetActive(club && deal != null);
            if (club && deal == null)
            {
                value.text = $"추정 가치 {JsMath.Round(Economy.MarketValue(g) / 50) * 50:N0} D";
                bool open = Transfers.InWindow(S);
                bid.interactable = open && !locked;
                if (!open && msgText == null) { msg.gameObject.SetActive(true); msg.text = $"이적 시장 기간: {Transfers.WindowLabel}"; msg.color = Theme.TextDim; }
            }
            if (deal != null) dealText.text = $"이적료 {deal.fee:N0} D에 합의했다.";
        }
    }
}
