using Npgsql;

namespace backend.Infrastructure;

internal static class PersistenceConflicts
{
    public static bool IsSerializationConflict(Exception error)
    {
        // Providers can wrap a serialization failure in their execution-strategy exception
        for (Exception? cause = error; cause is not null; cause = cause.InnerException)
            if (cause is PostgresException { SqlState: "40001" or "40P01" }) return true;
        return false;
    }
}
