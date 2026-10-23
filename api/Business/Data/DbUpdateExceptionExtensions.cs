using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace StargateAPI.Business.Data
{
    public static class DbUpdateExceptionExtensions
    {
        // SQLITE_CONSTRAINT_UNIQUE: the save would have put a duplicate value into a unique index.
        private const int SqliteConstraintUnique = 2067;

        // Lets a handler turn a unique index violation into a message about what the caller sent.
        public static bool IsUniqueConstraintViolation(this DbUpdateException exception)
        {
            return exception.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique };
        }
    }
}
