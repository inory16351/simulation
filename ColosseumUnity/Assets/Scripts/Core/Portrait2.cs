// 그림 초상화(파츠 조립) 사양 (src/core/portrait2.ts). 같은 검투사는 늘 웹판과 같은 얼굴이 나온다.
// 파츠 이미지와 놓을 자리는 tools/portrait-parts.mjs가 data/portrait2.json으로 만든다.
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Colosseum.Core
{
    public class PaintedSpec
    {
        public string race;
        /// <summary>0부터</summary>
        public int head;
        public string hair, beard, helmet, armor;
        /// <summary>얼굴에 그리는 흉터·문신 ('mark_...' 파츠), 아래부터 그린다</summary>
        public List<string> marks = new();
        /// <summary>머리카락·수염 색 이름 (black, dark, brown, auburn, blond, grey). null이면 그림 그대로</summary>
        public string tint;
    }

    /// <summary>한 시점에서 그릴 층 (아래부터). 셀 좌표 [x, y, w, h], 위가 0</summary>
    public class PortraitLayer
    {
        /// <summary>Art 폴더 기준 경로, 확장자 없음 (예: portrait2/human/front/head_3)</summary>
        public string src;
        public float x, y, w, h;
        /// <summary>얼굴 다시 그리기: 이 y(셀 좌표) 아래는 자른다</summary>
        public float? clipBottom;
        /// <summary>닫힌 투구 아래 머리: 이 y(셀 좌표) 위는 자른다</summary>
        public float? clipTop;
        public string tint;
    }

    public static class Portrait2
    {
        static JObject data;
        static JObject Races => (JObject)Data()["races"];
        static JObject Data() => data ??= JObject.Parse(UnityEngine.Resources.Load<UnityEngine.TextAsset>("Data/portrait2").text);

        /// <summary>테스트 등에서 직접 넣는다</summary>
        public static void Load(string json) => data = JObject.Parse(json);

        public static int Cell => (int)Data()["cell"];

        static readonly Regex Digits = new(@"^\d+$");

        public static bool HasPainted(string race, string view = "front") => Races[race]?[view]?["heads"] is JArray a && a.Count > 0;

        public static PaintedSpec Spec(Gladiator g)
        {
            var rd = Races[g.race];
            if (rd == null || !(((JArray)rd["front"]["heads"]).Count > 0)) return null;
            var r = new Rng(unchecked((uint)(g.portraitSeed ^ 0x51ed27)));
            bool female = g.gender == "f";
            // 머리는 성별에 맞는 것 중에서
            var heads = rd["heads"].ToObject<List<string>>();
            var pool = heads.Select((s, i) => (s, i)).Where(t => t.s == (female ? "f" : "m")).Select(t => t.i).ToList();
            int head = r.Pick(pool.Count > 0 ? pool : Enumerable.Range(0, heads.Count).ToList());
            var partKeys = ((JObject)rd["front"]["parts"]).Properties().Select(p => p.Name).ToList();
            // 이 종족에 그림이 있는 파츠만 고른다. 같은 종류의 다른 세트('hair_short_2')도 후보에 넣는다
            List<string> Variants(string kind) => partKeys.Where(k => k == kind || (k.StartsWith(kind + "_") && Digits.IsMatch(k.Substring(kind.Length + 1)))).ToList();
            bool Has(string n) => n != null && Variants(n).Count > 0;
            string PickVariant(string kind)
            {
                var v = kind != null ? Variants(kind) : new List<string>();
                return v.Count > 1 ? r.Pick(v) : v.FirstOrDefault();
            }
            string Only(params string[] list)
            {
                var ok = list.Where(n => n == null || Has(n)).ToList();
                return ok.Count > 0 ? PickVariant(r.Pick(ok)) : null;
            }
            // 투구는 반쯤만 쓴 모습으로 그린다. 밀폐 투구는 닫힌 투구, 나머지는 열린 투구 그림
            string helmetBase = r.Chance(0.5) ? Core.Data.Armor(g.equipment.helmet ?? "")?.id.Split('+')[0] : null;
            string helmetWant = helmetBase == null ? null : helmetBase == "sealed_helm" ? "helmet_visor" : "helmet_crest";
            string helmet = PickVariant(Has(helmetWant) ? helmetWant : Has("helmet_crest") && helmetWant != null ? "helmet_crest" : null);
            string hair = helmet != null ? null
                : female ? Only("hair_long", "hair_braids", "hair_short")
                : Only("hair_short", "hair_short", "hair_long", "hair_braids", null);
            // 수염은 남자만, 그것도 다는 아니다. 면갑을 쓰면 가려져서 뺀다
            bool old = GladiatorRules.AgeStage(g) == "decline";
            string beard = !female && !(helmet?.StartsWith("helmet_visor") ?? false) && r.Chance(old ? 0.75 : g.race == "dwarf" ? 0.85 : 0.45) ? Only("beard_full", "beard_short") : null;
            string weight = Core.Data.Armor(g.equipment.armor)?.weight;
            string armorWant = weight == "heavy" || weight == "medium" ? "armor_plate" : weight == "light" && g.equipment.armor.Split('+')[0] != "loincloth" ? "armor_harness" : null;
            string armor = PickVariant(armorWant);
            // 머리색: 늙으면 희끗해지고, 나머지는 그림 그대로이거나 자연스러운 색 중 하나
            string tint = old && r.Chance(0.7) ? "grey"
                : r.Weighted(new Dictionary<string, double> { ["none"] = 3, ["black"] = 2, ["dark"] = 2, ["brown"] = 2, ["auburn"] = 1, ["blond"] = 1.2 });
            return new PaintedSpec { race = g.race, head = head, hair = hair, beard = beard, helmet = helmet, armor = armor, marks = PickMarks(g, partKeys), tint = tint == "none" ? null : tint };
        }

        /// <summary>흉터·문신. 위 파츠와 다른 난수열을 써서 기존 얼굴 고르기가 바뀌지 않게 한다.
        /// 문신은 처음부터 정해지고(검투사의 25%), 흉터는 싸운 횟수가 늘수록 생기며 한 번 생기면 남는다.</summary>
        static List<string> PickMarks(Gladiator g, List<string> parts)
        {
            var r = new Rng(unchecked((uint)(g.portraitSeed ^ 0x3a7c15)));
            var scars = parts.Where(k => k.StartsWith("mark_scar")).ToList();
            var tattoos = parts.Where(k => k.StartsWith("mark_") && !k.StartsWith("mark_scar")).ToList();
            double tattooRoll = r.Next(), scarRoll = r.Next();
            string tattoo = tattoos.Count > 0 ? r.Pick(tattoos) : null, scar = scars.Count > 0 ? r.Pick(scars) : null;
            int fights = g.record.wins + g.record.losses;
            var o = new List<string>();
            if (tattoo != null && tattooRoll < 0.25) o.Add(tattoo);
            if (scar != null && scarRoll < System.Math.Min(0.65, 0.12 + fights * 0.025)) o.Add(scar);
            return o;
        }

        /// <summary>닫힌 투구가 머리를 통째로 감싸는 종족: 투구 아래쪽 위로는 머리 그림을 지운다 (투구 옆으로 귀가 삐져나오지 않게)</summary>
        static readonly HashSet<string> VisorHidesHead = new() { "human", "elf", "orc", "dwarf" };

        public static List<PortraitLayer> Layers(PaintedSpec spec, string view = "front")
        {
            var vd = Races[spec.race]?[view];
            if (vd == null || ((JArray)vd["heads"]).Count == 0) return null;
            string dir = $"portrait2/{spec.race}/{view}";
            float c = Cell;
            var visor = view == "front" && (spec.helmet?.StartsWith("helmet_visor") ?? false) && VisorHidesHead.Contains(spec.race) ? vd["parts"]?[spec.helmet]?[spec.head] : null;
            if (visor != null && visor.Type == JTokenType.Null) visor = null;
            var o = new List<PortraitLayer> { new() { src = $"{dir}/head_{spec.head + 1}", x = 0, y = 0, w = c, h = c, clipTop = visor != null ? (float?)((float)visor[1] + (float)visor[3] * 0.7f) : null } };
            void Part(string name, string tint = null)
            {
                if (name == null) return;
                var rect = vd["parts"]?[name]?[spec.head];
                if (rect == null || rect.Type == JTokenType.Null) return;
                o.Add(new PortraitLayer { src = $"{dir}/{name}", x = (float)rect[0], y = (float)rect[1], w = (float)rect[2], h = (float)rect[3], tint = tint });
            }
            if (view == "front")
            {
                Part(spec.armor);
                // 갑옷이 턱을 덮지 않게 얼굴(턱 줄 위)을 다시 그린다 (머리를 감싸는 투구면 필요 없다)
                if (spec.armor != null && visor == null) o.Add(new PortraitLayer { src = $"{dir}/head_{spec.head + 1}", x = 0, y = 0, w = c, h = c, clipBottom = (float?)vd["heads"][spec.head]["chin"] });
            }
            if (visor == null) foreach (var m in spec.marks ?? new List<string>()) Part(m);
            Part(spec.beard, spec.tint);
            // 위아래로 나눠 맞춘 수염의 아래(턱수염) 조각
            if (spec.beard != null) Part(spec.beard + "_lo", spec.tint);
            Part(spec.hair, spec.tint);
            Part(spec.helmet);
            return o;
        }
    }
}
