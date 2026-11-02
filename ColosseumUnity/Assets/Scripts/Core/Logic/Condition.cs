// 검투사 컨디션 (src/core/condition.ts). performance는 GladiatorRules.Performance에 있다.
using Colosseum.Core.Combat;
using System;

namespace Colosseum.Core
{
    public static class Condition
    {
        /// <summary>탈진: 혹사도가 너무 높아 쉬게 해야 하는 상태</summary>
        public static bool IsJaded(Gladiator g) => g.wear >= 85;

        /// <summary>경기 직후 컨디션 변화. rule: training | missio | sudden</summary>
        public static void AfterMatch(Gladiator g, double at, string rule)
        {
            double gap = g.lastFightAt == null ? 30 : at - g.lastFightAt.Value;
            double fatigueMult = (double)Bal.Rules[rule]["fatigueMult"];
            g.fatigue = Math.Min(100, g.fatigue + (double)Bal.Training["fightFatigue"] * fatigueMult);
            // 일주일 안에 다시 뛰면 혹사가 크게 쌓인다
            g.wear = Math.Min(100, g.wear + (12 + Math.Max(0, 7 - gap) * 5) * fatigueMult);
            g.sharpness = Math.Min(100, g.sharpness + (rule == "training" ? 10 : 16));
            g.lastFightAt = at;
        }

        /// <summary>하루치 회복과 감각 변화. 부상이 다 나은 날이면 그 부상을 돌려준다 (영구 손상 판정용)</summary>
        public static Injury DailyCondition(Gladiator g, double at, double medicalLevel, double bathLevel = 0, double healMult = 1)
        {
            Injury healed = null;
            var T = Bal.Training;
            bool resting = g.training.focus == "rest";
            double recover = (double)T["fatigueRecoveryBase"] + g.attrs["rec"] * (double)T["fatigueRecoveryPerRec"] + (resting ? (double)T["restRecoveryBonus"] : 0) + bathLevel * 2;
            double load = resting ? 0 : (double)T["intensity"][g.training.intensity]["fatigue"];
            g.fatigue = Math.Max(0, Math.Min(100, g.fatigue + load - recover));
            g.wear = Math.Max(0, g.wear - (1.2 + (resting ? 1.5 : 0) + bathLevel * 0.3));
            double idle = g.lastFightAt == null ? 99 : at - g.lastFightAt.Value;
            if (idle > 5) g.sharpness = Math.Max(0, g.sharpness - 1.2);
            else if (!resting) g.sharpness = Math.Min(100, g.sharpness + 0.1);
            if (g.injury != null)
            {
                double speed = (g.race == "lizardfolk" ? 2 : 1) * (1 + (medicalLevel - 1) * 0.2) * healMult * (resting ? 1.5 : 1);
                g.injury.daysLeft -= speed;
                if (g.injury.daysLeft <= 0) { healed = g.injury; g.injury = null; }
            }
            // 사기는 평소 수준(60) 쪽으로 천천히 돌아간다
            double drift = (double)Bal.Morale["dailyDrift"] * (g.race == "lizardfolk" ? 0.5 : 1);
            if (g.morale > 62) g.morale -= drift;
            else if (g.morale < 58 && g.unpaidWeeks == 0) g.morale += drift;
            return healed;
        }
    }
}
