// 세계 이벤트가 건 일시 배율 (src/core/modifiers.ts)
namespace Colosseum.Core
{
    public static class Modifiers
    {
        /// <summary>kind: ticket | food | market</summary>
        public static double ModifierMult(GameState state, string kind)
        {
            int now = Calendar.AbsDay(state.season, state.day);
            double p = 1;
            foreach (var m in state.modifiers)
                if (m.kind == kind && m.until >= now) p = p * m.mult;
            return p;
        }
    }
}
