// Windows x64 플레이어 빌드: Builds/ColosseumWindows/Colosseum.exe (Builds/Windows는 다른 프로젝트 빌드라 건드리지 않는다).
// 에디터 메뉴 Colosseum/Build Windows, MCP execute_code의 BuildWindows.Build(),
// 배치 모드 -executeMethod Colosseum.EditorTools.BuildWindows.BuildBatch 모두 같은 빌드를 한다.
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Colosseum.EditorTools
{
    public static class BuildWindows
    {
        public const string OutputPath = "Builds/ColosseumWindows/Colosseum.exe";

        [MenuItem("Colosseum/Build Windows")]
        public static string Build()
        {
            // 빌드 설정의 켜진 씬, Game.unity를 맨 앞에 (진입 씬)
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
            scenes.Remove(GameSceneBuilder.ScenePath);
            scenes.Insert(0, GameSceneBuilder.ScenePath);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var s = report.summary;
            return $"{s.result} · {s.totalSize / (1024 * 1024)} MB · 오류 {s.totalErrors} · 경고 {s.totalWarnings} · {s.totalTime.TotalSeconds:0}초 · {s.outputPath}";
        }

        /// <summary>배치 모드용: 결과를 로그에 남기고 실패면 종료 코드 1</summary>
        public static void BuildBatch()
        {
            string line = Build();
            UnityEngine.Debug.Log("[BuildWindows] " + line);
            EditorApplication.Exit(line.StartsWith(BuildResult.Succeeded.ToString()) ? 0 : 1);
        }
    }
}
