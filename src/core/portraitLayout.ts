// 초상화 파츠 배치 규칙. 웹(SVG)과 미리보기 도구, Unity가 같은 규칙을 쓴다.
// 좌표는 0~1 비율 (초상화 정사각형 기준). 파츠 이미지는 투명 여백을 잘라낸 크기(trim)를 기준으로 맞춘다.

export type PartSlot = "body" | "head" | "eyes" | "nose" | "mouth" | "beard" | "hair" | "helmet";

/** 아래에서 위로 그리는 순서 */
export const SLOT_ORDER: PartSlot[] = ["body", "head", "nose", "beard", "mouth", "eyes", "hair", "helmet"];

export interface PartMeta {
  /** 잘라낸 그림의 크기 (원본 픽셀) */
  w: number;
  h: number;
  /** 파츠별 크기 보정 (좁은 모히칸, 작은 염소수염 등) */
  fit?: number;
}

export interface Box {
  x: number;
  y: number;
  w: number;
  h: number;
}

/** 종족별 얼굴 비율 보정 (뿔·갈기가 있는 머리는 얼굴이 머리 그림보다 좁다) */
export interface RaceFace {
  headHeight: number;
  faceWidth: number;
  eyeY: number;
  mouthY: number;
}

export const DEFAULT_FACE: RaceFace = { headHeight: 0.56, faceWidth: 0.6, eyeY: 0.46, mouthY: 0.7 };

/** 종족별 얼굴 위치 (머리 그림 안에서의 비율). 귀·뿔·갈기가 넓을수록 faceWidth가 작다 */
export const RACE_FACE: Record<string, RaceFace> = {
  human: DEFAULT_FACE,
  elf: { headHeight: 0.56, faceWidth: 0.42, eyeY: 0.48, mouthY: 0.72 },
  orc: { headHeight: 0.56, faceWidth: 0.55, eyeY: 0.43, mouthY: 0.68 },
  dwarf: { headHeight: 0.54, faceWidth: 0.62, eyeY: 0.42, mouthY: 0.64 },
  minotaur: { headHeight: 0.62, faceWidth: 0.36, eyeY: 0.46, mouthY: 0.8 },
  lizardfolk: { headHeight: 0.58, faceWidth: 0.46, eyeY: 0.4, mouthY: 0.75 },
  beastkin: { headHeight: 0.6, faceWidth: 0.42, eyeY: 0.47, mouthY: 0.75 },
};

/** 머리 그림을 기준으로 각 파츠 위치를 계산한다 */
export function layoutPortrait(parts: Partial<Record<PartSlot, PartMeta>>, face: RaceFace = DEFAULT_FACE): Partial<Record<PartSlot, Box>> {
  const out: Partial<Record<PartSlot, Box>> = {};
  const fit = (m: PartMeta, w: number) => ({ w, h: (m.h / m.w) * w });

  const body = parts.body;
  let bodyTop = 0.62;
  if (body) {
    const s = fit(body, 0.9);
    out.body = { x: 0.5 - s.w / 2, y: 1 - s.h, ...s };
    bodyTop = 1 - s.h;
  }

  const head = parts.head;
  if (!head) return out;
  const hh = face.headHeight;
  const hw = (head.w / head.h) * hh;
  // 목 아랫부분이 몸통 위에 살짝 겹치게
  const headY = Math.max(-0.04, bodyTop + 0.14 - hh);
  const hb: Box = { x: 0.5 - hw / 2, y: headY, w: hw, h: hh };
  out.head = hb;

  const faceW = hw * face.faceWidth;
  const place = (slot: PartSlot, widthRatio: number, centerY: number | null, topY?: number) => {
    const m = parts[slot];
    if (!m) return;
    const s = fit(m, faceW * widthRatio * (m.fit ?? 1));
    out[slot] = { x: 0.5 - s.w / 2, y: centerY !== null ? centerY - s.h / 2 : topY!, ...s };
  };
  place("eyes", 1.0, hb.y + hb.h * face.eyeY);
  place("nose", 0.3, hb.y + hb.h * (face.eyeY + (face.mouthY - face.eyeY) * 0.52));
  place("mouth", 0.5, hb.y + hb.h * face.mouthY);
  place("beard", 1.05, null, hb.y + hb.h * (face.mouthY - 0.08));
  place("hair", 1.75, null, hb.y - hb.h * 0.06);
  place("helmet", 1.85, null, hb.y - hb.h * 0.1);
  return out;
}
