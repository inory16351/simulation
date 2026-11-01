// 일 단위 달력. 1시즌 = 6달 × 28일 = 168일 (docs/00_overview.md)
export const DAYS_PER_MONTH = 28;
export const MONTHS = ["서리달", "씨앗달", "꽃달", "불꽃달", "수확달", "그림자달"] as const;
export const DAYS_PER_SEASON = DAYS_PER_MONTH * MONTHS.length;
export const PAYDAY_INTERVAL = 7;

export function monthOf(day: number): string {
  return MONTHS[Math.floor((day - 1) / DAYS_PER_MONTH)];
}

export function dayOfMonth(day: number): number {
  return ((day - 1) % DAYS_PER_MONTH) + 1;
}

/** "서리달 3일" */
export function dateLabel(day: number): string {
  return `${monthOf(day)} ${dayOfMonth(day)}일`;
}

/** 시즌을 넘어 이어지는 절대 일수 */
export function absDay(season: number, day: number): number {
  return (season - 1) * DAYS_PER_SEASON + day;
}

export function fromAbs(abs: number): { season: number; day: number } {
  return { season: Math.floor((abs - 1) / DAYS_PER_SEASON) + 1, day: ((abs - 1) % DAYS_PER_SEASON) + 1 };
}

export const isPayday = (day: number) => day % PAYDAY_INTERVAL === 0;

/** 다음 급료일까지 남은 날 (오늘이 급료일이면 0) */
export function daysToPayday(day: number): number {
  const r = day % PAYDAY_INTERVAL;
  return r === 0 ? 0 : PAYDAY_INTERVAL - r;
}
