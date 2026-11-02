// 전투 교범 패널 (웹판 components/DoctrinePanel.tsx): 자세·거리·노리는 곳, 기술 순서, 조건 명령, 항복 여부.
// 고칠 때마다 숙련도가 떨어진다. 스크롤 영역 안이라 select 대신 버튼을 눌러 그 아래 펼친 선택지(Picker)에서 고른다.
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class DoctrinePanel : MonoBehaviour
    {
        [SerializeField] TMP_Text famValue;
        [SerializeField] RectTransform famBar;
        [Tooltip("우리 검투사일 때만 보이는 숙련도 안내")]
        [SerializeField] GameObject famHint;
        [Header("자세·거리·노리는 곳 (자식 버튼 이름 = 값)")]
        [SerializeField] RectTransform stanceGrid;
        [SerializeField] RectTransform rangeGrid;
        [SerializeField] RectTransform focusGrid;
        [SerializeField] TMP_Text stanceDesc;
        [SerializeField] TMP_Text focusDesc;
        [Header("기술 순서")]
        [Tooltip("줄 원본: N, Name, Up, Down")]
        [SerializeField] RectTransform orderTemplate;
        [SerializeField] GameObject orderEmpty;
        [Header("조건 명령")]
        [Tooltip("줄 원본: Text(보기만) / Edit(Cond, Value, Action, Arg, Delete)")]
        [SerializeField] RectTransform ruleTemplate;
        [Tooltip("조건·행동·대상 버튼을 누르면 펼쳐지는 선택지")]
        [SerializeField] GameObject picker;
        [SerializeField] TMP_Text pickerTitle;
        [SerializeField] Button pickTemplate;
        [SerializeField] Button addRule;
        [SerializeField] Button surrender;

        static GameController G => GameController.I;
        static GameState S => GameController.I.State;

        TemplateList<RectTransform> orderRows, ruleRows;
        TemplateList<Button> pickRows;
        string gid;
        bool editable;
        // 펼친 선택지: 몇 번째 명령의 어느 칸 (cond | action | arg)
        int pickRule = -1;
        string pickField;

        static string SkillName(string id) => Data.Skill(id)?.name ?? id;

        void Awake()
        {
            orderRows = new TemplateList<RectTransform>(orderTemplate);
            ruleRows = new TemplateList<RectTransform>(ruleTemplate);
            pickRows = new TemplateList<Button>(pickTemplate);
            Wire(stanceGrid, d => d.stance, (d, k) => d.stance = k);
            Wire(rangeGrid, d => d.range, (d, k) => d.range = k);
            Wire(focusGrid, d => d.focus, (d, k) => d.focus = k);
            addRule.onClick.AddListener(() => Change(d => d.rules.Add(new DoctrineRule
            {
                cond = new RuleCond { type = "myHpBelow", value = 40 },
                action = new RuleAction { type = "stance", stance = "bulwark" },
            })));
            surrender.onClick.AddListener(() => Change(d => d.allowSurrender = !d.allowSurrender));
        }

        void Wire(RectTransform grid, Func<Doctrine, string> get, Action<Doctrine, string> set)
        {
            foreach (Transform c in grid)
            {
                string k = c.name;
                c.GetComponent<Button>().onClick.AddListener(() =>
                {
                    var d = Doc;
                    if (d != null && get(d) != k) Change(x => set(x, k));
                });
            }
        }

        Doctrine Doc => gid != null && S.gladiators.TryGetValue(gid, out var g) ? g.doctrine : null;

        /// <summary>교범이 없으면 숨긴다. editable: 우리 검투사이고 살아 있을 때</summary>
        public void Show(Gladiator g, bool canEdit)
        {
            bool has = g.doctrine != null;
            gameObject.SetActive(has);
            if (!has) return;
            if (gid != g.id) { gid = g.id; pickRule = -1; }
            editable = canEdit;
            if (!editable) pickRule = -1;
            Draw();
        }

        /// <summary>교범을 바꾸면 숙련도가 떨어진다</summary>
        void Change(Action<Doctrine> patch)
        {
            if (!editable) return;
            string id = gid;
            pickRule = -1;
            G.Mutate(s =>
            {
                var d = s.gladiators[id].doctrine;
                patch(d);
                d.familiarity = Math.Max(0, d.familiarity - Tactics.FamiliarityChangeCost);
            });
        }

        void Move(int i, int dir)
        {
            Change(d =>
            {
                int j = i + dir;
                if (j < 0 || j >= d.order.Count) return;
                (d.order[i], d.order[j]) = (d.order[j], d.order[i]);
            });
        }

        void TogglePick(int i, string field)
        {
            if (pickRule == i && pickField == field) pickRule = -1;
            else { pickRule = i; pickField = field; }
            Draw();
        }

        void Draw()
        {
            var d = Doc;
            if (d == null) return;
            string faint = Theme.Hex(Theme.TextFaint);

            famValue.text = Math.Round(d.familiarity).ToString();
            Widgets.SetBar(famBar, d.familiarity, 100, Theme.Torch);
            famHint.SetActive(editable);

            Choice(stanceGrid, d.stance);
            Choice(rangeGrid, d.range);
            Choice(focusGrid, d.focus);
            stanceDesc.text = Tactics.StanceDesc.TryGetValue(d.stance ?? "", out var sd) ? sd : "";
            focusDesc.text = Tactics.FocusDesc.TryGetValue(d.focus ?? "", out var fd) ? fd : "";

            // 기술 순서
            orderEmpty.SetActive(d.order.Count == 0);
            orderRows.Sync(d.order.Count, (row, i) =>
            {
                T(row, "N").text = (i + 1).ToString();
                T(row, "Name").text = SkillName(d.order[i]);
                var up = row.Find("Up").GetComponent<Button>();
                var down = row.Find("Down").GetComponent<Button>();
                up.gameObject.SetActive(editable);
                down.gameObject.SetActive(editable);
                up.interactable = i > 0;
                down.interactable = i < d.order.Count - 1;
                int at = i;
                up.onClick.RemoveAllListeners();
                up.onClick.AddListener(() => Move(at, -1));
                down.onClick.RemoveAllListeners();
                down.onClick.AddListener(() => Move(at, 1));
            });

            // 조건 명령
            if (pickRule >= d.rules.Count) pickRule = -1;
            ruleRows.Sync(d.rules.Count, (row, i) => DrawRule(row, d, i));
            addRule.gameObject.SetActive(editable && d.rules.Count < 3);
            surrender.interactable = editable;
            surrender.GetComponentInChildren<TMP_Text>().text =
                $"{(!d.allowSurrender ? "■" : "□")} 항복하지 않고 끝까지 싸우기 <color={faint}>(살아날 길이 줄어듭니다)</color>";

            DrawPicker(d);
        }

        void DrawRule(RectTransform row, Doctrine d, int i)
        {
            var r = d.rules[i];
            var text = T(row, "Text");
            text.gameObject.SetActive(!editable);
            text.text = $"{i + 1}. {Tactics.DescribeRule(r, SkillName)}";
            var edit = row.Find("Edit").gameObject;
            edit.SetActive(editable);
            if (!editable) return;

            var c = r.cond;
            var a = r.action;
            int at = i;
            var cond = Btn(row, "Edit/Cond", Tactics.CondNames.TryGetValue(c.type ?? "", out var cn) ? cn : c.type, () => TogglePick(at, "cond"));
            Seg(cond, pickRule == i && pickField == "cond");

            var value = row.Find("Edit/Value").GetComponent<TMP_InputField>();
            value.gameObject.SetActive(c.type != "oppDisabled");
            value.SetTextWithoutNotify(Math.Round(c.value).ToString());
            value.onEndEdit.RemoveAllListeners();
            value.onEndEdit.AddListener(txt => SetValue(at, txt));

            var act = Btn(row, "Edit/Action", Tactics.ActionNames.TryGetValue(a.type ?? "", out var an) ? an : a.type, () => TogglePick(at, "action"));
            Seg(act, pickRule == i && pickField == "action");

            bool hasArg = a.type == "stance" || a.type == "skill";
            string argLabel = a.type == "stance" ? (Tactics.StanceNames.TryGetValue(a.stance ?? "", out var sn) ? sn : a.stance) : a.type == "skill" ? SkillName(a.skill) : "";
            var arg = Btn(row, "Edit/Arg", argLabel, () => TogglePick(at, "arg"));
            arg.gameObject.SetActive(hasArg);
            Seg(arg, pickRule == i && pickField == "arg");

            Btn(row, "Edit/Delete", "지우기", () => Change(x => x.rules.RemoveAt(at)));
        }

        void SetValue(int i, string txt)
        {
            var d = Doc;
            if (d == null || i >= d.rules.Count || !int.TryParse(txt, out var v)) { Draw(); return; }
            var c = d.rules[i].cond;
            v = Math.Clamp(v, 1, c.type == "roundAtLeast" ? 30 : 99);
            if (v == c.value) { Draw(); return; }
            Change(x => x.rules[i].cond.value = v);
        }

        /// <summary>펼친 선택지: 고르면 바로 반영한다 (웹판 select onChange 한 번)</summary>
        void DrawPicker(Doctrine d)
        {
            bool on = editable && pickRule >= 0;
            picker.SetActive(on);
            if (!on) return;
            var r = d.rules[pickRule];
            List<string> keys;
            Func<string, string> label;
            string current, field;
            if (pickField == "cond")
            {
                keys = Tactics.CondNames.Keys.ToList(); label = k => Tactics.CondNames[k]; current = r.cond.type; field = "조건";
            }
            else if (pickField == "action")
            {
                keys = Tactics.ActionNames.Keys.ToList(); label = k => Tactics.ActionNames[k]; current = r.action.type; field = "행동";
            }
            else if (r.action.type == "stance")
            {
                keys = Tactics.StanceNames.Keys.ToList(); label = k => Tactics.StanceNames[k]; current = r.action.stance; field = "자세";
            }
            else
            {
                keys = d.order.ToList(); label = SkillName; current = r.action.skill; field = "기술";
            }
            pickerTitle.text = $"{pickRule + 1}번 명령 — {field} 고르기";
            int ri = pickRule;
            string pf = pickField;
            pickRows.Sync(keys.Count, (b, j) =>
            {
                string k = keys[j];
                b.GetComponentInChildren<TMP_Text>().text = label(k);
                Seg(b, k == current);
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => Pick(ri, pf, k));
            });
        }

        void Pick(int i, string field, string k)
        {
            var d = Doc;
            if (d == null || i >= d.rules.Count) return;
            var r = d.rules[i];
            if (field == "cond")
            {
                if (r.cond.type == k) { pickRule = -1; Draw(); return; }
                Change(x => x.rules[i].cond = k == "oppDisabled" ? new RuleCond { type = k } : new RuleCond { type = k, value = k == "roundAtLeast" ? 10 : 40 });
            }
            else if (field == "action")
            {
                if (r.action.type == k) { pickRule = -1; Draw(); return; }
                var skills = d.order;
                Change(x => x.rules[i].action =
                    k == "stance" ? new RuleAction { type = k, stance = "bulwark" }
                    : k == "skill" ? new RuleAction { type = k, skill = skills.Count > 0 ? skills[0] : "skill.power_strike" }
                    : new RuleAction { type = k });
            }
            else if (r.action.type == "stance")
            {
                if (r.action.stance == k) { pickRule = -1; Draw(); return; }
                Change(x => x.rules[i].action = new RuleAction { type = "stance", stance = k });
            }
            else
            {
                if (r.action.skill == k) { pickRule = -1; Draw(); return; }
                Change(x => x.rules[i].action = new RuleAction { type = "skill", skill = k });
            }
        }

        /// <summary>자세·거리·노리는 곳 줄: 고른 것만 횃불색. 남의 검투사는 고른 것만 또렷하게</summary>
        void Choice(RectTransform grid, string value)
        {
            foreach (Transform c in grid)
            {
                var b = c.GetComponent<Button>();
                bool on = c.name == value;
                Seg(b, on);
                b.interactable = editable || on;
            }
        }

        static Button Btn(Transform row, string path, string text, UnityEngine.Events.UnityAction click)
        {
            var b = row.Find(path).GetComponent<Button>();
            b.GetComponentInChildren<TMP_Text>().text = text;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(click);
            return b;
        }

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        /// <summary>웹판 .seg 버튼: 고른 것만 횃불색</summary>
        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
