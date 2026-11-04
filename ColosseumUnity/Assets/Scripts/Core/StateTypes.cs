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
        /// <summary>Unity판: 공식 경기 평점 합과 평점 받은 경기 수 (평균 = ratingSum / rated)</summary>
        public double? ratingSum, rated;
        /// <summary>Unity판: 이 줄의 루두스 id (이적할 때마다 줄을 나눈다)</summary>
        public string ludusId;
        /// <summary>Unity판: 이 루두스로 올 때 낸 이적료. 0이면 자유 이적, null이면 표시 없음</summary>
        public double? fee;
        /// <summary>Unity판: 이적료 대신 보여 줄 말 (학당 출신 등)</summary>
        public string via;
    }

    public class CareerEvent
    {
        public int season, day;
        public string text;
        /// <summary>join | match | milestone | injury | death | other</summary>
        public string kind;
    }

    public class DiedAt { public int season, day; public string cause; }
    public class GladiatorContract
    {
        public int untilSeason;
        public List<string> clauses = new();
        /// <summary>Unity판 전용 계약 조항 (Logic/Contracts.cs). 없으면 null</summary>
        public double? winBonus, killBonus, promoBonus, promoRaise, relegCut, releaseClause;
        public bool? promoExtend;
    }
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
        /// <summary>Unity판: 공식 경기에 나갈 등록 명단 (최대 15, Logic/Registration.cs)과 이미 등록 여부를 따져 본 검투사</summary>
        public List<string> registered, regSeen;
        public bool isPlayer;
        /// <summary>aggressive | stable | grudge | merchant | developer</summary>
        public string aiPersonality;
        public WinLoss record = new();
        public double matchesThisSeason, refusalsUsed;
        public Facilities facilities = new();
        public Construction construction;
        public List<string> staff = new();
        public List<string> youth = new();
        /// <summary>Unity판: 유소년 입단 테스트 후보 (계약을 골라 맺는다, Logic/Youth.cs)와 마감일(절대 일수)</summary>
        public List<string> youthTrial;
        public double? youthTrialUntil;
        /// <summary>Unity판: 다음 입단 예고에서 정한 후보 수준 (0 평범 · 1 괜찮음 · 2 뛰어남 · 3 황금 세대)</summary>
        public int? youthOutlook;
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
        /// <summary>league | cup | natcup | olympica (올림피카는 winnerId가 나라 id)</summary>
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
        /// <summary>Unity판 전용 세부 능력치 1~20 (StaffLogic.AttrKeys). 없으면 rating 하나로 본다</summary>
        public Dictionary<string, int> attrs;
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
        /// <summary>Unity판: 우리가 일정 화면에서 고른 경기 날 (절대 일수). 받으면 이 날로 잡힌다</summary>
        public double? wantAt;
        /// <summary>Unity판: 휴식기 연습전 (훈련전 규칙, RP·상금·리그 경기 수 없음)</summary>
        public bool? friendly;
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
        /// <summary>Unity판: 다시 볼 수 있는 전투 링크 (fight:원정:층 순서). 없으면 null</summary>
        public string replay;
    }

    public class ScoutingState
    {
        public Dictionary<string, double> knowledge = new();
        public List<string> assignments = new();
    }

    public class Deal { public string id, gladiatorId, fromLudus; public double fee, expiresAt; }

    public class Offer
    {
        public string id, fromLudus, gladiatorId;
        public double fee, expiresAt;
        /// <summary>Unity판 전용: null/open = 우리 답을 기다림(결정 전엔 다음 날로 못 감), waiting = 우리 역제안에 상대 답을 기다림</summary>
        public string status;
        /// <summary>우리가 부른 값 (역제안)</summary>
        public double? ask;
        /// <summary>상대가 낼 수 있는 최대 (상대만 앎)</summary>
        public double? maxFee;
        public double? replyAt;
        public int? round;
        /// <summary>최소 이적료 조항을 건드린 제안 (거절할 수 없다)</summary>
        public bool? clause;
    }

    public class TransferState { public List<Deal> deals = new(); public List<Offer> offers = new(); }

    /// <summary>Unity판 전용: 우리가 정해야 다음 날로 갈 수 있는 협상 답장 (상대의 역제안).
    /// kind: fee(이적료 역제안) | terms(이적 개인 계약) | free(무소속 계약) | renew(재계약)</summary>
    public class Decision
    {
        public string id, kind, gladiatorId, dealId, line;
        public double counterFee, at;
        public ContractTerms counterTerms;
        /// <summary>우리가 낸 마지막 제안 (협상 창이 보여 준다)</summary>
        public ContractTerms lastTerms;
        public double lastFee;
    }

    public class CupTie
    {
        public string id, stage, home, away, winner;
        public int day;
        public int? group;
        public double[] score;
        /// <summary>Unity판: 한쪽이 나오지 않아 끝난 경기</summary>
        public bool? forfeit;
    }

    /// <summary>국내 컵 (Unity판, Logic/NatCupLogic.cs). stage: r40 | r20 | r10 | league5</summary>
    public class NatCup
    {
        public int season;
        public string nation, champion, runnerUp;
        public List<string> teams = new();
        public List<string> final5;
        public List<CupTie> ties = new();
    }

    /// <summary>올림피카 컵 (Unity판, Logic/OlympicaLogic.cs). 경기의 home·away·winner는 나라 id</summary>
    public class Olympica
    {
        public int season;
        public string champion, runnerUp;
        public Dictionary<string, List<string>> squads = new();
        public List<CupTie> ties = new();
        /// <summary>우리에게 온 감독 제안에 아직 답하지 않았다</summary>
        public bool? offer;
        public bool? playerManager;
    }

    public class Cup
    {
        /// <summary>Unity판 FM식 대회 (플레이오프 → 16강 조별 → 8강). stage: po | group | qf | sf | final</summary>
        public bool? fm;
        /// <summary>플레이오프 두 조 (FM식)</summary>
        public List<List<string>> poGroups;
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
        /// <summary>Unity판: 층마다 치른 전투 기록 (전투 화면으로 다시 보기)</summary>
        public List<FloorBattle> battles;
    }

    /// <summary>던전 한 층의 전투 기록: 그때의 양쪽 모습과 대결 순서. 시드로 같은 싸움을 다시 돌려 보여 준다</summary>
    public class FloorBattle
    {
        public int floor;
        public bool boss, won;
        public List<Gladiator> party = new(), foes = new();
        public List<BattleBout> bouts = new();
    }

    /// <summary>진형 전투 속 1:1 대결 하나</summary>
    public class BattleBout
    {
        public string a, b;
        public uint seed;
        public double?[] startHp;
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
        /// <summary>Unity판 전용: 이번 시즌 국내 컵 (키: 나라)</summary>
        public Dictionary<string, NatCup> natCups;
        /// <summary>Unity판 전용: 올림피카 컵 (4시즌마다)</summary>
        public Olympica olympica;
        /// <summary>Unity판 전용: 결정해야 다음 날로 가는 협상 역제안 (FM처럼)</summary>
        public List<Decision> decisions;
        /// <summary>Unity판 전용: 이적 명단에 올린 우리 검투사 (id → 희망 이적료)</summary>
        public Dictionary<string, double> transferList;
        /// <summary>Unity판 전용: 시장에 내놓은 우리 장비</summary>
        public List<Consignment> consignments;
        /// <summary>Unity판 전용: 역대 우승 기록 (리그 1·2부, 판테온 컵). Logic/Honours.cs</summary>
        public List<HonourEntry> honours;
        /// <summary>Unity판 전용: 이번 시즌 대회별 개인 기록 (대회 키 → 검투사 id → 기록). Logic/Awards.cs</summary>
        public Dictionary<string, Dictionary<string, CompStat>> compStats;
        /// <summary>Unity판 전용: 역대 수상</summary>
        public List<AwardEntry> awards;
        /// <summary>Unity판 전용: 아직 띄우지 않은 시상 카드 링크 (대회가 끝날 때 awards:comp:…, 시즌 끝 awards:season:N)</summary>
        public List<string> ceremonies;
        public List<Expedition> expeditions;
        public List<DungeonQuest> dungeonQuests;
        /// <summary>Unity판: 이번 주에 열린 던전 (급료일마다 등급별로 하나씩 새로 뽑는다)</summary>
        public List<string> dungeonRotation;
        public List<LedgerEntry> ledger = new();
        public List<PendingEvent> pendingEvents = new();
        public double lastDisasterAt;
        public double luckStreak;
        public List<Modifier> modifiers = new();
        public string gameOver;
        public int nextId;
    }
}
