namespace ChessBot.Search.Tt
{
    public interface ITranspositionTable<in TValue, TNode>
    {
        void Add<TState>(TState state, TValue value) where TState : IState;
        bool Touch(TNode node);
        bool TryGetNode<TState>(TState state, out TNode node) where TState : IState;
    }
}
