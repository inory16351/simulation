// 식솔 (웹판 StaffScreen.tsx): 자리 넷(독토레·메디쿠스·정찰꾼·중개인)과 식솔 찾기. 후보 줄은 하이어라키의 비활성 원본을 복제한다.
// Unity판에만 있는 것: 고용은 주급 흥정(StaffLogic.HaggleStaff). 역제안이 오면 입력칸을 그 값으로 채운다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class StaffScreen : ScreenBase
    {
        static readonly int[] RatingSteps = { 0, 5, 10, 15 };
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        [SerializeField] TMP_Text sub;
        [Tooltip("자리 패널 내용 칸 (StaffLogic.Roles 순서)")]
        [SerializeField] List<RectTransform> rolePads = new();

        [Header("식솔 찾기")]
        [SerializeField] TMP_Dropdown roleFilter;
        [SerializeField] TMP_Dropdown ratingFilter;
        [SerializeField] Button willingToggle;
        [SerializeField] TMP_Text repHint;
        [SerializeField] TMP_Text msgText;
        [SerializeField] GameObject empty;
        [SerializeField] RectTransform rowTemplate;

        TemplateList<RectTransform> rows;
        // 화면 상태 (웹판 useState)
        string msg, confirmFire;
        bool msgGood, onlyWilling;
        readonly Dictionary<string, double> offers = new();

        void Awake()
        {
            rows = new TemplateList<RectTransform>(rowTemplate);
            GrowRolePads();
            Fill(roleFilter, new[] { "전부" }.Concat(StaffLogic.Roles.Select(r => StaffLogic.RoleNames[r])), 0);
            Fill(ratingFilter, RatingSteps.Select(v => v == 0 ? "제한 없음" : v.ToString()), 0);
            roleFilter.onValueChanged.AddListener(_ => Refresh());
            ratingFilter.onValueChanged.AddListener(_ => Refresh());
            willingToggle.onClick.AddListener(() => { onlyWilling = !onlyWilling; Refresh(); });
        }

        Ludus Player => S.ludi[S.playerLudusId];

        public override void Refresh()
        {
            var me = Player;
            sub.text = (FmRules.Enabled ? "역할마다 여러 명을 둘 수 있습니다 (부·시설에 따라 늘어남). 같은 역할은 가장 잘하는 사람 효과에 둘째는 절반, 셋째는 4분의 1이 더해집니다. " : "자리마다 한 명씩 둡니다. ") + $"식솔 급료도 급료일에 함께 나가며, 지금은 매주 {StateUtil.Num(StaffLogic.StaffWages(S, me))} D가 나갑니다. 후보는 이적 시장이 열릴 때마다 바뀝니다.";
            for (int i = 0; i < rolePads.Count && i < StaffLogic.Roles.Length; i++)
                BindRolePeople(rolePads[i], StaffLogic.Roles[i], StaffLogic.StaffsOf(S, me, StaffLogic.Roles[i]));
            DrawMarket();
        }

        // ── 자리 ──

        /// <summary>FM 세계: 역할이 여덟이라 씬의 자리 패널(넷, 두 줄)을 줄째 복제해 늘린다 (씬 다시 조립 없이)</summary>
        void GrowRolePads()
        {
            if (rolePads.Count == 0) return;
            var line = rolePads[rolePads.Count - 1].parent.parent;
            while (rolePads.Count < StaffLogic.Roles.Length)
            {
                var copy = Instantiate(line.gameObject, line.parent);
                copy.name = "Line" + rolePads.Count / 2;
                foreach (Transform frame in copy.transform)
                {
                    var pad = (RectTransform)frame.Find("Pad");
                    if (pad != null) rolePads.Add(pad);
                }
            }
            // 제목·설명을 역할 순서대로
            for (int i = 0; i < rolePads.Count; i++)
            {
                bool on = i < StaffLogic.Roles.Length;
                rolePads[i].parent.gameObject.SetActive(on);
                if (!on) continue;
                string role = StaffLogic.Roles[i];
                rolePads[i].parent.name = "Role_" + role;
                var desc = rolePads[i].Find("Desc")?.GetComponent<TMP_Text>();
                if (desc != null) desc.text = StaffLogic.RoleDesc[role];
            }
        }

        /// <summary>역할 한 칸: 그 역할 식솔 전원 (카드를 사람 수만큼 복제), 제목에 인원/상한</summary>
        void BindRolePeople(RectTransform pad, string role, List<Staff> people)
        {
            var title = pad.parent.Find("Head/Text")?.GetComponent<TMP_Text>();
            int cap = StaffLogic.RoleCap(Player, role);
            if (title != null) title.text = $"◆  {StaffLogic.RoleNames[role]}" + (FmRules.Enabled ? $"  <size=80%><color={Theme.Hex(Theme.TextDim)}>{people.Count}/{cap}명</color></size>" : "");
            var first = pad.Find("Card");
            var cards = new List<Transform> { first };
            foreach (Transform c in pad) if (c != first && c.name.StartsWith("Card")) cards.Add(c);
            while (cards.Count < people.Count)
            {
                var c = Instantiate(first.gameObject, pad).transform;
                c.name = "Card" + cards.Count;
                c.SetSiblingIndex(first.GetSiblingIndex() + cards.Count);
                cards.Add(c);
            }
            for (int i = 0; i < cards.Count; i++)
            {
                if (i == 0) BindRole(pad, people.Count > 0 ? people[0] : null);
                else
                {
                    cards[i].gameObject.SetActive(i < people.Count);
                    if (i < people.Count) BindCard(cards[i], people[i]);
                }
            }
            // 줄 높이: 사람이 많은 칸에 맞춘다
            var line = pad.parent.parent;
            int most = 1;
            foreach (Transform frame in line)
            {
                var p = frame.Find("Pad");
                if (p == null || !frame.gameObject.activeSelf) continue;
                int n = 0;
                foreach (Transform c in p) if (c.name.StartsWith("Card") && c.gameObject.activeSelf) n++;
                most = Mathf.Max(most, n);
            }
            var le = line.GetComponent<LayoutElement>();
            if (le != null) le.minHeight = le.preferredHeight = 116 + (most - 1) * 58;
        }

        void BindRole(RectTransform pad, Staff st)
        {
            var card = pad.Find("Card");
            card.gameObject.SetActive(st != null);
            pad.Find("Empty").gameObject.SetActive(st == null);
            if (st == null) return;
            BindCard(card, st);
        }

        void BindCard(Transform card, Staff st)
        {
            card.GetComponentInChildren<PortraitView>(true).Show(Face(st));
            T(card, "Info/Name").text = Tips.Person(st);
            Widgets.SetBar((RectTransform)card.Find("Info/Rating/Bar"), st.rating, 20, Theme.Torch);
            T(card, "Info/Rating/Value").text = StateUtil.Num(st.rating);
            T(card, "Info/Hint").text = $"주급 {StateUtil.Num(st.wage)} D · {StateUtil.Num(st.age)}세" + (FmRules.Enabled ? $"\n{Tips.StaffKeyAttrs(st, true, 2)}" : "");
            // 웹판 confirm(): 한 번 더 누르면 내보낸다
            string id = st.id;
            bool asking = confirmFire == id;
            var b = card.Find("Fire").GetComponent<Button>();
            b.GetComponentInChildren<TMP_Text>().text = asking ? $"정말 {Josa.J(st.name, "을/를")} 내보내겠습니까?" : "내보내기";
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() =>
            {
                if (confirmFire != id) { confirmFire = id; Refresh(); return; }
                confirmFire = null;
                msg = null;
                G.Mutate(s => StaffLogic.FireStaff(s, id));
            });
        }

        // ── 식솔 찾기 ──

        void DrawMarket()
        {
            var me = Player;
            double rep = Reputation.LudusReputation(S, me);
            repHint.text = $"우리 명성 {StateUtil.Num(rep)} ({Reputation.ReputationNames(rep)}). 실력 좋은 사람일수록 이름난 루두스를 원합니다";
            willingToggle.GetComponentInChildren<TMP_Text>().text = $"{(onlyWilling ? "◆" : "◇")} 올 마음이 있는 사람만";
            Seg(willingToggle, onlyWilling);

            string role = roleFilter.value == 0 ? null : StaffLogic.Roles[roleFilter.value - 1];
            int minRating = RatingSteps[Mathf.Clamp(ratingFilter.value, 0, RatingSteps.Length - 1)];
            var list = S.staffMarket.Select(id => S.staff.TryGetValue(id, out var c) ? c : null).Where(c => c != null)
                .Where(c => (role == null || c.role == role) && c.rating >= minRating)
                .Select(c => (s: c, d: StaffLogic.StaffDemand(S, c)))
                .Where(x => !onlyWilling || !x.d.refuse)
                .OrderByDescending(x => x.s.rating)
                .ToList();

            msgText.gameObject.SetActive(msg != null);
            msgText.text = msg ?? "";
            msgText.color = msgGood ? Theme.Good : Neg;
            empty.SetActive(list.Count == 0);
            rows.Sync(list.Count, (row, i) => BindRow(row, list[i].s, list[i].d));
        }

        void BindRow(RectTransform row, Staff c, Demand d)
        {
            row.Find("Name").GetComponentInChildren<PortraitView>(true).Show(Face(c));
            T(row, "Name/Text").text = Tips.Person(c);
            T(row, "Name/Age").text = $"{StateUtil.Num(c.age)}세";
            T(row, "Role").text = Tips.StaffRole(c.role);
            Widgets.SetBar((RectTransform)row.Find("Rating/Bar"), c.rating, 20, Theme.Torch);
            T(row, "Rating/Value").text = StateUtil.Num(c.rating);

            string id = c.id, name = c.name;
            string full = StaffLogic.RoleFull(S, Player, c.role);
            bool taken = full != null;
            string key = "staff:" + id;
            bool locked = Haggling.IsLocked(S, key);
            var nego = row.Find("Nego");
            var refuse = T(nego, "Refuse");
            refuse.gameObject.SetActive(d.refuse);
            refuse.text = d.refuse ? $"관심 없음 · {d.reason}" : "";
            foreach (var n in new[] { "Want", "Wage", "Offer" }) nego.Find(n).gameObject.SetActive(!d.refuse);

            var mood = T(row, "Mood");
            string line = Widgets.HaggleLine(S, key);
            mood.text = d.refuse ? $"<color={Theme.Hex(Theme.TextFaint)}>루두스 명성이 오르면 협상에 응합니다</color>"
                : taken ? $"<color={Theme.Hex(Theme.TextDim)}>{full}</color>"
                : line ?? $"<color={Theme.Hex(Theme.TextDim)}>{d.reason}</color>";
            if (d.refuse) return;

            if (!offers.ContainsKey(id)) offers[id] = d.wage;
            T(nego, "Want").text = $"요구 주급 {StateUtil.Num(d.wage)} D";
            var input = nego.Find("Wage").GetComponent<TMP_InputField>();
            input.onEndEdit.RemoveAllListeners();
            input.SetTextWithoutNotify(StateUtil.Num(offers[id]));
            input.onEndEdit.AddListener(v => { offers[id] = double.TryParse(v, out var w) ? w : 0; Refresh(); });
            var offer = nego.Find("Offer").GetComponent<Button>();
            offer.interactable = !taken && !locked && offers[id] > 0;
            offer.GetComponentInChildren<TMP_Text>().text = locked ? "결렬" : "고용 제안";
            offer.onClick.RemoveAllListeners();
            offer.onClick.AddListener(() =>
            {
                double w = offers[id];
                HaggleResult res = null;
                G.Act((s, rng) => res = StaffLogic.HaggleStaff(s, rng, id, w));
                msgGood = res.Accepted;
                if (res.Accepted) { msg = $"{Josa.J(name, "을/를")} 고용했습니다. {res.line}"; offers.Remove(id); }
                else
                {
                    msg = res.line;
                    if (res.status == "countered") offers[id] = res.counter;
                }
                Refresh();
            });
        }

        // ── 공용 ──

        /// <summary>식솔 초상화: 검투사와 같은 그림을 쓰되 평상복 차림 (웹판 StaffFace)</summary>
        Gladiator Face(Staff st) => new Gladiator
        {
            id = st.id, name = st.name, race = st.race, gender = st.gender, age = st.age, portraitSeed = st.portraitSeed, nation = S.nation,
            style = st.role == "medicus" ? "sorcerer" : "murmillo",
            equipment = new Equipment { main = "gladius", armor = "leather" },
        };

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        static void Fill(TMP_Dropdown d, IEnumerable<string> labels, int value)
        {
            var list = labels.ToList();
            bool same = d.options.Count == list.Count && d.options.Select(o => o.text).SequenceEqual(list);
            if (!same) { d.ClearOptions(); d.AddOptions(list); }
            d.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, list.Count - 1)));
        }

        /// <summary>웹판 .seg 버튼: 고른 것만 횃불색</summary>
        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
