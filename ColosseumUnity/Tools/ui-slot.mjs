// 아이템 슬롯 틀 그림 (Unity판): 초상화 그림체에 맞춘 붓 느낌 청동 테두리 + 어두운 홈.
//   item_slot.png      틀 (먹선 + 청동 테두리 두 톤 + 어두운 안쪽, 왼쪽 위 횃불빛)
//   item_slot_rim.png  등급 색을 입힐 흰 안쪽 테두리 (Image.color로 칠한다)
// 사용 (저장소 루트에서): node ColosseumUnity/Tools/ui-slot.mjs
import sharp from "sharp";

const OUT = "ColosseumUnity/Assets/Resources/Art/ui";
const N = 128;

// 결정적 잡음 (붓결): 같은 그림이 나오게 난수 대신 해시
const hash = (x, y) => {
  let h = (x * 374761393 + y * 668265263) | 0;
  h = (h ^ (h >>> 13)) * 1274126177;
  return ((h ^ (h >>> 16)) >>> 0) / 4294967295;
};
// 가로 붓결: 몇 픽셀씩 같은 값이 이어지게
const stroke = (x, y) => hash(Math.floor(x / 7), y) * 0.6 + hash(x, Math.floor(y / 3)) * 0.4;

// 모서리가 둥근 사각형까지의 거리 (안쪽 음수)
function sdBox(x, y, half, r) {
  const qx = Math.abs(x) - half + r, qy = Math.abs(y) - half + r;
  return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0) - r;
}

const frame = Buffer.alloc(N * N * 4), rim = Buffer.alloc(N * N * 4);
const ink = [18, 12, 9], bronzeHi = [176, 128, 74], bronzeLo = [104, 70, 42], inner = [26, 19, 15], innerLit = [46, 34, 25];
const mix = (a, b, t) => a.map((v, i) => v + (b[i] - v) * t);

for (let y = 0; y < N; y++)
  for (let x = 0; x < N; x++) {
    const cx = x - N / 2 + 0.5, cy = y - N / 2 + 0.5;
    // 가장자리를 붓결만큼 흔든다
    const wob = (hash(Math.floor((x + y) / 5), 7) - 0.5) * 1.6;
    const d = sdBox(cx, cy, N / 2 - 2, 10) + wob;
    const p = (y * N + x) * 4;
    let c = null, a = 0;
    if (d < 0) {
      a = Math.min(1, -d);
      if (d > -4) c = ink; // 바깥 먹선
      else if (d > -11) {
        // 청동 테두리: 왼쪽 위는 밝고 오른쪽 아래는 어둡게, 평평한 두 톤 + 붓결
        const lit = (-cx - cy) / N + (stroke(x, y) - 0.5) * 0.5;
        c = lit > 0.05 ? bronzeHi : bronzeLo;
        if (d > -5.5) c = mix(c, ink, 0.35);
      } else if (d > -13) c = ink; // 안쪽 먹선
      else {
        // 안쪽 홈: 왼쪽 위에서 오는 횃불빛을 두 단계로, 붓결 약간
        const t = Math.hypot(cx + 22, cy + 22) / (N * 0.9);
        c = t < 0.45 + (stroke(x, y) - 0.5) * 0.12 ? innerLit : inner;
      }
    }
    if (c) {
      frame[p] = c[0]; frame[p + 1] = c[1]; frame[p + 2] = c[2]; frame[p + 3] = Math.round(a * 255);
    }
    // 등급 테두리: 안쪽 먹선 바로 안쪽 굵은 띠 + 안쪽으로 번지는 빛 (청동 틀과 구분되게)
    if (d < -12) {
      const band = d > -19 ? Math.min(1, -12 - d, d + 19) * (0.8 + stroke(x, y) * 0.2) : 0;
      const glow = Math.max(0, 1 - (-19 - d) / 16) * 0.35;
      rim[p] = rim[p + 1] = rim[p + 2] = 255; rim[p + 3] = Math.round(Math.min(1, Math.max(band, glow)) * 255);
    }
  }

const raw = { raw: { width: N, height: N, channels: 4 } };
await sharp(frame, raw).png().toFile(`${OUT}/item_slot.png`);
await sharp(rim, raw).png().toFile(`${OUT}/item_slot_rim.png`);
console.log(`${OUT}/item_slot.png, item_slot_rim.png`);
