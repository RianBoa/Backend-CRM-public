using backend.Domain.Products;
using NUlid;
using System.Text.RegularExpressions;

namespace backend.Domain.LearningStructure
{
    public sealed record Cohort
    {

        public Ulid Id { get; private init; } = Ulid.NewUlid();

        public int? ProductId { get; private init; }
        public Product Product { get; private init; } = null!;

        public string Name { get; private set; }

        public DateTime StartAt { get; private set; }
        public DateTime? EndsAt { get; private set; }
        public enum CohortStatus
        {
            Planned,
            Active,
            Graduated,
            Archived
        }

        public CohortStatus Status { get; private set; }

        public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;

        // Навигация
        public ICollection<Group> Groups { get; private init; } = new List<Group>();
        public ICollection<Enrollment> Enrollments { get; private init; } = new List<Enrollment>();

        private Cohort() { }

        private Cohort(int productId, string name, DateTime startAt, DateTime endsAt)
        {
            ProductId = productId;
            Name = name;
            StartAt = startAt;
            EndsAt = endsAt;
            Status = CohortStatus.Planned;
        }

        public static Cohort Create(int productId, string name, DateTime startAt, DateTime endsAt)
            => new Cohort(productId, name, startAt, endsAt);

        public void SetStatus(CohortStatus status)
        {
            Status = status;
        }
    }


}
