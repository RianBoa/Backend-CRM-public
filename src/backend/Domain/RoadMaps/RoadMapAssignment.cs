namespace backend.Domain.RoadMaps
{
    using backend.Domain.LearningStructure;
    using NUlid;
    public sealed record RoadMapAssignment
    {
        public Ulid Id { get; private init; }
        public int RoadMapItemId { get; private init; }
        public RoadMapItem RoadMapItem { get; private init; } = null!;
        public Ulid EnrollmentId { get; private init; }
        public Enrollment Enrollment { get; private init; } = null!;
        public enum AssignmentStatus
        {
            Pending,
            Completed,
            Overdue,
            Skipped
        }
        public AssignmentStatus Status { get; private set; } = AssignmentStatus.Pending;
        public int DueAtDaysFromEnrollment { get; private init; }
        public DateTime AssignedAt { get; private init; }
        public Ulid UserId { get; private init; }
        public Users.User AssignedBy { get; private init; } = null!;

        private RoadMapAssignment() { }

        public static RoadMapAssignment Create(
            int roadMapItemId,
            RoadMapItem roadMapItem,
            Ulid enrollmentId,
            Enrollment enrollment,
            int dueAtDaysFromEnrollment,
            Ulid userId,
            Users.User assignedBy)
        {
            ArgumentNullException.ThrowIfNull(roadMapItem);
            ArgumentNullException.ThrowIfNull(enrollment);
            if (roadMapItem.RoadMapTemplate is null || enrollment.OrderItem is null)
                throw new ArgumentException("Roadmap template and enrollment purchase must be loaded.");
            if (roadMapItem.RoadMapTemplate.ProductId != enrollment.OrderItem.ProductId)
                throw new ArgumentException("Roadmap template must match the purchased product.");
            if (dueAtDaysFromEnrollment < 0)
                throw new ArgumentException("Due at days from enrollment must be non-negative.", nameof(dueAtDaysFromEnrollment));
            return new RoadMapAssignment
            {
                Id = Ulid.NewUlid(),
                RoadMapItemId = roadMapItemId,
                RoadMapItem = roadMapItem ?? throw new ArgumentNullException(nameof(roadMapItem)),
                EnrollmentId = enrollmentId,
                Enrollment = enrollment ?? throw new ArgumentNullException(nameof(enrollment)),
                DueAtDaysFromEnrollment = dueAtDaysFromEnrollment,
                AssignedAt = DateTime.UtcNow,
                UserId = userId,
                AssignedBy = assignedBy ?? throw new ArgumentNullException(nameof(assignedBy))
            };
        }
        public static RoadMapAssignment CreateForTest(
            Ulid id,
            int roadMapItemId,
            RoadMapItem roadMapItem,
            Ulid enrollmentId,
            Enrollment enrollment,
            int dueAtDaysFromEnrollment,
            AssignmentStatus status,
            DateTime assignedAt,
            Ulid userId,
            Users.User assignedBy)
        {
            if (dueAtDaysFromEnrollment < 0)
                throw new ArgumentException("Due at days from enrollment must be non-negative.", nameof(dueAtDaysFromEnrollment));
            return new RoadMapAssignment
            {
                Id = id,
                RoadMapItemId = roadMapItemId,
                RoadMapItem = roadMapItem ?? throw new ArgumentNullException(nameof(roadMapItem)),
                EnrollmentId = enrollmentId,
                Enrollment = enrollment ?? throw new ArgumentNullException(nameof(enrollment)),
                DueAtDaysFromEnrollment = dueAtDaysFromEnrollment,
                Status = status,
                AssignedAt = assignedAt,
                UserId = userId,
                AssignedBy = assignedBy ?? throw new ArgumentNullException(nameof(assignedBy))
            };
        }

        public static void MarkOverdueIfNeeded(RoadMapAssignment assignment, DateTime currentDate)
        {
            if (assignment.Status == AssignmentStatus.Pending)
            {
                var dueDate = assignment.Enrollment.EnrolledAt.AddDays(assignment.DueAtDaysFromEnrollment);
                if (currentDate > dueDate)
                {
                    assignment.Status = AssignmentStatus.Overdue;
                }
            }
        }
        public void UpdateStatus(AssignmentStatus newStatus)
        {
            if (!Enum.IsDefined(newStatus))
                throw new ArgumentOutOfRangeException(nameof(newStatus));
            Status = newStatus;
        }
        public bool SetCompletion(bool completed, DateTime now)
        {
            if (Status == AssignmentStatus.Skipped)
                throw new InvalidOperationException("Restore the skipped assignment before changing completion.");
            if ((Status == AssignmentStatus.Completed) == completed) return false;
            Status = completed ? AssignmentStatus.Completed
                : now > Enrollment.EnrolledAt.AddDays(DueAtDaysFromEnrollment)
                    ? AssignmentStatus.Overdue : AssignmentStatus.Pending;
            return true;
        }
        public void MarkAsCompleted() => Status = AssignmentStatus.Completed;
    }
}
