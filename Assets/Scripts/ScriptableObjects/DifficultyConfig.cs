using UnityEngine;

namespace Blokfit.ScriptableObjects
{
    [CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Blokfit/DifficultyConfig")]
    public class DifficultyConfig : ScriptableObject
    {
        [Header("Identity")]
        public string difficultyName;

        [Header("Grid")]
        public int gridSize = 4;
        public float boardWorldSize = 7f;

        [Header("Pieces")]
        public int minPieces = 5;
        public int maxPieces = 6;
        public int minPieceSize = 3;
        public int maxPieceSize = 4;

        [Header("Rotation")]
        public bool allowRotations = false;

        [Header("Anchors")]
        public int minAnchors = 1;
        public int maxAnchors = 1;
    }
}
