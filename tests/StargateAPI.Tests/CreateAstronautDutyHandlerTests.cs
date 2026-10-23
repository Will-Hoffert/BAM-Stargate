using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests
{
    // Calls CreateAstronautDutyHandler directly, for cases CreateAstronautDutyPreProcessor
    // keeps out of reach over HTTP. Each test uses its own person.
    public class CreateAstronautDutyHandlerTests : IClassFixture<StargateApiFactory>
    {
        private readonly StargateApiFactory _factory;
        private readonly HttpClient _client;

        public CreateAstronautDutyHandlerTests(StargateApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreateAstronautDutyHandler_UnknownPerson_ThrowsNotFound()
        {
            // The pre-processor checks the person exists, but the person can change before the handler runs.
            using var scope = _factory.Services.CreateScope();
            var handler = new CreateAstronautDutyHandler(scope.ServiceProvider.GetRequiredService<StargateContext>());

            var exception = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => handler.Handle(DutyRequest("Nobody By This Name", "Commander", new DateTime(2020, 1, 1)), CancellationToken.None));

            Assert.Equal(StatusCodes.Status404NotFound, exception.StatusCode);
            Assert.Equal("No person named 'Nobody By This Name' exists.", exception.Message);
        }

        [Fact]
        public async Task CreateAstronautDutyHandler_RowsAlreadyTracked_AddsDuty()
        {
            // The pre-processor shares the handler's DbContext, so a rule check may already have loaded these rows.
            (await _client.PostAsJsonAsync("/Person", "Tracked Rows Person")).EnsureSuccessStatusCode();
            (await _client.PostAsJsonAsync("/AstronautDuty", DutyRequest("Tracked Rows Person", "Pilot", new DateTime(2019, 1, 1)))).EnsureSuccessStatusCode();

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            await context.People
                .Include(p => p.AstronautDetail)
                .Include(p => p.AstronautDuties)
                .SingleAsync(p => p.Name == "Tracked Rows Person");

            await new CreateAstronautDutyHandler(context).Handle(DutyRequest("Tracked Rows Person", "Commander", new DateTime(2021, 7, 1)), CancellationToken.None);

            var duties = await context.AstronautDuties.AsNoTracking()
                .Where(d => d.Person.Name == "Tracked Rows Person")
                .OrderBy(d => d.DutyStartDate)
                .ToListAsync();
            Assert.Equal(2, duties.Count);
            Assert.Equal(new DateTime(2021, 6, 30), duties[0].DutyEndDate);
            Assert.Null(duties[1].DutyEndDate);

            var detail = await context.AstronautDetails.AsNoTracking().SingleAsync(d => d.Person.Name == "Tracked Rows Person");
            Assert.Equal("Commander", detail.CurrentDutyTitle);
        }

        [Fact]
        public async Task CreateAstronautDutyHandler_OtherOpenDutySaved_ThrowsConflictAndRollsBack()
        {
            // Two requests for one person can both read the same current duty before either saves. The one that saves
            // second finds an open duty it never read. Here the newest duty is closed and an older one is still open,
            // so the handler meets that same open duty when it inserts.
            (await _client.PostAsJsonAsync("/Person", "Raced Duty Person")).EnsureSuccessStatusCode();
            using (var arrangeScope = _factory.Services.CreateScope())
            {
                var context = arrangeScope.ServiceProvider.GetRequiredService<StargateContext>();
                var person = await context.People.SingleAsync(p => p.Name == "Raced Duty Person");
                context.AstronautDetails.Add(new AstronautDetail { PersonId = person.Id, CurrentRank = "CPT", CurrentDutyTitle = "Navigator", CareerStartDate = new DateTime(2019, 1, 1) });
                context.AstronautDuties.Add(new AstronautDuty { PersonId = person.Id, Rank = "CPT", DutyTitle = "Pilot", DutyStartDate = new DateTime(2019, 1, 1) });
                context.AstronautDuties.Add(new AstronautDuty { PersonId = person.Id, Rank = "CPT", DutyTitle = "Navigator", DutyStartDate = new DateTime(2020, 1, 1), DutyEndDate = new DateTime(2020, 6, 30) });
                await context.SaveChangesAsync();
            }

            using (var actScope = _factory.Services.CreateScope())
            {
                var handler = new CreateAstronautDutyHandler(actScope.ServiceProvider.GetRequiredService<StargateContext>());

                var exception = await Assert.ThrowsAsync<BadHttpRequestException>(
                    () => handler.Handle(DutyRequest("Raced Duty Person", "Commander", new DateTime(2021, 1, 1)), CancellationToken.None));

                Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
                Assert.Equal("Another duty for 'Raced Duty Person' was saved at the same time. Check their current duty and try again.", exception.Message);
            }

            // Closing the newest duty and updating the detail were saved before the insert failed, and must be rolled back.
            using var assertScope = _factory.Services.CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<StargateContext>();
            var duties = await assertContext.AstronautDuties.AsNoTracking()
                .Where(d => d.Person.Name == "Raced Duty Person")
                .OrderBy(d => d.DutyStartDate)
                .ToListAsync();
            Assert.Equal(2, duties.Count);
            Assert.Equal(new DateTime(2020, 6, 30), duties[1].DutyEndDate);
            var detail = await assertContext.AstronautDetails.AsNoTracking().SingleAsync(d => d.Person.Name == "Raced Duty Person");
            Assert.Equal("Navigator", detail.CurrentDutyTitle);
        }

        private static CreateAstronautDuty DutyRequest(string name, string dutyTitle, DateTime dutyStartDate)
        {
            return new CreateAstronautDuty
            {
                Name = name,
                Rank = "CPT",
                DutyTitle = dutyTitle,
                DutyStartDate = dutyStartDate
            };
        }
    }
}
