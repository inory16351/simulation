// 게임 상태 전체 (src/core/types.ts GameState와 각 모듈의 상태 타입). 필드 이름은 웹판 저장 JSON과 같다.
// 숫자는 JS처럼 대부분 double로 둔다 (중간에 소수가 끼어도 결과가 같게).
using System.Collections.Generic;

namespace Colosseum.Core
{
    public class SeasonRecord
    {
        public int season;
        public string ludus;
        public double tier, apps, wins, losses, kills, caStart, caEnd, popularity, injuries, earnings;
    }

    public class CareerEvent
    {
        public int season, day;
        public string text;
        /// <summary>join | match | milestone | injury | death | other</summary>
        public string kind;
    }

    public class DiedAt { public int season, day; public string cause; }
    public class GladiatorContract { public int untilSeason; public List<string> clauses = new(); }
    public class Deployment { public string contractId; public double until; /** null이면 전쟁 용병 */ public string kind; }

    public class Facilities { public int barracks, training, medical, bath, arena, guard, academy; }
    public class Construction { public string facility; public int toLevel; public double doneAt; }
    public class WinLoss { public double wins, losses; }

    public class Ludus
    {
        public string id, name, nation;
        public int tier;
        public double rp, treasury;
        public List<string> gladiators = new();
        public bool isPlayer;
        /// <summary>aggressive | stable | grudge | merchant | developer</summary>
        public string aiPersonality;
        public WinLoss record = new();
        public double matchesThisSeason, refusalsUsed;
        public Facilities facilities = new();
        public Construction construction;
        public List<string> staff = new();
        public List<string> youth = new();
        public double? titles;
        public Dictionary<string, double> favor;
        public double? bankruptSince;
        /// <summary>Unity판 전용: 시즌별 연혁 (리그·순위·전적·우승·승강)</summary>
        public List<LudusSeason> seasons;
        /// <summary>Unity판 전용: 단체 전술 기본값 ("3" = 3:3, "5" = 5:5). null이면 자동</summary>
        public Dictionary<string, TeamTactic> teamTactics;
    }

    /// <summary>우승 한 건 (FM 대회 역사)</summary>
    public class HonourEntry
    {
        public int season;
        /// <summary>league | cup</summary>
        public string kind;
        /// <summary>리그 우승이면 나라와 부</summary>
        public string nation;
        public int tier;
        public string winnerId, winnerName, runnerUpId, runnerUpName;
    }

    /// <summary>루두스의 한 시즌 (FM 클럽 연혁의 한 줄)</summary>
    public class LudusSeason
    {
        public int season, tier, rank, teams;
        public string nation;
        public double wins, losses, rp;
        /// <summary>champion | promoted | relegated | null</summary>
        public string result;
        /// <summary>그 시즌에 든 우승컵 이름들 (리그 우승, 판테온 컵)</summary>
        public List<string> trophies;
    }

    public class Staff
    {
        public string id, name, race, gender;
        public double age;
        /// <summary>doctore | medicus | scout | agent</summary>
        public string role;
        public double rating, wage;
        public int portraitSeed;
        public string formerGladiator;
    }

    public class Challenge
    {
        public string id, from, to, fromGladiator, toGladiator;
        public string rule;
        public double stake, createdAt, respondBy;
        /// <summary>pending | scheduled | declined | done | cancelled</summary>
        public string status;
        public double? matchAt;
        /// <summary>1, 3, 5</summary>
        public int? format;
        public List<string> fromLineup, toLineup;
    }

    public class LedgerEntry { public double at, amount; public string desc; }
    public class MarketListing { public string gladiatorId; public double price; }

    public class War
    {
        public string id, a, b, winner;
        public double startedAt, endsAt, front;
        public bool over;
    }

    public class Contract
    {
        public string id, warId, employer, enemy, type;
        public double slots, minCA, days, pay, risk, postedAt, expiresAt, merit;
        /// <summary>open | active | done | expired</summary>
        public string status;
        public List<string> assigned = new();
        public double? startedAt, endsAt;
    }

    public class Modifier { public string kind; public double mult, until; }
    public class PendingEvent { public string id, eventId, targetId, title, text; }

    public class InboxMessage
    {
        public string id;
        public int season, day;
        public string kind, title, body;
        public bool read;
    }

    public class ScoutingState
    {
        public Dictionary<string, double> knowledge = new();
        public List<string> assignments = new();
    }

    public class Deal { public string id, gladiatorId, fromLudus; public double fee, expiresAt; }
    public class Offer { public string id, fromLudus, gladiatorId; public double fee, expiresAt; }
    public class TransferState { public List<Deal> deals = new(); public List<Offer> offers = new(); }

    public class CupTie
    {
        public string id, stage, home, away, winner;
        public int day;
        public int? group;
        public double[] score;
    }

    public class Cup
    {
        public int season;
        public List<string> teams = new();
        public List<List<string>> groups = new();
        public List<CupTie> ties = new();
        public Dictionary<string, double> points = new();
        public string champion;
    }

    public class GearListing { public string id, item, nation, seller; public double price; }
    /// <summary>위탁 판매: viaAgent면 중개인이 팔고 수수료를 뗀다</summary>
    public class Consignment { public string id, item; public double price, listedAt; public bool viaAgent; }

    public class Expedition
    {
        public string id, dungeon, formation, status, questId;
        public List<string> party = new();
        public double startedAt, endsAt, floor, coins;
        public List<string> loot = new(), log = new();
        public Dictionary<string, double> hp = new();
    }

    public class DungeonQuest
    {
        public string id, nation, dungeon, status;
        public double hard, bounty, rp, postedAt, expiresAt;
    }

    public class GameState
    {
        public int version;
        public double seed;
        public uint rngState;
        public int season;
        /// <summary>시즌 안의 날짜 1~168</summary>
        public int day;
        public string nation, playerLudusId;
        public Dictionary<string, Ludus> ludi = new();
        public Dictionary<string, Gladiator> gladiators = new();
        public List<InboxMessage> inbox = new();
        public List<string> fallen = new();
        public List<Challenge> challenges = new();
        public Dictionary<string, Staff> staff = new();
        public Dictionary<string, Dictionary<string, double>> relations = new();
        public List<War> wars = new();
        public List<Contract> contracts = new();
        public string powPending;
        public List<string> staffMarket = new();
        public List<string> freeAgents = new();
        public ScoutingState scouting;
        public TransferState transfer;
        public List<string> shortlist;
        public Dictionary<string, List<double>> coefficients = new();
        public Cup cup;
        public List<MarketListing> market = new();
        public List<string> armory;
        public List<GearListing> gearMarket;
        /// <summary>Unity판 전용: 진행 중인 흥정 (키: gear:매물, fee:검투사, wage:…)</summary>
        public Dictionary<string, Haggle> haggles;
        /// <summary>Unity판 전용: 답을 기다리는 영입 제안 (Logic/Negotiations.cs)</summary>
        public List<PendingBid> bids;
        /// <summary>Unity판 전용: 같은 검투사를 노리는 경쟁 루두스 (키: 검투사 id)</summary>
        public Dictionary<string, RivalBid> rivals;
        /// <summary>Unity판 전용: 시장에 내놓은 우리 장비</summary>
        public List<Consignment> consignments;
        /// <summary>Unity판 전용: 역대 우승 기록 (리그 1·2부, 판테온 컵). Logic/Honours.cs</summary>
        public List<HonourEntry> honours;
        public List<Expedition> expeditions;
        public List<DungeonQuest> dungeonQuests;
        public List<LedgerEntry> ledger = new();
        public List<PendingEvent> pendingEvents = new();
        public double lastDisasterAt;
        public double luckStreak;
        public List<Modifier> modifiers = new();
        public string gameOver;
        public int nextId;
    }
}
