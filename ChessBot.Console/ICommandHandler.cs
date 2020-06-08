namespace ChessBot.Console
{
    interface ICommandHandler
    {
        void ExitCommand();
        void HelpCommand();
        void MovesCommand();
        void SearchTimesCommand();
    }
}