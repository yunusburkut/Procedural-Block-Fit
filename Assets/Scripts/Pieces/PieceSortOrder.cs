namespace Blokfit.Pieces
{
    public static class PieceSortOrder
    {
        private static int _counter;

        public static int  Next()  => ++_counter;
        public static void Reset() => _counter = 0;
    }
}
