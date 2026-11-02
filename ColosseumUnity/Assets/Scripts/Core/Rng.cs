// 시드 고정 난수 (mulberry32). src/core/rng.ts와 같은 수열을 낸다 (골든 테스트로 확인).
// 게임 로직에서는 UnityEngine.Random 대신 반드시 이 클래스를 사용한다.
using System;
using System.Collections.Generic;

namespace Colosseum.Core
{
    public sealed class Rng
    {
        uint s;

        public Rng(double seed) { s = unchecked((uint)(long)seed); }
        public Rng(uint seed) { s = seed; }

        public uint State => s;

        static int Imul(int a, int b) => unchecked(a * b);

        /// <summary>0 이상 1 미만</summary>
        public double Next()
        {
            s = unchecked(s + 0x6d2b79f5u);
            int t = unchecked((int)s);
            t = Imul(t ^ (int)((uint)t >> 15), t | 1);
            t ^= unchecked(t + Imul(t ^ (int)((uint)t >> 7), t | 61));
            return (uint)(t ^ (int)((uint)t >> 14)) / 4294967296.0;
        }

        /// <summary>min 이상 max 이하 정수</summary>
        public int Int(int min, int max) => (int)(min + Math.Floor(Next() * ((double)max - min + 1)));

        public double Range(double min, double max) => min + Next() * (max - min);

        public bool Chance(double p) => Next() < p;

        public T Pick<T>(IReadOnlyList<T> arr) => arr[(int)Math.Floor(Next() * arr.Count)];

        /// <summary>가중치 추첨. 넣은 순서대로 훑는다 (TS Object.entries 순서와 같게)</summary>
        public string Weighted(IEnumerable<KeyValuePair<string, double>> weights)
        {
            var entries = new List<KeyValuePair<string, double>>(weights);
            double total = 0;
            foreach (var e in entries) total += e.Value;
            double r = Next() * total;
            foreach (var e in entries)
            {
                r -= e.Value;
                if (r < 0) return e.Key;
            }
            return entries[entries.Count - 1].Key;
        }

        public List<T> Shuffle<T>(IReadOnlyList<T> arr)
        {
            var o = new List<T>(arr);
            for (int i = o.Count - 1; i > 0; i--)
            {
                int j = Int(0, i);
                (o[i], o[j]) = (o[j], o[i]);
            }
            return o;
        }

        /// <summary>정규분포 (Box-Muller)</summary>
        public double Normal(double mean, double sd)
        {
            double u = 1 - Next();
            double v = Next();
            return mean + sd * Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
        }

        /// <summary>하위 시스템용 독립 시드</summary>
        public Rng Fork() => new Rng((uint)Math.Floor(Next() * 4294967296.0));
    }
}
