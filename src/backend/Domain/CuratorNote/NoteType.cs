namespace backend.Domain.CuratorNote
{
    public sealed record NoteType
    {
        public int Id { get; private init; }
        public enum NoteCategory
        {
            Discord,
            Bot,
            Portfolio,
            Feedback
        }
        public NoteCategory Category { get; private init; }
        public string Name { get; private init; } = null!;

        private NoteType() { }
    }
}
