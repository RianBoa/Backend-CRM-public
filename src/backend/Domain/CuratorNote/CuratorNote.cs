namespace backend.Domain.CuratorNote
{
    using NUlid;
    public sealed record CuratorNote
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        public int TypeId { get; private init; }
        public NoteType Type { get; private init; } = null!;
        public Ulid ClientId { get; private init; }
        public Clients.Client Client { get; private init; } = null!;
        public string Comment { get; private init; } = null!;
        public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;
        public Ulid CreatedById { get; private init; }
        public Users.User CreatedBy { get; private init; } = null!;

        private CuratorNote() { }
    }
}
