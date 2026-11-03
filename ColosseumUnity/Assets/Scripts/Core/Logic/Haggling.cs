// 흥정 (Unity판에만 있다. 웹판에는 없음): FM처럼 여러 번 제안할 수 있지만, 상대의 마지노선보다 낮게 부를 때마다
// 인내심이 깎인다. 인내심이 바닥나면 협상이 깨지고 한동안 상대하지 않는다.
// 장비 매물, 이적료, 개인 계약(이적·무소속), 식솔 고용이 모두 이걸 쓴다.
using System;
using System.Collections.Generic;

namespace Colosseum.Core
{
    /// <summary>진행 중인 흥정 하나. floor(마지노선)는 상대만 안다</summary>
    public class Haggle
    {
        public double ask, floor, patience, startPatience, lastOffer, counter, lockedUntil;
        public int tries;
    }

    /// <summary>status: accepted | countered | walked(이번에 깨짐) | locked(이미 깨져 있음) | error</summary>
    public class HaggleResult
    {
        public string status;
        public double counter;
        public string line;
        /// <summary>계약 협상의 역제안 조건 (Unity판, Contracts.Resolve)</summary>
        public ContractTerms terms;
        public bool Accepted => status == "accepted";
    }

    public static class Haggling
    {
        /// <summary>협상이 깨지면 이만큼 상대하지 않는다</summary>
        public const int LockDays = 7;

        static double Now(GameState s) => Calendar.AbsDay(s.season, s.day);

        public static Haggle Get(GameState s, string key) => s.haggles != null && s.haggles.TryGetValue(key, out var h) ? h : null;

        public static bool IsLocked(GameState s, string key) => Get(s, key) is Haggle h && h.lockedUntil > Now(s);

        public static string LockedLine(GameState s, string key) =>
            $"협상이 깨졌습니다. {Calendar.DateLabel(Calendar.FromAbs(Get(s, key).lockedUntil).day)}까지는 상대하지 않습니다";

        /// <summary>흥정을 열거나 이어 간다. 부르는 값이 바뀌었거나 깨진 기한이 지났으면 새로 연다.
        /// cutMin~cutMax: 마지노선이 부르는 값보다 얼마나 낮은가</summary>
        public static Haggle Open(GameState s, Rng rng, string key, double ask, double cutMin, double cutMax, double patience = 100)
        {
            s.haggles ??= new Dictionary<string, Haggle>();
            var h = Get(s, key);
            if (h != null && h.ask == ask && (h.lockedUntil == 0 || h.lockedUntil > Now(s))) return h;
            double cut = rng.Range(cutMin, Math.Max(cutMin, cutMax));
            h = new Haggle { ask = ask, floor = Math.Ceiling(ask * (1 - cut)), patience = patience, startPatience = patience };
            s.haggles[key] = h;
            return h;
        }

        /// <summary>제안 하나를 낸다. 마지노선 이상이면 수락, 아니면 인내심이 깎이고 역제안이 온다</summary>
        public static HaggleResult Offer(GameState s, string key, double offer)
        {
            var h = Get(s, key);
            if (h == null) return new HaggleResult { status = "error", line = "흥정 중이 아닙니다" };
            if (h.lockedUntil > Now(s)) return new HaggleResult { status = "locked", line = LockedLine(s, key) };
            if (offer >= h.floor)
            {
                s.haggles.Remove(key);
                return new HaggleResult { status = "accepted", line = offer >= h.ask ? "부른 값 그대로다. 거래하자" : "좋다, 그 값에 하지" };
            }
            // 모자란 만큼, 그리고 값을 올리지 않고 버틸수록 더 짜증 낸다
            double gap = (h.floor - offer) / h.ask;
            double hit = Math.Min(70, 8 + gap * 110) + (h.tries > 0 && offer <= h.lastOffer ? 12 : 0);
            h.patience -= hit;
            h.tries++;
            h.lastOffer = Math.Max(h.lastOffer, offer);
            if (h.patience <= 0)
            {
                h.patience = 0;
                h.lockedUntil = Now(s) + LockDays;
                return new HaggleResult { status = "walked", line = gap > 0.35 ? "사람을 무시하는군. 거래는 끝이다" : "더는 시간 낭비하지 않겠다" };
            }
            double keep = Math.Clamp(h.patience / h.startPatience, 0, 1);
            double step = h.ask >= 1000 ? 10 : 1;
            h.counter = Math.Max(h.floor, Math.Min(h.ask, Math.Round((h.floor + (h.ask - h.floor) * (0.15 + 0.55 * keep)) / step) * step));
            return new HaggleResult { status = "countered", counter = h.counter, line = $"{(gap > 0.35 ? "어이없다는 표정입니다. " : "")}{Mood(h)} \"{h.counter:N0} D라면 생각해 보지.\"" };
        }

        /// <summary>상대 기분 (인내심 비율로)</summary>
        public static string Mood(Haggle h)
        {
            double r = h.startPatience > 0 ? h.patience / h.startPatience : 0;
            return r > 0.65 ? "아직 이야기할 만하다는 눈치입니다." : r > 0.35 ? "슬슬 짜증을 냅니다." : "곧 자리를 뜰 것 같습니다.";
        }

        /// <summary>이제 쓸모없는 흥정 기록을 지운다 (끝난 매물, 기한 지난 결렬)</summary>
        public static void Forget(GameState s, string key) => s.haggles?.Remove(key);

        /// <summary>끝난 흥정 기록 정리 (하루 진행): 사라진 매물·합의, 결렬 기한이 지난 것</summary>
        public static void Prune(GameState s)
        {
            if (s.haggles == null) return;
            double now = Now(s);
            foreach (var key in new List<string>(s.haggles.Keys))
            {
                var h = s.haggles[key];
                bool gone = key.StartsWith("gear:") && (s.gearMarket == null || !s.gearMarket.Exists(l => "gear:" + l.id == key))
                    || key.StartsWith("deal:") && (s.transfer == null || !s.transfer.deals.Exists(d => "deal:" + d.id == key));
                if (gone || (h.lockedUntil > 0 && h.lockedUntil <= now)) s.haggles.Remove(key);
            }
        }
    }
}
