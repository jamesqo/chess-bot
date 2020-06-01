namespace ChessBot.Console
{
    public class CacheNode
    {
        public ulong Key { get; }
        public int Value { get; }
        public CacheNode Previous { get; set; }
        public CacheNode Next { get; set; }
        //public int Hits { get; set; }

        // dummy initializer for head and tail
        public CacheNode()
        {
        }

        public CacheNode(ulong key, int value)
        {
            Key = key;
            Value = value;
        }
    }
}
