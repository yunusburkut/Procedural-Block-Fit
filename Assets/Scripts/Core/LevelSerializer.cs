using UnityEngine;

namespace Blokfit.Core
{
    /// <summary>Thin wrapper around Unity's JsonUtility for level serialisation.</summary>
    public static class LevelSerializer
    {
        public static string    ToJson(LevelData data)   => JsonUtility.ToJson(data, prettyPrint: true);
        public static LevelData FromJson(string json)    => JsonUtility.FromJson<LevelData>(json);
    }
}
