namespace Blokfit.Commands
{
    /// <summary>
    /// Contract for all reversible game actions (Command pattern).
    /// </summary>
    public interface ICommand
    {
        void Execute();
        void Undo();
    }
}
