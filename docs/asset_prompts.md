# 에셋 프롬프트 (Codex CLI 이미지 생성)

이미지는 Codex CLI의 이미지 생성 기능으로 만든다. 프롬프트와 출력 경로는 `tools/assets.json`에서 관리하고, `tools/gen-images.mjs`가 항목마다 `codex exec`를 실행해서 생성한다.

```bash
node tools/gen-images.mjs --only portrait.human     # 일부만
node tools/gen-images.mjs --missing                 # 아직 없는 파일만
```

## 공통 스타일 블록

모든 프롬프트 앞에 붙인다.

```
Dark gritty fantasy, layered painterly illustration with rough visible brush strokes,
cut-paper puppet aesthetic (like Salt and Sanctuary / Hollow Knight Silksong),
low saturation charcoal, sand and tarnished bronze palette, strong side light from torches,
deep shadows, visceral and grim mood. No text, no watermark, no border.
```

유혈이 필요한 파츠에는 추가한다.

```
Graphic blood: thick dark-red painterly blood, wet sheen, splatter and drips.
```

## 정렬 규칙 (파츠 합성을 위해 필수)

| 종류 | 캔버스 | 배경 | 정렬 |
|---|---|---|---|
| 초상화 베이스 | 512×512 | 불투명, 어두운 단색 | 얼굴 중심 (256, 230), 정면에서 약간 돌린 3/4 흉상, 턱 아래로 어깨까지 |
| 초상화 오버레이 (흉터·피·붕대·안대) | 512×512 | **투명** | 베이스와 같은 얼굴 위치에 맞춤 |
| 리그 파츠 | 256×256 | **투명** | 파츠 하나만, 관절 쪽 끝이 캔버스 위쪽 중앙에 오게 |
| 배경 레이어 | 1920×1080 | 원경만 불투명, 나머지 **투명** | 지평선 y=620 고정 |
| 문장·아이콘 | 256×256 / 128×128 | **투명** | 중앙 정렬, 단색 청동 실루엣 |

## 1차 제작 목록

| 우선 | 그룹 | 수량 | 경로 |
|---|---|---|---|
| 1 | 종족별 초상화 베이스 | 7종족 × 4 | `public/assets/portrait/{race}/base_{n}.png` |
| 1 | 초상화 오버레이 (흉터 6, 피 3, 붕대 2, 안대 1) | 12 | `public/assets/portrait/overlay/{id}.png` |
| 2 | 국가 문장 | 6 | `public/assets/crest/{nation}.png` |
| 2 | 스타일 아이콘 | 11 | `public/assets/icon/style/{id}.png` |
| 3 | 속주 투기장 패럴랙스 | 6레이어 | `public/assets/arena/provincial/{n}_{name}.png` |
| 4 | 리그 파츠 (인간 기본 몸) | 11파츠 | `public/assets/rig/human/{bone}.png` |
| 4 | 피 파티클·피 웅덩이 데칼 | 8 | `public/assets/fx/blood_{n}.png` |

파츠 합성은 생성 결과가 조금씩 어긋날 수 있으므로, 생성 후 `tools/assets.json`의 `offset` 값으로 미세 조정한다.
