import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    // Unity 프로젝트는 감시하지 않는다 (Library 파일을 잡고 있으면 Unity 패키지 설치가 EPERM으로 실패한다)
    watch: { ignored: ["**/ColosseumUnity/**"] },
  },
});
