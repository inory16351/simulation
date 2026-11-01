// 메뉴 아이콘과 국가 문장. 초상화와 같은 플랫 + 잉크 외곽선 화풍으로 코드로 그린다.
import type { ReactElement } from "react";
import type { NationId } from "../../core/types";

const INK = "#1a120e";
const ink = { stroke: INK, strokeWidth: 1.6, strokeLinejoin: "round" as const, strokeLinecap: "round" as const };
const BRONZE = "#c9a45a";
const BRONZE_D = "#8a6a3a";
const IRON = "#9aa0a6";
const WOOD = "#8a5a2b";
const PARCH = "#e8dcc0";
const RED = "#b3261c";

const MENU: Record<string, ReactElement> = {
  // 서판: 밀랍 서판과 철필
  inbox: <g {...ink}><rect x="4" y="6" width="16" height="13" rx="2" fill={WOOD} /><rect x="6.5" y="8.5" width="11" height="8" rx="1" fill={PARCH} /><path d="M8 11 H15 M8 13.5 H13" strokeWidth={1.1} /><path d="M17 3 L21 7" stroke={BRONZE_D} strokeWidth={2.2} /></g>,
  // 안뜰: 아치 문
  overview: <g {...ink}><path d="M4 20 V10 C4 5 8 3 12 3 C16 3 20 5 20 10 V20 Z" fill="#b8a58a" /><path d="M8 20 V12 C8 9.5 10 8 12 8 C14 8 16 9.5 16 12 V20 Z" fill="#3a2c22" /></g>,
  // 막사: 볏 투구
  roster: <g {...ink}><path d="M6 7 C9 2 15 2 18 7 C15 5 9 5 6 7 Z" fill={RED} /><path d="M5 15 C5 8 8 5 12 5 C16 5 19 8 19 15 L16 15 L16 19 L8 19 L8 15 Z" fill={BRONZE} /><path d="M10 11 H14 V16 H10 Z" fill="#3a2c22" strokeWidth={1.1} /></g>,
  // 연무장: 나무 허수아비
  training: <g {...ink}><circle cx="12" cy="6" r="3" fill="#c49a6c" /><path d="M5 11 H19 M12 9 V21 M8 21 H16" stroke={WOOD} strokeWidth={2.6} /><path d="M5 11 H19 M12 9 V21 M8 21 H16" strokeWidth={0.8} /></g>,
  // 도전장: 단검이 꽂힌 두루마리
  challenge: <g {...ink}><rect x="4" y="7" width="14" height="12" rx="2" fill={PARCH} /><path d="M7 11 H14 M7 14 H12" strokeWidth={1.1} /><path d="M20 3 L11 14" stroke={IRON} strokeWidth={2.4} /><path d="M17 5 L19 7" stroke={BRONZE_D} strokeWidth={2.4} /></g>,
  // 모래 마당: 엇갈린 검 두 자루
  match: <g {...ink}><path d="M5 5 L18 18 M19 5 L6 18" stroke={IRON} strokeWidth={2.6} /><path d="M5 5 L18 18 M19 5 L6 18" strokeWidth={0.8} /><path d="M14 18 L18 14 M10 18 L6 14" stroke={BRONZE_D} strokeWidth={2.4} /></g>,
  // 서열표: 시상대
  league: <g {...ink}><rect x="9" y="8" width="6" height="13" fill={BRONZE} /><rect x="3" y="12" width="6" height="9" fill={IRON} /><rect x="15" y="15" width="6" height="6" fill="#a8703a" /><path d="M12 3 L13 5.5 L15.5 5.7 L13.6 7.2 L14.3 9.5 L12 8.2 L9.7 9.5 L10.4 7.2 L8.5 5.7 L11 5.5 Z" fill="#e8b84a" strokeWidth={1} /></g>,
  // 노예 경매: 족쇄와 돈주머니
  market: <g {...ink}><circle cx="8" cy="15" r="4" fill="none" stroke={IRON} strokeWidth={2.4} /><path d="M11 13 L13 11" stroke={IRON} strokeWidth={2} /><path d="M13 9 C13 6 21 6 21 9 L22 18 C22 21 12 21 12 18 Z" fill={BRONZE} /><path d="M15 7 L19 7" strokeWidth={1.2} /></g>,
  // 유소년 학당: 목검을 든 아이
  youth: <g {...ink}><circle cx="10" cy="7" r="3.6" fill="#e2b28c" /><path d="M6 20 C6 14 8 12 10 12 C12 12 14 14 14 20 Z" fill="#7a1e1a" /><path d="M15 18 L20 6" stroke={WOOD} strokeWidth={2.4} /><path d="M14.5 15 L17.5 16" stroke={WOOD} strokeWidth={1.8} /></g>,
  // 식솔: 열쇠
  staff: <g {...ink}><circle cx="8" cy="8" r="4.5" fill={BRONZE} /><circle cx="8" cy="8" r="1.6" fill="#3a2c22" /><path d="M11 11 L20 20 M16 16 L18 14 M18.5 18.5 L20.5 16.5" stroke={BRONZE_D} strokeWidth={2.4} /></g>,
  // 증축: 망치와 돌기둥
  facilities: <g {...ink}><rect x="4" y="9" width="7" height="11" fill="#b8a58a" /><path d="M3 9 H12 M3 20 H12" /><path d="M14 4 L20 10" stroke={WOOD} strokeWidth={2.4} /><rect x="15" y="3" width="7" height="4" rx="1" fill={IRON} transform="rotate(45 18.5 5)" /></g>,
  // 금고: 상자
  finance: <g {...ink}><rect x="4" y="10" width="16" height="10" rx="1.5" fill={WOOD} /><path d="M4 10 C4 5 20 5 20 10 Z" fill="#a8703a" /><rect x="10.5" y="11.5" width="3" height="4" rx="0.8" fill={BRONZE} /></g>,
  // 판테온 컵: 손잡이 둘 달린 잔
  cup: <g {...ink}><path d="M7 4 H17 V9 C17 13 14.5 15 12 15 C9.5 15 7 13 7 9 Z" fill={BRONZE} /><path d="M7 6 C3 6 3 11 7.5 11.5 M17 6 C21 6 21 11 16.5 11.5" fill="none" strokeWidth={1.6} /><path d="M12 15 V18 M8 20 H16 L15 18 H9 Z" fill={BRONZE} /></g>,
  // 대륙 정세: 지도
  world: <g {...ink}><path d="M3 6 L9 4 L15 6 L21 4 V18 L15 20 L9 18 L3 20 Z" fill={PARCH} /><path d="M9 4 V18 M15 6 V20" strokeWidth={1} /><circle cx="12" cy="11" r="2" fill={RED} strokeWidth={1} /></g>,
  // 비석의 언덕: 비석
  hall: <g {...ink}><path d="M6 21 V9 C6 5 9 3 12 3 C15 3 18 5 18 9 V21 Z" fill="#9a958c" /><path d="M10 10 H14 M12 8 V14" strokeWidth={1.4} /><path d="M3 21 H21" /></g>,
};

export function MenuIcon({ id, size = 22 }: { id: string; size?: number }) {
  return (
    <svg className="menu-icon" width={size} height={size} viewBox="0 0 24 24">
      {MENU[id] ?? <circle cx="12" cy="12" r="6" fill={BRONZE} {...ink} />}
    </svg>
  );
}

/** 나라별 문장: 방패 바탕색 + 상징 하나 */
const EMBLEM: Record<NationId, ReactElement> = {
  // 아우렐리아(로마): 독수리
  aurelia: <g {...ink} fill={BRONZE}><path d="M20 13 C17 13 16 16 16 18 L9 15 L13 21 L10 24 L16 23 L17 28 L20 25 L23 28 L24 23 L30 24 L27 21 L31 15 L24 18 C24 16 23 13 20 13 Z" /><circle cx="20" cy="12" r="2.6" /></g>,
  // 라케다이몬(스파르타): 람다 방패
  dolgar: <g {...ink}><circle cx="20" cy="20" r="9" fill={BRONZE} /><path d="M15.5 25 L20 14 L24.5 25" fill="none" stroke={RED} strokeWidth={2.6} /></g>,
  // 카르세아(카르타고): 닻과 바다뱀
  karsea: <g {...ink}><path d="M20 11 V28 M15 15 H25 M13 23 C14 28 26 28 27 23" fill="none" stroke={BRONZE} strokeWidth={2.6} /><circle cx="20" cy="10" r="2" fill={BRONZE} /></g>,
  // 케메르(이집트): 태양 원반
  kemer: <g {...ink}><circle cx="20" cy="19" r="6" fill="#e8b84a" />{[0, 45, 90, 135, 180, 225, 270, 315].map((a) => <path key={a} d="M20 9 L20 6" transform={`rotate(${a} 20 19)`} stroke="#e8b84a" strokeWidth={2.2} />)}<path d="M14 28 H26" stroke="#2c3e73" strokeWidth={2.4} /></g>,
  // 그롬마르(게르만·노르드): 뿔 해골과 도끼
  grommar: <g {...ink}><path d="M12 12 C9 10 9 6 11 4 C11 8 14 10 16 11 Z M28 12 C31 10 31 6 29 4 C29 8 26 10 24 11 Z" fill={PARCH} /><path d="M14 16 C14 10 26 10 26 16 C26 20 24 22 22 23 V26 H18 V23 C16 22 14 20 14 16 Z" fill={PARCH} /><circle cx="17.5" cy="17" r="1.8" fill={INK} /><circle cx="22.5" cy="17" r="1.8" fill={INK} /></g>,
  // 실바렌(켈트): 고목
  sylvaren: <g {...ink}><path d="M19 28 V18 M21 28 V18" stroke={WOOD} strokeWidth={2.2} /><circle cx="20" cy="14" r="7" fill="#7a9a4a" /><circle cx="14" cy="17" r="4" fill="#7a9a4a" /><circle cx="26" cy="17" r="4" fill="#7a9a4a" /><path d="M14 28 C17 26 23 26 26 28" fill="none" stroke={WOOD} strokeWidth={1.8} /></g>,
};

export function Crest({ nation, color, size = 32 }: { nation: NationId; color: string; size?: number }) {
  return (
    <svg className="crest" width={size} height={size} viewBox="0 0 40 40">
      <path d="M20 2 L36 7 V19 C36 29 28 35 20 38 C12 35 4 29 4 19 V7 Z" fill={color} {...ink} strokeWidth={2} />
      <path d="M20 5 L33 9 V19 C33 27 27 32 20 35 C13 32 7 27 7 19 V9 Z" fill="none" stroke={BRONZE} strokeWidth={1.2} opacity="0.7" />
      {EMBLEM[nation]}
    </svg>
  );
}
