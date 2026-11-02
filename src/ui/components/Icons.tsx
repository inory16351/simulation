// 메뉴 아이콘과 국가 문장. 동화책 그림체로 그린 이미지(tools/storybook.json → tools/slice-icons.mjs)를 쓴다.
import type { NationId } from "../../core/types";
import { asset } from "../assets";

export function MenuIcon({ id, size = 22 }: { id: string; size?: number }) {
  return <img className="menu-icon" src={asset(`icon/menu/${id}.webp`)} width={size} height={size} alt="" draggable={false} />;
}

/** 나라별 문장 (color는 예전 코드 문장과 같은 인터페이스를 위해 남겨 둔다) */
export function Crest({ nation, size = 32 }: { nation: NationId; color?: string; size?: number }) {
  return <img className="crest" src={asset(`crest/${nation}.webp`)} width={size} height={size} alt="" draggable={false} />;
}
