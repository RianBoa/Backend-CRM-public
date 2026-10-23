using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUlid;

namespace backend.Infrastructure.Converters
{
    public class NullableUlidToStringConverter : ValueConverter<Ulid?, string?>
    {
        public NullableUlidToStringConverter()
            : base(
                v => v.HasValue ? v.Value.ToString() : null,
                v => v == null ? null : Ulid.Parse(v))
        { }
    }
}
