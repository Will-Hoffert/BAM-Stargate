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
