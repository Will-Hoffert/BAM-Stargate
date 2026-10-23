using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests
{
    // GET /ProcessLog returns the newest entries. Tests in a class run one at a time, and reading the log adds no
    // entries, so the rows a test has just added are the newest ones.
    public class ProcessLogEndpointTests : IClassFixture<StargateApiFactory>
    {
        private readonly StargateApiFactory _factory;
        private readonly HttpClient _client;

        public ProcessLogEndpointTests(StargateApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetProcessLog_Take_ReturnsNewestEntriesWithoutException()
        {
            await AddEntriesAsync(
                Entry("Oldest Check Operation"),
                Entry("Older Check Operation"),
                new ProcessLog
                {
                    TimestampUtc = new DateTime(2026, 10, 23, 14, 5, 9, 123, DateTimeKind.Utc),
                    Level = ProcessLogLevel.Error,
                    Operation = "Newest Check Operation",
                    Message = "An unexpected error occurred.",
                    RequestJson = """{"name":"Newest Check Person"}""",
                    Exception = "Microsoft.Data.Sqlite.SqliteException: SQLite Error 1: 'secret table detail'.",
                    DurationMs = 7
                });

            var response = await _client.GetAsync("/ProcessLog?take=2");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("secret table detail", json);
            using var document = JsonDocument.Parse(json);
            var entries = document.RootElement.GetProperty("entries").EnumerateArray().ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal("Older Check Operation", entries[1].GetProperty("operation").GetString());

            var newest = entries[0];
            Assert.Equal("Newest Check Operation", newest.GetProperty("operation").GetString());
            Assert.Equal("Error", newest.GetProperty("level").GetString());
            Assert.Equal("An unexpected error occurred.", newest.GetProperty("message").GetString());
            Assert.Equal("""{"name":"Newest Check Person"}""", newest.GetProperty("requestJson").GetString());
            Assert.Equal(7, newest.GetProperty("durationMs").GetInt64());
            // Marked as UTC, so a browser doesn't read it as the viewer's local time.
            Assert.Equal("2026-10-23T14:05:09.123Z", newest.GetProperty("timestampUtc").GetString());
            Assert.False(newest.TryGetProperty("exception", out _));
        }

        [Fact]
        public async Task GetProcessLog_TakeMissingOrOutOfRange_ReturnsDefaultOrNearestLimit()
        {
            await AddEntriesAsync(Enumerable.Range(1, 201).Select(i => Entry($"Bulk Check Operation {i}")).ToArray());

            Assert.Equal(50, await CountEntriesAsync("/ProcessLog"));
            Assert.Equal(1, await CountEntriesAsync("/ProcessLog?take=0"));
            Assert.Equal(1, await CountEntriesAsync("/ProcessLog?take=-5"));
            Assert.Equal(200, await CountEntriesAsync("/ProcessLog?take=200"));
            Assert.Equal(200, await CountEntriesAsync("/ProcessLog?take=500"));
        }

        [Fact]
        public async Task GetProcessLog_Read_IsNotLogged()
        {
            // Otherwise every refresh of a log view would add an entry about itself.
            var first = await _client.GetAsync("/ProcessLog");
            var second = await _client.GetAsync("/ProcessLog");

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            Assert.False(await context.ProcessLogs.AnyAsync(e => e.Operation == "GetProcessLog"));
        }

        private async Task AddEntriesAsync(params ProcessLog[] entries)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            foreach (var entry in entries)
            {
                // One save each, so the Ids follow the order given.
                context.ProcessLogs.Add(entry);
                await context.SaveChangesAsync();
            }
        }

        private async Task<int> CountEntriesAsync(string url)
        {
            var response = await _client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.GetProperty("entries").GetArrayLength();
        }

        private static ProcessLog Entry(string operation)
        {
            return new ProcessLog
            {
                TimestampUtc = DateTime.UtcNow,
                Level = ProcessLogLevel.Information,
                Operation = operation,
                Message = "Successful",
                RequestJson = "{}",
                DurationMs = 1
            };
        }
    }
}
