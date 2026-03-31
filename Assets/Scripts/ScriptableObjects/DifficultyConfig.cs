using UnityEngine;

namespace Blokfit.ScriptableObjects
{
    /// <summary>
    /// ScriptableObject that drives procedural level generation for one difficulty tier.
    /// Create via Assets > Create > Blokfit > Difficulty Config.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Blokfit/Difficulty Config")]
    public class DifficultyConfig : ScriptableObject
    {
        public string difficultyName;
        public int    gridSize      = 4;   // board is gridSize × gridSize cells
        public int    minPieces     = 5;
        public int    maxPieces     = 6;
        public int    minPieceSize  = 3;   // size in squares (doubled to triangles internally)
        public int    maxPieceSize  = 4;
    }
}
