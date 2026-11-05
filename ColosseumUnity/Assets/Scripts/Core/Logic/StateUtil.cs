// 상태 공용 함수 (src/core/state.ts의 drawSeed, pushMessage). 저장/불러오기는 Unity 쪽에서 따로 한다.
// JS와 같은 숫자 문자열, 넣은 순서를 지키는 삭제 같은 이식 도우미도 여기 둔다.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Colosseum.Core
{
    public static class StateUtil
    {
        /// <summary>게임 상태의 RNG에서 시드 하나를 뽑고 상태를 전진시킨다.</summary>
        public static int DrawSeed(GameState state)
        {
            var r = new Rng(state.rngState);
            // r.int(0, 2 ** 31 - 1): Rng.Int(int,int)는 max - min + 1에서 넘치므로 직접 계산한다
            int s = (int)Math.Floor(r.Next() * 2147483648.0);
            state.rngState = r.State;
            return s;
        }

        static GameState namesOwner;
        static HashSet<string> namesCache;

        /// <summary>검투사 이름 집합 (새 이름이 겹치지 않게). FM식은 세계 공용 집합을 두고 생성기가 이름을 더해 간다 — 480팀에서 부를 때마다 새로 만들면 O(n²).
        /// 다른 상태(복사본 검증·불러오기)면 다시 만든다. 웹판 규칙은 지금 있는 검투사 이름만으로 새로 만든다 (지운 검투사 이름이 남으면 난수 순서가 달라진다)</summary>
        public static HashSet<string> UsedNames(GameState state)
        {
            if (!FmRules.Enabled) return Names(state);
            if (namesOwner != state || namesCache == null)
            {
                namesOwner = state;
                namesCache = Names(state);
            }
            return namesCache;
        }

        static HashSet<string> Names(GameState state)
        {
            var set = new HashSet<string>();
            foreach (var g in state.gladiators.Values) set.Add(g.name);
            return set;
        }

        public static void PushMessage(GameState state, string kind, string title, string body)
        {
            state.inbox.Insert(0, new InboxMessage
            {
                kind = kind, title = title, body = body,
                id = $"M{state.nextId++}", season = state.season, day = state.day, read = false,
            });
        }

        /// <summary>TS의 pushMessage(state, { kind, title, body }) 모양 그대로 부를 때</summary>
        public static void PushMessage(GameState state, InboxMessage msg) => PushMessage(state, msg.kind, msg.title, msg.body);

        // ---- 이식 도우미 ----

        /// <summary>JS의 `${number}`와 같은 문자열 (정수는 소수점 없이)</summary>
        public static string Num(double x)
        {
            if (double.IsNaN(x)) return "NaN";
            if (double.IsInfinity(x)) return x > 0 ? "Infinity" : "-Infinity";
            if (x == Math.Floor(x) && Math.Abs(x) < 9e15) return ((long)x).ToString(CultureInfo.InvariantCulture);
            return x.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>JS number.toFixed(n)</summary>
        public static string Fixed(double x, int n) => x.ToString("F" + n, CultureInfo.InvariantCulture);

        /// <summary>JS number.toLocaleString() (천 단위 콤마)</summary>
        public static string Locale(double x) => x.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>
        /// JS `delete obj[key]`와 같은 효과를 내되 남은 키의 순서를 지킨다.
        /// C# Dictionary는 지운 자리에 새 키를 채워 넣어 순회 순서가 JS와 달라지므로, 비우고 다시 넣는다.
        /// </summary>
        public static void RemoveOrdered<TV>(Dictionary<string, TV> d, string key)
        {
            if (!d.ContainsKey(key)) return;
            var keep = new List<KeyValuePair<string, TV>>(d.Count);
            foreach (var kv in d) if (kv.Key != key) keep.Add(kv);
            d.Clear();
            foreach (var kv in keep) d.Add(kv.Key, kv.Value);
        }

        /// <summary>여러 키를 한 번에 지운다 (순서 유지)</summary>
        public static void RemoveOrdered<TV>(Dictionary<string, TV> d, ICollection<string> keys)
        {
            bool any = false;
            foreach (var k in keys) if (d.ContainsKey(k)) { any = true; break; }
            if (!any) return;
            var keep = new List<KeyValuePair<string, TV>>(d.Count);
            foreach (var kv in d) if (!keys.Contains(kv.Key)) keep.Add(kv);
            d.Clear();
            foreach (var kv in keep) d.Add(kv.Key, kv.Value);
        }
    }
}
