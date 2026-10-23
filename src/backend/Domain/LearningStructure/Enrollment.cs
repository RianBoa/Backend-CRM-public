using backend.Domain.Clients;
using backend.Domain.Orders;
using NUlid;

namespace backend.Domain.LearningStructure
{
    public sealed class Enrollment
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        public Guid Version { get; private set; } = Guid.NewGuid();
        public Ulid OrderId { get; private set; }
        public Order Order { get; private set; } = null!;

        public Ulid? OrderItemId { get; private set; }
        public OrderItem? OrderItem { get; private set; }

        public Ulid? CohortId { get; private set; }
        public Cohort? Cohort { get; private set; }

        public Ulid? GroupId { get; private set; }
        public Group? Group { get; private set; }

        public bool IsAddedToChat { get; private set; }
        public DateTime? AddedToChatAt { get; private set; }

        public bool? IsAddedToPlatform { get; private set; }
        public DateTime? AddedToPlatformAt { get; private set; }

        public DateTime? DesiredStartDate { get; private set; }
        public DateTime EnrolledAt { get; private set; }

        public enum EnrollmentStatus
        {
            Pending,
            Active,
            Frozen,
            Graduated,
            Dropped,
            Closed,
            AccessRevoked
        }

        public EnrollmentStatus Status { get; private set; }

        private Enrollment() { }

        private Enrollment(OrderItem orderItem, DateTime? desiredStartDate = null)
        {
            OrderItem = orderItem ?? throw new ArgumentNullException(nameof(orderItem));
            OrderItemId = orderItem.Id;
            Order = orderItem.Order;
            OrderId = orderItem.OrderId;
            DesiredStartDate = desiredStartDate;
            EnrolledAt = DateTime.UtcNow;
            Status = EnrollmentStatus.Pending;
        }

        public static Enrollment Create(OrderItem orderItem, DateTime? desiredStartDate = null)
            => new Enrollment(orderItem, desiredStartDate);

        public void AssignToGroup(Group group)
        {
            ArgumentNullException.ThrowIfNull(group);
            if (Status is EnrollmentStatus.Graduated or EnrollmentStatus.Dropped or EnrollmentStatus.Closed or EnrollmentStatus.AccessRevoked)
                throw new InvalidOperationException("A terminal enrollment cannot be assigned to a group.");
            if (OrderItem is null)
                throw new InvalidOperationException("Link the enrollment to a purchase item before assigning a group.");
            if (group.Cohort is null)
                throw new InvalidOperationException("The group's cohort must be loaded.");
            if (group.Cohort.ProductId != OrderItem.ProductId)
                throw new InvalidOperationException("The cohort does not match the purchased product.");
            if (group.PackageId is not null && group.PackageId != OrderItem.PackageId)
                throw new InvalidOperationException("The group does not match the purchased package.");
            if (group.Cohort.Status is Cohort.CohortStatus.Graduated or Cohort.CohortStatus.Archived)
                throw new InvalidOperationException("Cannot assign a student to a completed or archived cohort.");

            GroupId = group.Id;
            Group = group;

            CohortId = group.CohortId;
            Cohort = group.Cohort;

            if (Status == EnrollmentStatus.Pending)
                Status = EnrollmentStatus.Active;
        }

        public void ChangeStatus(EnrollmentStatus newStatus)
        {
            if (!CanTransition(Status, newStatus))
                throw new InvalidOperationException($"Cannot change enrollment from {Status} to {newStatus}.");
            if (newStatus == EnrollmentStatus.Active && GroupId is null)
                throw new InvalidOperationException("Assign a group before activating the enrollment.");
            Status = newStatus;
        }

        public static bool CanTransition(EnrollmentStatus current, EnrollmentStatus next)
        {
            if (!Enum.IsDefined(current) || !Enum.IsDefined(next)) return false;
            if (current == next) return true;
            return (current, next) switch
            {
                (EnrollmentStatus.Pending, EnrollmentStatus.Active or EnrollmentStatus.Closed or EnrollmentStatus.AccessRevoked) => true,
                (EnrollmentStatus.Active, EnrollmentStatus.Frozen or EnrollmentStatus.Graduated or EnrollmentStatus.Dropped or EnrollmentStatus.Closed or EnrollmentStatus.AccessRevoked) => true,
                (EnrollmentStatus.Frozen, EnrollmentStatus.Active or EnrollmentStatus.Dropped or EnrollmentStatus.Closed or EnrollmentStatus.AccessRevoked) => true,
                _ => false
            };
        }
    }
}
