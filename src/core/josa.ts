// 한국어 조사 처리. 마지막 글자의 받침 유무로 이/가, 을/를 등을 고른다.
function hasBatchim(word: string): boolean {
  const ch = word.charCodeAt(word.length - 1);
  if (ch < 0xac00 || ch > 0xd7a3) return false;
  return (ch - 0xac00) % 28 !== 0;
}

function hasRieulBatchim(word: string): boolean {
  const ch = word.charCodeAt(word.length - 1);
  if (ch < 0xac00 || ch > 0xd7a3) return false;
  return (ch - 0xac00) % 28 === 8;
}

export type JosaPair = "이/가" | "을/를" | "은/는" | "과/와" | "으로/로" | "아/야" | "이여/여";

export function josa(word: string, pair: JosaPair): string {
  const [withB, withoutB] = pair.split("/");
  if (pair === "으로/로") return word + (hasBatchim(word) && !hasRieulBatchim(word) ? withB : withoutB);
  return word + (hasBatchim(word) ? withB : withoutB);
}
