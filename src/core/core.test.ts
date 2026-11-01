import { describe, expect, it } from "vitest";
import { buildCommentary } from "./combat/commentary";
import { simulateMatch } from "./combat/sim";
import { generateGladiator } from "./generators/gladiator";
import { newGame } from "./generators/world";
import { computeCA } from "./gladiator";
import { josa } from "./josa";
import { Rng } from "./rng";
import { ATTRS } from "./types";

describe("Rng", () => {
  it("같은 시드는 같은 수열을 만든다", () => {
    const a = new Rng(42), b = new Rng(42);
    for (let i = 0; i < 100; i++) expect(a.next()).toBe(b.next());
  });
  it("int는 범위를 지킨다", () => {
    const r = new Rng(1);
    for (let i = 0; i < 1000; i++) {
      const v = r.int(3, 7);
      expect(v).toBeGreaterThanOrEqual(3);
      expect(v).toBeLessThanOrEqual(7);
    }
  });
});

describe("josa", () => {
  it("받침에 따라 조사를 고른다", () => {
    expect(josa("가르크", "이/가")).toBe("가르크가");
    expect(josa("마르쿠스", "을/를")).toBe("마르쿠스를");
    expect(josa("리비아", "은/는")).toBe("리비아는");
    expect(josa("두락", "이/가")).toBe("두락이");
    expect(josa("칼", "으로/로")).toBe("칼로");
    expect(josa("창", "으로/로")).toBe("창으로");
  });
});

describe("검투사 생성", () => {
  it("능력치가 1~20, PA ≥ CA", () => {
    const rng = new Rng(7);
    for (let i = 0; i < 200; i++) {
      const g = generateGladiator(rng, { id: `g${i}`, nation: "aurelia", target: rng.range(4, 15) });
      for (const a of ATTRS) {
        expect(g.attrs[a]).toBeGreaterThanOrEqual(1);
        expect(g.attrs[a]).toBeLessThanOrEqual(20);
      }
      expect(g.hidden.pa).toBeGreaterThanOrEqual(computeCA(g));
      expect(g.wage).toBeGreaterThan(0);
      if (g.race === "minotaur") expect(g.equipment.armor).not.toBe("loincloth");
    }
  });
});

describe("새 게임", () => {
  it("1부 10팀 + 2부 10팀, 플레이어는 2부", () => {
    const s = newGame(123);
    const ludi = Object.values(s.ludi).filter((l) => l.nation === s.nation);
    expect(ludi.filter((l) => l.tier === 1)).toHaveLength(10);
    // 다른 다섯 나라는 1부만 10팀씩
    expect(Object.values(s.ludi).filter((l) => l.nation !== s.nation && l.tier === 1)).toHaveLength(50);
    expect(ludi.filter((l) => l.tier === 2)).toHaveLength(10);
    expect(s.ludi[s.playerLudusId].tier).toBe(2);
    expect(s.ludi[s.playerLudusId].gladiators).toHaveLength(6);
  });
  it("검투사 이름이 겹치지 않는다", () => {
    const s = newGame(321);
    const names = Object.values(s.gladiators).map((g) => g.name);
    expect(new Set(names).size).toBe(names.length);
  });
  it("같은 시드는 같은 세계를 만든다", () => {
    expect(JSON.stringify(newGame(9))).toBe(JSON.stringify(newGame(9)));
  });
});

describe("전투 시뮬레이션", () => {
  const rng = new Rng(99);
  const pool = Array.from({ length: 60 }, (_, i) =>
    generateGladiator(rng, { id: `p${i}`, nation: "aurelia", target: 5 + (i % 10) }),
  );

  it("항상 승자가 정해지고 결정론적이다", () => {
    for (let i = 0; i < 50; i++) {
      const a = pool[i % pool.length], b = pool[(i * 7 + 3) % pool.length];
      if (a === b) continue;
      const r1 = simulateMatch(a, b, { rule: "missio", seed: i });
      const r2 = simulateMatch(a, b, { rule: "missio", seed: i });
      expect([a.id, b.id]).toContain(r1.winnerId);
      expect(r1.winnerId).not.toBe(r1.loserId);
      expect(JSON.stringify(r1)).toBe(JSON.stringify(r2));
      expect(r1.events.at(-1)?.t).toBe("end");
    }
  });

  it("CA가 높은 쪽이 대체로 이긴다", () => {
    const strong = generateGladiator(new Rng(5), { id: "s", nation: "aurelia", target: 15, style: "murmillo" });
    const weak = generateGladiator(new Rng(6), { id: "w", nation: "aurelia", target: 6, style: "murmillo" });
    let wins = 0;
    for (let i = 0; i < 200; i++) if (simulateMatch(strong, weak, { rule: "training", seed: i }).winnerId === "s") wins++;
    expect(wins).toBeGreaterThan(150);
  });

  it("사투는 패자가 대부분 죽고, 훈련전은 거의 죽지 않는다", () => {
    let suddenDeaths = 0, trainingDeaths = 0, suddenKo = 0;
    for (let i = 0; i < 300; i++) {
      const a = pool[i % 30], b = pool[30 + (i % 30)];
      const s = simulateMatch(a, b, { rule: "sudden", seed: i });
      if (s.reason !== "timeout") suddenKo++;
      if (s.died) suddenDeaths++;
      if (simulateMatch(a, b, { rule: "training", seed: i }).died) trainingDeaths++;
    }
    expect(suddenDeaths / Math.max(1, suddenKo)).toBeGreaterThan(0.8);
    expect(trainingDeaths).toBeLessThan(15);
  });

  it("중계 문장을 만든다", () => {
    const r = simulateMatch(pool[0], pool[1], { rule: "missio", seed: 3 });
    const lines = buildCommentary(r.events, { [pool[0].id]: pool[0], [pool[1].id]: pool[1] }, 3);
    expect(lines.length).toBeGreaterThan(5);
    expect(lines.at(-1)?.text).toContain("승리");
    for (const l of lines) expect(l.text).not.toContain("undefined");
  });
});

describe("초상화 성별 규칙", () => {
  it("여성에게는 수염이 붙지 않고, 남성도 수염이 없을 수 있다", async () => {
    const { generateGladiator } = await import("./generators/gladiator");
    const { makeAvatar } = await import("./avatar");
    const { paintedSpec } = await import("./portrait2");
    const { Rng } = await import("./rng");
    const rng = new Rng(99);
    let menWithout = 0;
    for (let i = 0; i < 400; i++) {
      const g = generateGladiator(rng, { id: `g${i}`, nation: "aurelia", target: 9 });
      const svg = makeAvatar(g);
      const painted = paintedSpec(g);
      if (g.gender === "f") {
        expect(svg.beard).toBeNull();
        expect(painted?.beard ?? null).toBeNull();
      } else if (!svg.beard && !(painted?.beard)) menWithout++;
    }
    expect(menWithout).toBeGreaterThan(0);
  });
});
