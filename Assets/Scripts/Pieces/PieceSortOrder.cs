namespace Blokfit.Pieces
{
    /// <summary>
    /// Global monotonically-increasing sprite sorting-order counter.
    /// Each new piece claims the next value so that the most recently touched piece
    /// always renders on top. Reset between levels to prevent overflow drift.
    /// </summary>
    public static class PieceSortOrder
    {
        private static int _counter;

        public static int  Next()  => ++_counter;
        public static void Reset() => _counter = 0;
    }
}
