namespace backend.Domain.RoadMaps
{
    public sealed record RoadMapItem
    {
        public int Id { get; private init; }
        public int RoadMapTemplateId { get; private init; }
        public RoadMapTemplate RoadMapTemplate { get; private init; } = null!;
        public string Title { get; private set; } = null!;
        public enum RoadMapItemType
        {
           Lesson,
           Task,
           Case,
        }
        public RoadMapItemType Type { get; private set; }
        public int DaysFromStart { get; private set; }

        private RoadMapItem() { }

        public static RoadMapItem Create(
            int roadMapTemplateId,
            string title,
            RoadMapItemType type,
            int daysFromStart)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title must not be empty.", nameof(title));
            if (daysFromStart < 0)
                throw new ArgumentException("Days from start must be non-negative.", nameof(daysFromStart));
            return new RoadMapItem
            {
                RoadMapTemplateId = roadMapTemplateId,
                Title = title,
                Type = type,
                DaysFromStart = daysFromStart
            };
        }

        public void Update(
            string title,
            RoadMapItemType type,
            int daysFromStart)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title must not be empty.", nameof(title));
            if (daysFromStart < 0)
                throw new ArgumentException("Days from start must be non-negative.", nameof(daysFromStart));
            Title = title;
            Type = type;
            DaysFromStart = daysFromStart;
        }
    }
}
