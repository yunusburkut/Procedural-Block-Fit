namespace Blokfit.Commands
{
    public interface ICommand
    {
        void Execute();
        void Undo();
    }
}
