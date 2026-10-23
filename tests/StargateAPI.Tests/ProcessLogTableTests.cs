using Dapper;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests
{
    // README task 5: request outcomes are stored in the database. Each test uses its own Operation name.
    public class ProcessLogTableTests : IClassFixture<StargateApiFactory>
    {
        private readonly StargateApiFactory _factory;

        public ProcessLogTableTests(StargateApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ProcessLog_SavedEntry_IsStoredAsReadableColumns()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
                context.ProcessLogs.Add(new ProcessLog
                {
                    TimestampUtc = new DateTime(2026, 10, 23, 14, 5, 9, DateTimeKind.Utc),
                    Level = ProcessLogLevel.Warning,
                    Operation = "StoredEntryCheck",
                    Message = "A person named 'John Doe' already exists.",
                    RequestJson = """{"name":"John Doe"}""",
                    DurationMs = 12
                });
                await context.SaveChangesAsync();
            }

            // Read back with plain SQL, as GET /ProcessLog and SQLite Viewer will, so the test sees what the table really holds.
            using var assertScope = _factory.Services.CreateScope();
            var connection = assertScope.ServiceProvider.GetRequiredService<StargateContext>().Connection;
            var row = (IDictionary<string, object?>)await connection.QuerySingleAsync(
                "SELECT TimestampUtc, Level, Operation, Message, RequestJson, Exception, DurationMs FROM [ProcessLog] WHERE Operation = 'StoredEntryCheck'");

            Assert.Equal("2026-10-23 14:05:09", row["TimestampUtc"]);
            Assert.Equal("Warning", row["Level"]);
            Assert.Equal("A person named 'John Doe' already exists.", row["Message"]);
            Assert.Equal("""{"name":"John Doe"}""", row["RequestJson"]);
            Assert.Null(row["Exception"]);
            Assert.Equal(12L, row["DurationMs"]);
        }
    }
}
