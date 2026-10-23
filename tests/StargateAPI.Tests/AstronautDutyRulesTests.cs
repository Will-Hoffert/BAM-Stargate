using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;

namespace StargateAPI.Tests
{
    // Rules for adding an Astronaut Duty. Each test uses its own people.
    public class AstronautDutyRulesTests : IClassFixture<StargateApiFactory>
    {
        private readonly StargateApiFactory _factory;
        private readonly HttpClient _client;

        public AstronautDutyRulesTests(StargateApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreateAstronautDuty_SameDutyForTwoPeople_AddsBoth()
        {
            await CreatePersonAsync("Shared Duty One");
            await CreatePersonAsync("Shared Duty Two");

            var first = await PostDutyAsync("Shared Duty One", "Commander", new DateTime(2020, 1, 1));
            var second = await PostDutyAsync("Shared Duty Two", "Commander", new DateTime(2020, 1, 1));

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        [Fact]
        public async Task CreateAstronautDuty_ExactDuplicate_ReturnsConflict()
        {
            await CreatePersonAsync("Duplicate Duty Person");
            (await PostDutyAsync("Duplicate Duty Person", "Pilot", new DateTime(2020, 3, 1))).EnsureSuccessStatusCode();

            var response = await PostDutyAsync("Duplicate Duty Person", "Pilot", new DateTime(2020, 3, 1));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BaseResponse>();
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal("'Duplicate Duty Person' already has a Pilot duty starting 2020-03-01.", body.Message);
        }

        [Fact]
        public async Task CreateAstronautDuty_DuplicateSentWithTimeOfDay_ReturnsConflictAndKeepsOneDuty()
        {
            // Start dates are stored without the time of day, so a resent duty must still match.
            await CreatePersonAsync("Retried Duty Person");
            (await PostDutyAsync("Retried Duty Person", "Engineer", new DateTime(2020, 5, 1, 8, 30, 0))).EnsureSuccessStatusCode();

            var response = await PostDutyAsync("Retried Duty Person", "Engineer", new DateTime(2020, 5, 1, 8, 30, 0));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            var duty = await context.AstronautDuties.AsNoTracking().SingleAsync(d => d.Person.Name == "Retried Duty Person");
            Assert.Null(duty.DutyEndDate);
        }

        private async Task CreatePersonAsync(string name)
        {
            var response = await _client.PostAsJsonAsync("/Person", name);
            response.EnsureSuccessStatusCode();
        }

        private Task<HttpResponseMessage> PostDutyAsync(string name, string dutyTitle, DateTime dutyStartDate)
        {
            return _client.PostAsJsonAsync("/AstronautDuty", new CreateAstronautDuty
            {
                Name = name,
                Rank = "CPT",
                DutyTitle = dutyTitle,
                DutyStartDate = dutyStartDate
            });
        }
    }
}
