// 파츠가 붙는 자리를 그림에서 직접 잰다.
// 머리: 눈(어두운 덩어리) 위치로 얼굴 중심과 눈높이, 정수리, 눈높이의 얼굴 폭, 턱(목이 좁아지는 줄), 어깨.
// 파츠: 머리카락·투구는 얼굴이 들어갈 빈 구멍(위쪽 가장자리·폭), 수염은 콧수염 윗가장자리, 갑옷은 목 파임.
const sharp = require("sharp");

// keepApart: 떨어진 조각도 남긴다 (양 볼 문신, 귀 뒤로 늘어진 머리 꼬리처럼 일부러 나뉜 그림)
async function load(file, keepApart = false) {
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const W = info.width, H = info.height;
  // 옆 칸에서 묻어 온 부스러기를 지운다: 큰 덩어리만 남긴다
  const lab = new Int32Array(W * H).fill(-1); const sizes = [];
  for (let i = 0; i < W * H; i++) {
    if (lab[i] >= 0 || data[i * 4 + 3] <= 128) continue;
    const id = sizes.length; let n = 0; const st = [i]; lab[i] = id;
    while (st.length) { const p = st.pop(); n++; const x = p % W, y = (p - x) / W;
      for (const q of [x > 0 ? p - 1 : -1, x < W - 1 ? p + 1 : -1, y > 0 ? p - W : -1, y < H - 1 ? p + W : -1]) if (q >= 0 && lab[q] < 0 && data[q * 4 + 3] > 128) { lab[q] = id; st.push(q); } }
    sizes.push(n);
  }
  const big = Math.max(0, ...sizes);
  // 가장 큰 덩어리의 영역(조금 넓혀서) 밖에 중심이 있는 덩어리는 옆 칸 파츠로 보고 지운다
  const bigId = sizes.indexOf(big);
  const box = [W, H, -1, -1], cxs = new Float64Array(sizes.length), cys = new Float64Array(sizes.length);
  for (let i = 0; i < W * H; i++) { const id = lab[i]; if (id < 0) continue; const x = i % W, y = (i - x) / W; cxs[id] += x; cys[id] += y;
    if (id === bigId) { box[0] = Math.min(box[0], x); box[1] = Math.min(box[1], y); box[2] = Math.max(box[2], x); box[3] = Math.max(box[3], y); } }
  const mx = (box[2] - box[0]) * 0.35, my = (box[3] - box[1]) * 0.35;
  for (let id = 0; id < sizes.length; id++) {
    const cx = cxs[id] / sizes[id], cy = cys[id] / sizes[id];
    if (!keepApart && id !== bigId && (cx < box[0] - mx || cx > box[2] + mx || cy < box[1] - my || cy > box[3] + my)) sizes[id] = 0;
  }
  for (let i = 0; i < W * H; i++) if (data[i * 4 + 3] > 0 && (lab[i] < 0 ? true : sizes[lab[i]] < big * 0.03)) {
    // 반투명 가장자리는 이웃 큰 덩어리가 있으면 남긴다
    if (lab[i] < 0) { const x = i % W, y = (i - x) / W; const nb = [i - 1, i + 1, i - W, i + W].some((q) => q >= 0 && q < W * H && lab[q] >= 0 && sizes[lab[q]] >= big * 0.03); if (nb) continue; }
    data[i * 4 + 3] = 0;
  }
  const clean = () => sharp(data, { raw: info }).png().toBuffer();
  const A = (x, y) => (x < 0 || y < 0 || x >= W || y >= H ? 0 : data[(y * W + x) * 4 + 3]);
  const L = (x, y) => { const i = (y * W + x) * 4; return 0.3 * data[i] + 0.59 * data[i + 1] + 0.11 * data[i + 2]; };
  const row = (y) => { let l = -1, r = -1; for (let x = 0; x < W; x++) if (A(x, y) > 128) { if (l < 0) l = x; r = x; } return [l, r]; };
  let top = -1, bottom = -1, left = W, right = -1;
  for (let y = 0; y < H; y++) { const [l, r] = row(y); if (l >= 0) { if (top < 0) top = y; bottom = y; left = Math.min(left, l); right = Math.max(right, r); } }
  return { W, H, A, L, row, top, bottom, left, right, cx: (left + right) / 2, clean };
}

async function headAnchors(file) {
  const m = await load(file);
  // 머리 윤곽: 위에서부터 가장 넓은 줄까지가 머리
  let maxW = 0, maxY = m.top;
  for (let y = m.top; y < m.top + (m.bottom - m.top) * 0.6; y++) { const [l, r] = m.row(y); if (r - l > maxW) { maxW = r - l; maxY = y; } }
  let chin = maxY;
  for (let y = maxY; y < m.H; y++) { const [l, r] = m.row(y); if (l >= 0 && r - l < maxW * 0.6) { chin = y; break; } }
  // 눈: 정수리와 턱 사이 얼굴 안쪽의 아주 어두운 픽셀 (테두리 선은 빼려고 가장자리에서 떨어진 곳만)
  const [hl, hr] = m.row(maxY);
  const xs = [], ys = [];
  for (let y = Math.round(m.top + (chin - m.top) * 0.3); y < m.top + (chin - m.top) * 0.75; y++) {
    const [l, r] = m.row(y);
    for (let x = l + (r - l) * 0.15; x < r - (r - l) * 0.15; x++) {
      const xi = Math.round(x);
      if (m.A(xi, y) > 200 && m.L(xi, y) < 55) { xs.push(xi); ys.push(y); }
    }
  }
  const mid = (hl + hr) / 2;
  const leftEye = xs.filter((x) => x < mid), rightEye = xs.filter((x) => x >= mid);
  const avg = (a) => a.reduce((s, v) => s + v, 0) / Math.max(1, a.length);
  const eyeY = avg(ys);
  const eyeL = avg(leftEye), eyeR = avg(rightEye);
  const cx = leftEye.length && rightEye.length ? (eyeL + eyeR) / 2 : mid;
  // 얼굴 폭: 눈높이에서 귀를 뺀 폭 ≈ 눈 사이 거리의 2.4배 (귀 때문에 윤곽 폭은 쓰지 않는다)
  let eyeGap = eyeR - eyeL;
  // 털이 어둡거나 눈을 못 찾으면 템플릿 비율로 대신한다 (머리 폭의 42%, 정수리~턱의 55% 높이)
  let eyeYv = eyeY, cxv = cx;
  if (!(eyeGap > maxW * 0.2 && eyeGap < maxW * 0.7) || !(eyeY > m.top && eyeY < chin)) {
    eyeGap = maxW * 0.42; eyeYv = m.top + (chin - m.top) * 0.6; cxv = mid;
  }
  const faceW = Math.min(maxW * 0.92, eyeGap * 2.45);
  // 어깨: 턱 아래에서 가장 넓은 줄
  let shW = 0, shCx = cx, shY = m.bottom;
  for (let y = m.bottom; y > chin; y--) { const [l, r] = m.row(y); if (l >= 0 && r - l > shW) { shW = r - l; shCx = (l + r) / 2; shY = y; } }
  // 목 아래쪽(어깨 시작): 턱에서 목 폭이 다시 넓어지기 시작하는 곳
  let neckW = 1e9, neckBase = chin;
  for (let y = chin; y < shY; y++) { const [l, r] = m.row(y); if (l < 0) continue; if (r - l < neckW) neckW = r - l; if (r - l > neckW * 1.6) { neckBase = y; break; } }
  return { top: m.top, cx: cxv, eyeY: eyeYv, eyeGap, faceW, headW: maxW, chin, neckBase, neckW, shW, shCx, shY, W: m.W, H: m.H };
}

/** 가운데 세로줄을 따라 위에서 내려가며: 처음 불투명이 나온 뒤 처음 투명해지는 곳 = 구멍의 윗가장자리 */
function openingTop(m) {
  const x = Math.round(m.cx);
  let seen = false;
  for (let y = m.top; y <= m.bottom; y++) {
    const a = m.A(x, y) > 128 || m.A(x - 3, y) > 128 || m.A(x + 3, y) > 128;
    if (a) seen = true;
    else if (seen) return y;
  }
  return -1;
}
/** y줄에서 가운데를 포함한 빈 구간의 폭 (양쪽에 막힌 부분이 없으면 -1) */
function gapAt(m, y) {
  const x0 = Math.round(m.cx);
  let l = x0, r = x0;
  while (l > m.left && m.A(l, y) <= 128) l--;
  while (r < m.right && m.A(r, y) <= 128) r++;
  if (l <= m.left || r >= m.right) return -1;
  return r - l;
}

async function partAnchors(file, kind) {
  const m = await load(file, kind === "mark" || kind === "hair");
  const out = { left: m.left, top: m.top, w: m.right - m.left + 1, h: m.bottom - m.top + 1, cx: m.cx, buf: await sharp(await m.clean()).extract({ left: m.left, top: m.top, width: m.right - m.left + 1, height: m.bottom - m.top + 1 }).png().toBuffer() };
  // 질량 중심과 잘라 낸 폭(양 끝 5%의 삐친 부분은 무시)
  const xsAll = [];
  for (let y = m.top; y <= m.bottom; y += 2) for (let x = m.left; x <= m.right; x += 2) if (m.A(x, y) > 128) xsAll.push(x);
  xsAll.sort((p, q) => p - q);
  const q = (t) => xsAll[Math.floor((xsAll.length - 1) * t)];
  out.massCx = xsAll.reduce((p, v) => p + v, 0) / xsAll.length;
  out.coreW = q(0.97) - q(0.03);
  // 앞머리 끝: 가운데 띠의 각 세로줄에서 위부터 이어진 불투명 구간의 끝, 그중 가장 낮은 값의 중간값
  {
    const ends = [];
    for (let x = Math.round(out.massCx - out.coreW * 0.15); x <= out.massCx + out.coreW * 0.15; x += 2) {
      let y = m.top; while (y <= m.bottom && m.A(x, y) <= 128) y++;
      while (y <= m.bottom && m.A(x, y) > 128) y++;
      ends.push(y);
    }
    ends.sort((p, q) => p - q);
    out.bangs = ends[Math.floor(ends.length * 0.5)];
  }
  if (kind === "hair" || kind === "helmet_open") {
    const oy = openingTop(m);
    // 구멍 폭: 윗가장자리에서 조금 내려간 곳 (투구는 눈높이 근처까지)
    // 얼굴 구멍의 가장 넓은 폭 (구멍 윗가장자리부터 아래로 35%까지)
    let gw = -1;
    for (let y = oy; y < oy + out.h * 0.35; y += 2) gw = Math.max(gw, gapAt(m, y));
    out.openTop = oy;
    out.openW = gw > 0 ? gw : out.w * 0.8; // 옆머리가 없는 짧은 머리는 머리 폭 기준
    out.openFromSides = gw <= 0;
  } else if (kind === "beard") {
    // 콧수염 윗가장자리: 가운데 줄에서 처음 불투명한 곳
    let y = m.top;
    for (; y <= m.bottom; y++) if (m.A(Math.round(m.cx), y) > 128) break;
    out.mustacheTop = y;
  } else if (kind === "armor") {
    // 목 파임: 가운데 줄에서 처음 불투명한 곳
    let y = m.top;
    for (; y <= m.bottom; y++) if (m.A(Math.round(m.cx), y) > 128) break;
    out.neckNotch = y;
  }
  return out;
}

/** 측면 머리(오른쪽을 봄): 정수리, 뒤통수, 이마 앞, 눈, 코끝 높이, 턱, 목 끝 */
async function sideHeadAnchors(file) {
  const m = await load(file);
  // 눈: 머리 위쪽 절반 안의 아주 어두운 픽셀
  const xs = [], ys = [];
  for (let y = m.top; y < m.top + (m.bottom - m.top) * 0.75; y++) {
    const [l, r] = m.row(y);
    for (let x = l + (r - l) * 0.2; x < r - (r - l) * 0.06; x++) { const xi = Math.round(x); if (m.A(xi, y) > 200 && m.L(xi, y) < 55) { xs.push(xi); ys.push(y); } }
  }
  const avg = (a) => a.reduce((p, v) => p + v, 0) / Math.max(1, a.length);
  const eyeY = avg(ys), eyeX = avg(xs);
  const [back] = m.row(Math.round(eyeY));
  // 이마 앞: 정수리와 눈 사이에서 가장 오른쪽
  let front = 0;
  for (let y = m.top; y < eyeY; y++) front = Math.max(front, m.row(y)[1]);
  const headLen = front - back;
  // 코끝: 눈 아래에서 가장 오른쪽인 줄
  let noseX = 0, nose = eyeY;
  for (let y = Math.round(eyeY); y < eyeY + headLen * 0.5; y++) { const r = m.row(y)[1]; if (r > noseX) { noseX = r; nose = y; } }
  // 턱: 코 아래에서 앞쪽 윤곽이 크게 들어가는 줄 (목 앞선)
  let chin = nose;
  for (let y = nose; y < m.bottom; y++) { const r = m.row(y)[1]; if (r < front - headLen * 0.32) { chin = y; break; } }
  const [nl, nr] = m.row(m.bottom - 2);
  return { top: m.top, back, front, headLen, cx: (back + front) / 2, eyeX, eyeY, nose: nose + headLen * 0.04, chin, neckX: (nl + nr) / 2, neckBottom: m.bottom };
}

/** 측면 파츠: 정면과 같은 값에 앞 가장자리(오른쪽, 삐친 부분 제외)를 더한다 */
async function sidePartAnchors(file, kind) {
  const out = await partAnchors(file, kind === "hair_cap" ? "hair" : kind);
  const m = await load(file);
  const xs = [];
  for (let y = m.top; y <= m.bottom; y += 2) for (let x = m.left; x <= m.right; x += 2) if (m.A(x, y) > 128) xs.push(x);
  xs.sort((p, q) => p - q);
  out.front = xs[Math.floor((xs.length - 1) * 0.985)];
  return out;
}

module.exports = { headAnchors, partAnchors, sideHeadAnchors, sidePartAnchors, load };
