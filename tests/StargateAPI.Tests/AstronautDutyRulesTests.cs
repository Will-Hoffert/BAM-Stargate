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

        [Fact]
        public async Task CreateAstronautDuty_StartsBeforeCurrentDuty_ReturnsConflict()
        {
            await CreatePersonAsync("Backdated Duty Person");
            (await PostDutyAsync("Backdated Duty Person", "Pilot", new DateTime(2020, 1, 1))).EnsureSuccessStatusCode();
            (await PostDutyAsync("Backdated Duty Person", "Commander", new DateTime(2022, 6, 1))).EnsureSuccessStatusCode();

            var response = await PostDutyAsync("Backdated Duty Person", "Engineer", new DateTime(2021, 1, 1));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BaseResponse>();
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal("A new duty for 'Backdated Duty Person' must start after their current duty, which started 2022-06-01.", body.Message);

            var duties = await GetDutiesOldestFirstAsync("Backdated Duty Person");
            Assert.Equal(2, duties.Count);
            Assert.Equal("Commander", duties[1].DutyTitle);
            Assert.Null(duties[1].DutyEndDate);
        }

        [Fact]
        public async Task CreateAstronautDuty_StartsSameDayAsCurrentDuty_ReturnsConflict()
        {
            await CreatePersonAsync("Same Day Duty Person");
            (await PostDutyAsync("Same Day Duty Person", "Pilot", new DateTime(2020, 1, 1))).EnsureSuccessStatusCode();

            // Sent with a time of day, which must not make it count as starting later.
            var response = await PostDutyAsync("Same Day Duty Person", "Commander", new DateTime(2020, 1, 1, 14, 0, 0));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var duty = Assert.Single(await GetDutiesOldestFirstAsync("Same Day Duty Person"));
            Assert.Equal("Pilot", duty.DutyTitle);
            Assert.Null(duty.DutyEndDate);
        }

        [Fact]
        public async Task CreateAstronautDuty_StartsDayAfterCurrentDuty_ClosesCurrentDutyThatDay()
        {
            await CreatePersonAsync("Next Day Duty Person");
            (await PostDutyAsync("Next Day Duty Person", "Pilot", new DateTime(2020, 1, 1))).EnsureSuccessStatusCode();

            var response = await PostDutyAsync("Next Day Duty Person", "Commander", new DateTime(2020, 1, 2));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var duties = await GetDutiesOldestFirstAsync("Next Day Duty Person");
            Assert.Equal(2, duties.Count);
            Assert.Equal(new DateTime(2020, 1, 1), duties[0].DutyEndDate);
            Assert.Null(duties[1].DutyEndDate);
        }

        [Fact]
        public async Task CreateAstronautDuty_RetiredInMixedCase_RetiresPerson()
        {
            await CreatePersonAsync("Mixed Case Retiree");
            (await PostDutyAsync("Mixed Case Retiree", "Engineer", new DateTime(2018, 3, 1))).EnsureSuccessStatusCode();

            var response = await PostDutyAsync("Mixed Case Retiree", "Retired", new DateTime(2024, 3, 1));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            var detail = await context.AstronautDetails.AsNoTracking().SingleAsync(d => d.Person.Name == "Mixed Case Retiree");
            Assert.Equal(new DateTime(2024, 2, 29), detail.CareerEndDate);
            Assert.Equal("RETIRED", detail.CurrentDutyTitle);

            var duties = await GetDutiesOldestFirstAsync("Mixed Case Retiree");
            Assert.Equal("RETIRED", duties[1].DutyTitle);
        }

        [Fact]
        public async Task CreateAstronautDuty_RetiredAsFirstDuty_ReturnsBadRequest()
        {
            await CreatePersonAsync("Never Served Person");

            var response = await PostDutyAsync("Never Served Person", "RETIRED", new DateTime(2024, 3, 1));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BaseResponse>();
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal("'Never Served Person' has no astronaut duties yet, so RETIRED can't be their first duty.", body.Message);

            // Rule 2: a person who never had an astronaut assignment has no astronaut records.
            Assert.Empty(await GetDutiesOldestFirstAsync("Never Served Person"));
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            Assert.False(await context.AstronautDetails.AnyAsync(d => d.Person.Name == "Never Served Person"));
        }

        [Fact]
        public async Task CreateAstronautDuty_AfterRetirement_ReturnsConflict()
        {
            await CreatePersonAsync("Retired Person");
            (await PostDutyAsync("Retired Person", "Engineer", new DateTime(2018, 3, 1))).EnsureSuccessStatusCode();
            (await PostDutyAsync("Retired Person", "RETIRED", new DateTime(2024, 3, 1))).EnsureSuccessStatusCode();

            var response = await PostDutyAsync("Retired Person", "Pilot", new DateTime(2025, 1, 1));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BaseResponse>();
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal("'Retired Person' retired on 2024-03-01 and can't be given new duties.", body.Message);

            var duties = await GetDutiesOldestFirstAsync("Retired Person");
            Assert.Equal(2, duties.Count);
            Assert.Equal("RETIRED", duties[1].DutyTitle);
            Assert.Null(duties[1].DutyEndDate);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            var detail = await context.AstronautDetails.AsNoTracking().SingleAsync(d => d.Person.Name == "Retired Person");
            Assert.Equal("RETIRED", detail.CurrentDutyTitle);
            Assert.Equal(new DateTime(2024, 2, 29), detail.CareerEndDate);
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

        private async Task<List<AstronautDuty>> GetDutiesOldestFirstAsync(string name)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            return await context.AstronautDuties.AsNoTracking()
                .Where(d => d.Person.Name == name)
                .OrderBy(d => d.DutyStartDate)
                .ToListAsync();
        }
    }
}
