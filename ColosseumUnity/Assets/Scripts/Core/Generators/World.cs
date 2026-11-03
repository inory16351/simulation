// 새 게임 세계 생성 (src/core/generators/world.ts). 난수 소비 순서를 TS와 똑같이 맞춘다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class World
    {
        /// <summary>시작 국가 계수 (판테온 컵 성적 3시즌치를 가정)</summary>
        public static readonly Dictionary<string, double> InitialCoefficient = new()
        {
            ["aurelia"] = 11, ["grommar"] = 9.5, ["dolgar"] = 9, ["karsea"] = 8, ["kemer"] = 7, ["sylvaren"] = 6,
        };

        static readonly string[] Personalities = { "aggressive", "stable", "grudge", "merchant", "developer" };

        public static GameState NewGame(double seed, string nation = "aurelia", string ludusName = "모래의 집")
        {
            var rng = new Rng(seed);
            var state = new GameState
            {
                version = 9,
                seed = seed,
                rngState = 0,
                season = 1,
                day = 1,
                nation = nation,
                playerLudusId = "",
                ludi = new Dictionary<string, Ludus>(),
                gladiators = new Dictionary<string, Gladiator>(),
                inbox = new List<InboxMessage>(),
                fallen = new List<string>(),
                challenges = new List<Challenge>(),
                staff = new Dictionary<string, Staff>(),
                relations = new Dictionary<string, Dictionary<string, double>>(),
                wars = new List<War>(),
                contracts = new List<Contract>(),
                staffMarket = new List<string>(),
                freeAgents = new List<string>(),
                market = new List<MarketListing>(),
                ledger = new List<LedgerEntry>(),
                pendingEvents = new List<PendingEvent>(),
                lastDisasterAt = -999,
                luckStreak = 0,
                modifiers = new List<Modifier>(),
                coefficients = new Dictionary<string, List<double>>(),
                nextId = 1,
            };
            foreach (var n in Data.Nations) state.relations[n.id] = new Dictionary<string, double>(n.relations);

            string NewId(string prefix) => $"{prefix}{state.nextId++}";
            var usedNames = new HashSet<string> { ludusName };
            var usedGladiatorNames = new HashSet<string>();
            var L = Data.Balance["league"];
            var E = Bal.Economy;
            int teamsPerTier = (int)L["teamsPerTier"];
            double rpTier1 = (double)L["rpTier1"], rpTier2 = (double)L["rpTier2"];
            int aiRoster = (int)E["aiRoster"], startRoster = (int)E["startRoster"];
            double startTreasury = (double)E["startTreasury"];

            Ludus AddLudus(int tier, bool isPlayer, string name, double rp, int rosterSize, double target, string ludusNation = null)
            {
                ludusNation ??= nation;
                var ludus = new Ludus();
                ludus.id = NewId("L");
                ludus.name = name;
                ludus.nation = ludusNation;
                ludus.tier = tier;
                ludus.rp = JsMath.Round(rp);
                ludus.treasury = isPlayer ? startTreasury : rng.Int(3000, tier == 1 ? 15000 : 7000);
                ludus.gladiators = new List<string>();
                ludus.isPlayer = isPlayer;
                ludus.aiPersonality = isPlayer ? null : rng.Pick(Personalities);
                ludus.record = new WinLoss { wins = 0, losses = 0 };
                ludus.matchesThisSeason = 0;
                ludus.refusalsUsed = 0;
                ludus.facilities = new Facilities { barracks = 1, training = 1, medical = 1, bath = 0, arena = 1, guard = 0, academy = 0 };
                ludus.staff = new List<string>();
                ludus.youth = new List<string>();
                for (int i = 0; i < rosterSize; i++)
                {
                    var opt = new GenOptions();
                    opt.id = NewId("G");
                    opt.nation = ludusNation;
                    opt.target = target + rng.Range(-1.5, 1.5);
                    opt.usedNames = usedGladiatorNames;
                    // 플레이어 시작 로스터는 노예 위주
                    opt.status = isPlayer ? (rng.Chance(0.85) ? "slave" : "free") : null;
                    var g = GladiatorGen.Generate(rng, opt);
                    state.gladiators[g.id] = g;
                    ludus.gladiators.Add(g.id);
                }
                state.ludi[ludus.id] = ludus;
                return ludus;
            }

            string LudusNameGen()
            {
                for (;;)
                {
                    // "붉은 늑대 투기단" 또는 "늑대의 집" 꼴
                    string n;
                    if (rng.Chance(0.35))
                    {
                        string adj = rng.Pick(Data.Names.ludusAdj);
                        string noun = rng.Pick(Data.Names.ludusNoun);
                        string suf = rng.Pick(Data.Names.ludusSuffixAfterAdj);
                        n = $"{adj} {noun}{suf}";
                    }
                    else
                    {
                        string noun = rng.Pick(Data.Names.ludusNoun);
                        string suf = rng.Pick(Data.Names.ludusSuffix);
                        n = noun + suf;
                    }
                    if (!usedNames.Contains(n))
                    {
                        usedNames.Add(n);
                        return n;
                    }
                }
            }

            for (int i = 0; i < teamsPerTier; i++)
            {
                string name = LudusNameGen();
                double rp = rpTier1 + rng.Normal(0, 60);
                AddLudus(1, false, name, rp, aiRoster, 11 - i * 0.15);
            }
            for (int i = 0; i < teamsPerTier - 1; i++)
            {
                string name = LudusNameGen();
                double rp = rpTier2 + rng.Normal(0, 50);
                AddLudus(2, false, name, rp, aiRoster, 8 - i * 0.15);
            }
            var player = AddLudus(2, true, ludusName, rpTier2 - 60, startRoster, 6.5);
            state.playerLudusId = player.id;

            // 다른 나라: 1부 리그만 실제로 돌린다 (판테온 컵 상대). 국가 계수가 높은 나라일수록 조금 더 강하다
            foreach (var other in Data.Nations)
            {
                if (other.id == nation) continue;
                double strength = (InitialCoefficient[other.id] - 8) * 0.12;
                for (int i = 0; i < teamsPerTier; i++)
                {
                    string name = LudusNameGen();
                    double rp = rpTier1 + rng.Normal(0, 60);
                    AddLudus(1, false, name, rp, aiRoster, 11 + strength - i * 0.15, other.id);
                }
            }
            state.coefficients = new Dictionary<string, List<double>>();
            foreach (var n in Data.Nations)
            {
                double c = InitialCoefficient[n.id];
                state.coefficients[n.id] = new List<double> { c, c, c };
            }

            var nationDef = Data.Nation(nation);
            state.inbox.Add(new InboxMessage
            {
                id = NewId("M"),
                season = 1,
                day = 1,
                kind = "info",
                title = $"{nationDef.league} 2부 입성",
                body =
                    $"라니스타, {ludusName}의 문이 열렸습니다.\n\n" +
                    $"금고엔 {StateUtil.Locale(startTreasury)} 데나리, 막사엔 검투사 {startRoster}명이 있습니다. " +
                    "다들 급료 날만 기다립니다. 돈줄이 끊기면 저 칼끝이 누구를 향할지 잊어서는 안 됩니다.\n\n" +
                    "이번 시즌 목표: 2부 리그 2위 안에 들어 승격.\n\n" +
                    $"{nationDef.name}의 {nationDef.league} 특징:\n{string.Join("\n", nationDef.ruleText.Select(t => $"· {t}"))}",
                read = false,
            });

            // 시즌 시작: 배당금, 무소속 명단, 일대기 첫 줄
            double promoted = (double)E["promotedDividendCoef"];
            foreach (var l in state.ludi.Values.ToList()) Economy.PayDividend(state, l, l.isPlayer ? promoted : (double?)null);
            StaffLogic.RefreshStaffMarket(state, rng);
            Economy.RefreshFreeAgents(state, rng);
            GearLogic.RefreshGearMarket(state, rng, true);
            StaffLogic.StartingStaff(state, rng, state.ludi[state.playerLudusId]);
            foreach (var l in state.ludi.Values.ToList())
            {
                foreach (var id in l.gladiators)
                {
                    var g = state.gladiators[id];
                    Career.SeasonRecord(state, g);
                    Career.LogCareer(state, g, $"{l.name}에 들어왔습니다", "join");
                }
            }

            state.rngState = rng.State;
            return state;
        }
    }
}
