using NUlid;

namespace backend.Domain.Tasks
{
    public sealed record WorkflowTask
    {
        public Ulid Id { get; init; } = Ulid.NewUlid();


        public Ulid? ClientId { get; init; }
        public Clients.Client? Client { get; init; }

        public Ulid? SubscriptionId { get; init; }
        public Subscriptions.Subscription? Subscription { get; init; }

        public string Title { get; init; } = null!;
        public WorkflowTaskType Type { get; init; }
        public WorkflowTaskPriority Priority { get; init; }
        public WorkflowTaskStatus Status { get; init; } = WorkflowTaskStatus.Open;

        public DateTimeOffset? Deadline { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }

        public Ulid? AssignedTo { get; init; }
        public Users.User? AssignedUser { get; init; }

        private WorkflowTask() { }

        private WorkflowTask(
            Ulid clientId,
            string title,
            WorkflowTaskType type,
            WorkflowTaskPriority priority,
            DateTimeOffset? deadline = null)
        {
            ClientId = clientId;
            Title = title;
            Type = type;
            Priority = priority;
            Status = WorkflowTaskStatus.Open;
            Deadline = deadline;
        }

        public static WorkflowTask Create(
            Ulid clientId,
            string title,
            WorkflowTaskType type,
            WorkflowTaskPriority priority,
            DateTimeOffset? deadline = null) =>
            new(clientId, title, type, priority, deadline);

        public WorkflowTask AssignTo(Ulid userId) =>
            this with { AssignedTo = userId };

        public WorkflowTask MarkInProgress() =>
            Status == WorkflowTaskStatus.Open
                ? this with { Status = WorkflowTaskStatus.InProgress }
                : this;

        public WorkflowTask MarkDone() =>
            Status is WorkflowTaskStatus.Open or WorkflowTaskStatus.InProgress
                ? this with { Status = WorkflowTaskStatus.Done, CompletedAt = DateTimeOffset.UtcNow }
                : this;

        public WorkflowTask Cancel() =>
            this with { Status = WorkflowTaskStatus.Canceled };
    }

    public enum WorkflowTaskType
    {
        Onboarding,
        Support,
        Finance,
        Other
    }

    public enum WorkflowTaskPriority
    {
        Low,
        Normal,
        High,
        Urgent
    }

    public enum WorkflowTaskStatus
    {
        Open,
        InProgress,
        Done,
        Canceled
    }
}
