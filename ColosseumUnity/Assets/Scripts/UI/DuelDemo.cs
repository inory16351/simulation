// 대결 데모 화면: C#으로 옮긴 생성기·전투·중계가 씬에서 도는지 보여 준다.
// 줄은 하이어라키에 있는 비활성 LineTemplate을 복제해서 만든다.
using System.Collections;
using System.Collections.Generic;
using Colosseum.Core;
using Colosseum.Core.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class DuelDemo : MonoBehaviour
    {
        [SerializeField] Text fighterA;
        [SerializeField] Text fighterB;
        [SerializeField] PortraitView portraitA;
        [SerializeField] PortraitView portraitB;
        [SerializeField] Text result;
        [SerializeField] Button fightButton;
        [SerializeField] RectTransform logContent;
        [SerializeField] Text lineTemplate;
        [SerializeField] ScrollRect scroll;
        [SerializeField] float lineDelay = 0.25f;

        static readonly Dictionary<string, Color> ToneColors = new()
        {
            ["normal"] = new Color32(0xd9, 0xcb, 0xb0, 0xff),
            ["hit"] = new Color32(0xe8, 0xc8, 0x90, 0xff),
            ["crit"] = new Color32(0xff, 0x8a, 0x5c, 0xff),
            ["blood"] = new Color32(0xd8, 0x5a, 0x48, 0xff),
            ["crowd"] = new Color32(0xb0, 0x8a, 0x4f, 0xff),
            ["system"] = new Color32(0x9a, 0xa8, 0xc0, 0xff),
            ["death"] = new Color32(0xff, 0x55, 0x55, 0xff),
        };

        uint seed = 1;
        Coroutine playing;

        void Start()
        {
            Data.Ensure();
            lineTemplate.gameObject.SetActive(false);
            fightButton.onClick.AddListener(Fight);
            Fight();
        }

        void Fight()
        {
            if (playing != null) StopCoroutine(playing);
            foreach (Transform c in logContent)
                if (c != lineTemplate.transform) Destroy(c.gameObject);

            var rng = new Rng(seed++ * 7919u);
            var a = GladiatorGen.Generate(rng, new GenOptions { id = "a", nation = "aurelia", target = rng.Range(8, 13) });
            var b = GladiatorGen.Generate(rng, new GenOptions { id = "b", nation = "grommar", target = rng.Range(8, 13) });
            fighterA.text = Describe(a);
            fighterB.text = Describe(b);
            if (portraitA) portraitA.Show(a);
            if (portraitB) portraitB.Show(b);
            result.text = "";

            var r = Sim.Run(a, b, new MatchOptions { rule = "missio", seed = rng.State });
            var lines = Commentary.Build(r.events, new Dictionary<string, Gladiator> { [a.id] = a, [b.id] = b }, rng.State);
            string winner = r.winnerId == a.id ? a.name : b.name;
            playing = StartCoroutine(Play(lines, $"{winner} 승리 · {r.rounds}라운드{(r.died != null ? " · 사망" : "")}"));
        }

        static string Describe(Gladiator g)
        {
            var style = Data.Style(g.style);
            return $"{g.name}\n{Data.Race(g.race).name} · {style.name} · 현재 능력 {GladiatorRules.ComputeCA(g)}\n" +
                   $"예측 {g.attrs["read"]}  기만 {g.attrs["feint"]}  천재 {g.attrs["genius"]}  적응 {g.attrs["adapt"]}";
        }

        IEnumerator Play(List<CommentaryLine> lines, string summary)
        {
            foreach (var l in lines)
            {
                var t = Instantiate(lineTemplate, logContent);
                t.gameObject.SetActive(true);
                t.text = l.round > 0 ? $"[{l.round}]  {l.text}" : l.text;
                t.color = ToneColors.TryGetValue(l.tone, out var c) ? c : Color.white;
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0;
                yield return new WaitForSeconds(lineDelay);
            }
            result.text = summary;
            playing = null;
        }
    }
}
