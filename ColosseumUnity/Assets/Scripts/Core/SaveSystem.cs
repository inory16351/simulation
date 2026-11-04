// 저장·불러오기 (src/core/state.ts saveGame/loadGame). 웹판과 같은 JSON 모양이라 웹판 저장도 읽을 수 있다.
// 경로는 호출하는 쪽(Unity: Application.persistentDataPath)이 넘긴다.
using System;
using System.IO;
using Newtonsoft.Json;

namespace Colosseum.Core
{
    public static class SaveSystem
    {
        public const int Version = 9;
        public const string FileName = "colosseum-league.save.v9.json";

        static readonly JsonSerializerSettings Settings = new() { NullValueHandling = NullValueHandling.Ignore };

        public static string ToJson(GameState s) => JsonConvert.SerializeObject(s, Settings);

        public static GameState FromJson(string json)
        {
            var s = JsonConvert.DeserializeObject<GameState>(json, Settings);
            if (s == null || s.version != Version) return null;
            // 장비 시장이 생기기 전 저장: 매물을 처음 깐다
            if (s.gearMarket == null || s.gearMarket.Count == 0) GearLogic.RefreshGearMarket(s, new Rng(s.rngState), true);
            // 전투 감각이 생기기 전 저장
            foreach (var g in s.gladiators.Values) GladiatorRules.FillSenseAttrs(g);
            Training.Today = Calendar.AbsDay(s.season, s.day);
            return s;
        }

        public static void Save(string dir, GameState s)
        {
            try
            {
                // 쓰다가 꺼져도 예전 저장이 남게 임시 파일에 쓰고 바꾼다
                string path = Path.Combine(dir, FileName), tmp = path + ".tmp";
                File.WriteAllText(tmp, ToJson(s));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception)
            {
                // 저장 공간이 없거나 막혀 있어도 게임은 계속 진행한다
            }
        }

        public static GameState Load(string dir)
        {
            try
            {
                string path = Path.Combine(dir, FileName);
                return File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Clear(string dir)
        {
            try { File.Delete(Path.Combine(dir, FileName)); }
            catch (Exception) { /* 무시 */ }
        }
    }
}
