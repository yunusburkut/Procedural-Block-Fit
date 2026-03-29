namespace Blokfit.ScriptableObjects
{
    public class DifficultyConfig
    {
        public string difficultyName;
        public int    gridSize       = 4;
        public float  boardWorldSize = 7f;
        public int    minPieces      = 5;
        public int    maxPieces      = 6;
        public int    minPieceSize   = 3;
        public int    maxPieceSize   = 4;
        public bool   allowRotations = false;
        public int    minAnchors     = 1;
        public int    maxAnchors     = 1;
    }
}
