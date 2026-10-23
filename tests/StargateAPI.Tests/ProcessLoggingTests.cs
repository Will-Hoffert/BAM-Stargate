using System.Net;
using System.Net.Http.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;
using StargateAPI.Infrastructure;

namespace StargateAPI.Tests
{
    // README task 5: every request's outcome is stored in ProcessLog, and logging never changes the response.
    // Each test uses its own names.
    public class ProcessLoggingTests : IClassFixture<StargateApiFactory>
    {
        private static readonly CancellationToken CancelledToken = new(canceled: true);

        private readonly StargateApiFactory _factory;
        private readonly HttpClient _client;

        public ProcessLoggingTests(StargateApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreatePerson_Success_LogsInformation()
        {
            var before = DateTime.UtcNow;

            var response = await _client.PostAsJsonAsync("/Person", "Logged Success Person");

            var after = DateTime.UtcNow;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var entry = await GetSingleEntryAsync(_factory, e => e.RequestJson.Contains("Logged Success Person"));
            Assert.Equal(ProcessLogLevel.Information, entry.Level);
            Assert.Equal("CreatePerson", entry.Operation);
            Assert.Equal("Successful", entry.Message);
            Assert.Equal("""{"name":"Logged Success Person"}""", entry.RequestJson);
            Assert.Null(entry.Exception);
            Assert.InRange(entry.TimestampUtc, before, after);
            Assert.InRange(entry.DurationMs, 0, (long)(after - before).TotalMilliseconds + 1);
        }

        [Fact]
        public async Task CreatePerson_RejectedByPreProcessor_LogsWarning()
        {
            (await _client.PostAsJsonAsync("/Person", "Logged Duplicate Person")).EnsureSuccessStatusCode();

            var response = await _client.PostAsJsonAsync("/Person", "Logged Duplicate Person");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var entry = await GetSingleEntryAsync(_factory, e => e.Level == ProcessLogLevel.Warning && e.RequestJson.Contains("Logged Duplicate Person"));
            Assert.Equal("CreatePerson", entry.Operation);
            Assert.Equal("A person named 'Logged Duplicate Person' already exists.", entry.Message);
            Assert.Contains("BadHttpRequestException", entry.Exception);
        }

        [Fact]
        public async Task GetPeople_UnexpectedError_LogsErrorWithGenericMessage()
        {
            // This test breaks its database, so it uses its own instead of the shared fixture's.
            using var factory = new StargateApiFactory();
            var client = factory.CreateClient();
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
                await context.Database.ExecuteSqlRawAsync("DROP TABLE [AstronautDetail]");
            }

            var response = await client.GetAsync("/Person");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BaseResponse>();
            var entry = await GetSingleEntryAsync(factory, e => e.Operation == "GetPeople");
            Assert.Equal(ProcessLogLevel.Error, entry.Level);
            Assert.Equal("An unexpected error occurred.", entry.Message);
            Assert.Equal(body?.Message, entry.Message);
            Assert.Contains("no such table: AstronautDetail", entry.Exception);
        }

        [Fact]
        public async Task CreatePerson_NameWithSurroundingSpaces_LogsRequestAsSent()
        {
            (await _client.PostAsJsonAsync("/Person", "  Logged Spaced Person  ")).EnsureSuccessStatusCode();

            var entry = await GetSingleEntryAsync(_factory, e => e.RequestJson.Contains("Logged Spaced Person"));
            Assert.Equal("""{"name":"  Logged Spaced Person  "}""", entry.RequestJson);
        }

        [Fact]
        public async Task CreatePerson_VeryLongName_LogsCappedRequest()
        {
            var response = await _client.PostAsJsonAsync("/Person", new string('q', 5000));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var entry = await GetSingleEntryAsync(_factory, e => e.RequestJson.StartsWith("""{"name":"qqq"""));
            Assert.Equal(ProcessLogLevel.Warning, entry.Level);
            Assert.Equal(4000 + "... (truncated)".Length, entry.RequestJson.Length);
            Assert.EndsWith("... (truncated)", entry.RequestJson);
        }

        [Fact]
        public async Task CreateAstronautDuty_FailsWithPendingChanges_LogsWithoutSavingThem()
        {
            // Step 31's race, set up as data: the newest duty is closed and an older one is still open. The handler closes
            // the newest duty and queues the new one before the database refuses it, so the request's DbContext still
            // holds those changes when the log entry is written.
            (await _client.PostAsJsonAsync("/Person", "Logged Race Person")).EnsureSuccessStatusCode();
            using (var arrangeScope = _factory.Services.CreateScope())
            {
                var context = arrangeScope.ServiceProvider.GetRequiredService<StargateContext>();
                var person = await context.People.SingleAsync(p => p.Name == "Logged Race Person");
                context.AstronautDetails.Add(new AstronautDetail { PersonId = person.Id, CurrentRank = "CPT", CurrentDutyTitle = "Navigator", CareerStartDate = new DateTime(2019, 1, 1) });
                context.AstronautDuties.Add(new AstronautDuty { PersonId = person.Id, Rank = "CPT", DutyTitle = "Pilot", DutyStartDate = new DateTime(2019, 1, 1) });
                context.AstronautDuties.Add(new AstronautDuty { PersonId = person.Id, Rank = "CPT", DutyTitle = "Navigator", DutyStartDate = new DateTime(2020, 1, 1), DutyEndDate = new DateTime(2020, 6, 30) });
                await context.SaveChangesAsync();
            }

            var response = await _client.PostAsJsonAsync("/AstronautDuty", new CreateAstronautDuty
            {
                Name = "Logged Race Person",
                Rank = "CPT",
                DutyTitle = "Commander",
                DutyStartDate = new DateTime(2021, 1, 1)
            });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var entry = await GetSingleEntryAsync(_factory, e => e.Operation == "CreateAstronautDuty" && e.RequestJson.Contains("Logged Race Person"));
            Assert.Equal(ProcessLogLevel.Warning, entry.Level);
            using var assertScope = _factory.Services.CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<StargateContext>();
            var duties = await assertContext.AstronautDuties.AsNoTracking()
                .Where(d => d.Person.Name == "Logged Race Person")
                .OrderBy(d => d.DutyStartDate)
                .ToListAsync();
            Assert.Equal(2, duties.Count);
            Assert.Equal(new DateTime(2020, 6, 30), duties[1].DutyEndDate);
        }

        [Fact]
        public async Task CreatePerson_LogWriteFails_StillSucceeds()
        {
            // This test breaks its database, so it uses its own instead of the shared fixture's.
            using var factory = new StargateApiFactory();
            var client = factory.CreateClient();
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
                await context.Database.ExecuteSqlRawAsync("DROP TABLE [ProcessLog]");
            }

            var response = await client.PostAsJsonAsync("/Person", "Unlogged Person");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var lookup = await client.GetAsync($"/Person/{Uri.EscapeDataString("Unlogged Person")}");
            Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        }

        [Fact]
        public async Task CreatePerson_CancelledRequest_LogsWarning()
        {
            // Over HTTP the request finishes too fast to cancel, so it is sent through MediatR with a cancelled token.
            using (var scope = _factory.Services.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mediator.Send(new CreatePerson { Name = "Logged Cancelled Person" }, CancelledToken));
            }

            var entry = await GetSingleEntryAsync(_factory, e => e.RequestJson.Contains("Logged Cancelled Person"));
            Assert.Equal(ProcessLogLevel.Warning, entry.Level);
            Assert.Equal("The request was cancelled.", entry.Message);
            Assert.Contains("CanceledException", entry.Exception);
        }

        [Fact]
        public async Task ProcessLoggingBehavior_UnusualRequest_StillRunsAndLogs()
        {
            // Every request type today is plain data answered with BaseResponse. A new one that is neither must not fail
            // just because it is logged.
            var behavior = ActivatorUtilities.CreateInstance<ProcessLoggingBehavior<UnserializableRequest, string>>(_factory.Services);

            var response = await behavior.Handle(new UnserializableRequest(), () => Task.FromResult("Done"), CancellationToken.None);

            Assert.Equal("Done", response);
            var entry = await GetSingleEntryAsync(_factory, e => e.Operation == nameof(UnserializableRequest));
            Assert.Equal(ProcessLogLevel.Information, entry.Level);
            Assert.Equal("Successful", entry.Message);
            Assert.Equal("(the request could not be serialized)", entry.RequestJson);
        }

        [Fact]
        public async Task ProcessLoggingBehavior_CancelledWithoutCaller_LogsError()
        {
            // Only the caller going away counts as a cancelled request; anything else cancelling it, such as a timeout
            // inside the API, is our failure.
            var behavior = ActivatorUtilities.CreateInstance<ProcessLoggingBehavior<TimedOutRequest, BaseResponse>>(_factory.Services);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => behavior.Handle(new TimedOutRequest(), () => Task.FromException<BaseResponse>(new OperationCanceledException()), CancellationToken.None));

            var entry = await GetSingleEntryAsync(_factory, e => e.Operation == nameof(TimedOutRequest));
            Assert.Equal(ProcessLogLevel.Error, entry.Level);
            Assert.Equal("An unexpected error occurred.", entry.Message);
        }

        private static async Task<ProcessLog> GetSingleEntryAsync(StargateApiFactory factory, Predicate<ProcessLog> match)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            var entries = await context.ProcessLogs.AsNoTracking().ToListAsync();
            return Assert.Single(entries, match);
        }

        private sealed class UnserializableRequest : IRequest<string>
        {
            public string Value => throw new InvalidOperationException("This value can't be read.");
        }

        private sealed class TimedOutRequest : IRequest<BaseResponse>
        {
        }
    }
}
