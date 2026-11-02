// 초상화 사양 (src/core/avatar.ts makeAvatar). 무엇을 그릴지만 정한다.
using System.Collections.Generic;

namespace Colosseum.Core
{
    public class AvatarSpec
    {
        public string race, skin, hair, hairColor, eyes, eyeColor, mouth, beard, helmet, armor, cloth;
        public int variant;
        public bool oneEyed, female, old;
    }

    public static class Avatar
    {
        public static readonly string[] HairStyles = { "tuft", "short", "long", "pony", "mohawk", "bun", "braids", "messy" };
        public static readonly string[] EyeStyles = { "round", "fierce", "sleepy", "wide" };
        public static readonly string[] MouthStyles = { "line", "smile", "grit", "frown", "open" };
        public static readonly string[] BeardStyles = { "stubble", "short", "full", "braided", "goatee" };
        public static readonly string[] HelmetStyles = { "crest", "horned", "cap", "hood" };

        public static readonly Dictionary<string, string[]> Skins = new()
        {
            ["human"] = new[] { "#f3d6bb", "#e2b28c", "#b97d52", "#7a4b2e" },
            ["elf"] = new[] { "#f2ebe2", "#d7dceb", "#a9b6cf" },
            ["dwarf"] = new[] { "#eab896", "#d39a73", "#b9805b" },
            ["orc"] = new[] { "#8aa45a", "#738a4b", "#98987a" },
            ["minotaur"] = new[] { "#3a2a22", "#6c4630", "#8b7865" },
            ["lizardfolk"] = new[] { "#5f8a52", "#a08a40", "#4f7a74" },
            ["beastkin"] = new[] { "#d4a356", "#b0855a", "#c98b3e", "#2e2724", "#8a6a4a" },
        };

        public static readonly string[] HairColors = { "#211915", "#4a2f1f", "#8a5a2b", "#d1ad5f", "#a8331f", "#e6e0d2" };
        static readonly string[] EyeColors = { "#2a1c14", "#3a5a7a", "#4a6a3a", "#6a3a1a" };
        static readonly string[] Cloth = { "#7a1e1a", "#3d4f6b", "#5b2a4e", "#4a5a3a", "#6b4a2a", "#2f2f35" };
        static readonly string[][] BeastSkins = { new[] { "#d4a356", "#b0855a", "#c98b3e" }, new[] { "#2e2724", "#8a6a4a", "#5a4a3e" } };

        static bool NoHair(string r) => r == "minotaur" || r == "lizardfolk" || r == "beastkin";

        public static AvatarSpec Make(Gladiator g) => Make(g, g.portraitSeed);

        public static AvatarSpec Make(Gladiator g, int seed)
        {
            var r = new Rng(unchecked((uint)(seed ^ 0x2545f491)));
            bool old = GladiatorRules.AgeStage(g) == "decline";
            bool female = g.gender == "f";
            string weight = Data.Armor(g.equipment.armor)?.weight ?? "light";
            // TS: style === "sorcerer" || (style === "runeblade" && r.chance(0.5)) — 단락 평가 순서 그대로
            string armor = g.style == "sorcerer" || (g.style == "runeblade" && r.Chance(0.5)) ? "robe" : weight;
            bool canBeard = !female && !NoHair(g.race) && g.race != "elf";
            int variant = r.Int(0, 1);
            var s = new AvatarSpec { race = g.race, variant = variant, armor = armor, female = female, old = old };
            s.skin = g.race == "beastkin" ? r.Pick(BeastSkins[variant]) : r.Pick(Skins[g.race]);
            if (NoHair(g.race)) s.hair = null;
            else s.hair = r.Chance(female ? 0.97 : 0.82) ? r.Pick(female ? new[] { "long", "pony", "bun", "braids" } : HairStyles) : null;
            s.hairColor = old ? "#e6e0d2" : r.Pick(new[] { HairColors[0], HairColors[1], HairColors[2], HairColors[3], HairColors[4] });
            s.eyes = g.race == "orc" || g.race == "minotaur" ? (r.Chance(0.6) ? "fierce" : "round") : old ? "sleepy" : r.Pick(EyeStyles);
            s.eyeColor = r.Pick(EyeColors);
            s.mouth = NoHair(g.race) ? null : r.Pick(MouthStyles);
            if (g.race == "dwarf" && !female) s.beard = r.Chance(0.85) ? r.Pick(new[] { "full", "braided" }) : null;
            else s.beard = canBeard && r.Chance(old ? 0.8 : 0.4) ? r.Pick(BeardStyles) : null;
            s.helmet = g.equipment.helmet != null && !NoHair(g.race) && r.Chance(0.3) ? r.Pick(HelmetStyles) : null;
            s.cloth = r.Pick(Cloth);
            s.oneEyed = g.traits.Contains("one_eyed");
            return s;
        }
    }
}
