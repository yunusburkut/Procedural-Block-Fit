using UnityEngine;

namespace Blokfit.ScriptableObjects
{
    [CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Blokfit/Difficulty Config")]
    public class DifficultyConfig : ScriptableObject
    {
        public string difficultyName;
        public int    gridSize      = 4;
        public int    minPieces     = 5;
        public int    maxPieces     = 6;
        public int    minPieceSize  = 3;
        public int    maxPieceSize  = 4;
        public bool   allowRotations = false;
    }
}
