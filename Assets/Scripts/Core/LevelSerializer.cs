using UnityEngine;

namespace Blokfit.Core
{
    public static class LevelSerializer
    {
        public static string    ToJson(LevelData data)   => JsonUtility.ToJson(data, prettyPrint: true);
        public static LevelData FromJson(string json)    => JsonUtility.FromJson<LevelData>(json);
    }
}
