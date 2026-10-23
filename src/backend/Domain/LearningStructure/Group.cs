using backend.Domain.Products;
using backend.Domain.Users;
using NUlid;

namespace backend.Domain.LearningStructure
{
    public sealed record Group
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        public Ulid CohortId { get; private init; }
        public Ulid? MentorId { get; private set; }
        public User? Mentor { get; private set; }
        public int? PackageId { get; private set; }
        public Package? Package { get; private set; }
        public Cohort Cohort { get; private init; }
        public string Name { get; private set; }

        public string? LearningPlatformId { get; private set; }
        public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;

        public ICollection<Enrollment> Enrollments { get; private init; } = new List<Enrollment>();

        private Group() { }

        private Group(Ulid cohortId, string name, Ulid? mentorId = null, int? packageId = null)
        {
            CohortId = cohortId;
            Name = name;
            MentorId = mentorId;
            PackageId = packageId;
        }

        public static Group Create(Ulid cohortId, string name, Ulid? mentorId = null, int? packageId = null)
            => new Group(cohortId, name, mentorId, packageId);

        public static Group Create(Cohort cohort, string name, Ulid? mentorId = null, int? packageId = null)
        {
            ArgumentNullException.ThrowIfNull(cohort);
            return new Group(cohort.Id, name, mentorId, packageId) { Cohort = cohort };
        }

        public void AssignMentor(Ulid? mentorId)
        {
            MentorId = mentorId;
        }

        public void ChangePackage(int? packageId)
        {
            PackageId = packageId;
        }

        public void ChangeName(string name)
        {
            Name = name;
        }

        public void LinkLearningPlatformGroup(string externalId)
        {
            LearningPlatformId = externalId.Trim();
        }
    }
}
