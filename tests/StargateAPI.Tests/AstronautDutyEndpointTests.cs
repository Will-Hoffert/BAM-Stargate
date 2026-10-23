using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;
using StargateAPI.Business.Queries;
using StargateAPI.Controllers;

namespace StargateAPI.Tests
{
    // These tests share one database per class, so each test uses its own person
    // and its own duty dates.
    public class AstronautDutyEndpointTests : IClassFixture<StargateApiFactory>
    {
        private readonly StargateApiFactory _factory;
        private readonly HttpClient _client;

        public AstronautDutyEndpointTests(StargateApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreateAstronautDuty_FirstDuty_CreatesAstronautDetail()
        {
            var personId = await CreatePersonAsync("First Duty Person");

            await AddDutyAsync("First Duty Person", "1LT", "Commander", new DateTime(2020, 1, 1));

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();

            var detail = await context.AstronautDetails.AsNoTracking().SingleAsync(d => d.PersonId == personId);
            Assert.Equal("1LT", detail.CurrentRank);
            Assert.Equal("Commander", detail.CurrentDutyTitle);
            Assert.Equal(new DateTime(2020, 1, 1), detail.CareerStartDate);
            Assert.Null(detail.CareerEndDate);

            var duty = await context.AstronautDuties.AsNoTracking().SingleAsync(d => d.PersonId == personId);
            Assert.Equal(new DateTime(2020, 1, 1), duty.DutyStartDate);
            Assert.Null(duty.DutyEndDate);
        }

        [Fact]
        public async Task CreateAstronautDuty_NextDuty_ClosesPreviousDutyTheDayBefore()
        {
            var personId = await CreatePersonAsync("Next Duty Person");

            await AddDutyAsync("Next Duty Person", "1LT", "Pilot", new DateTime(2021, 1, 1));
            await AddDutyAsync("Next Duty Person", "CPT", "Commander", new DateTime(2022, 6, 1));

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();

            var duties = await context.AstronautDuties.AsNoTracking()
                .Where(d => d.PersonId == personId)
                .OrderBy(d => d.DutyStartDate)
                .ToListAsync();
            Assert.Equal(2, duties.Count);
            Assert.Equal(new DateTime(2022, 5, 31), duties[0].DutyEndDate);
            Assert.Null(duties[1].DutyEndDate);

            var detail = await context.AstronautDetails.AsNoTracking().SingleAsync(d => d.PersonId == personId);
            Assert.Equal("CPT", detail.CurrentRank);
            Assert.Equal("Commander", detail.CurrentDutyTitle);
            Assert.Equal(new DateTime(2021, 1, 1), detail.CareerStartDate);
        }

        [Fact]
        public async Task CreateAstronautDuty_RetiredAfterCareer_SetsCareerEndDateTheDayBefore()
        {
            var personId = await CreatePersonAsync("Retiring Person");

            await AddDutyAsync("Retiring Person", "MAJ", "Engineer", new DateTime(2018, 3, 1));
            await AddDutyAsync("Retiring Person", "MAJ", "RETIRED", new DateTime(2024, 3, 1));

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();

            var detail = await context.AstronautDetails.AsNoTracking().SingleAsync(d => d.PersonId == personId);
            Assert.Equal("RETIRED", detail.CurrentDutyTitle);
            Assert.Equal(new DateTime(2024, 2, 29), detail.CareerEndDate);
        }

        [Fact]
        public async Task GetAstronautDutiesByName_PersonWithDuties_ReturnsDutiesNewestFirst()
        {
            await CreatePersonAsync("Duty History Person");
            await AddDutyAsync("Duty History Person", "1LT", "Pilot", new DateTime(2019, 1, 1));
            await AddDutyAsync("Duty History Person", "CPT", "Commander", new DateTime(2021, 6, 1));

            var response = await _client.GetAsync($"/AstronautDuty/{Uri.EscapeDataString("Duty History Person")}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<GetAstronautDutiesByNameResult>();
            Assert.NotNull(body);
            Assert.Equal("Duty History Person", body.Person.Name);
            Assert.Collection(body.AstronautDuties,
                duty =>
                {
                    Assert.Equal("Commander", duty.DutyTitle);
                    Assert.Null(duty.DutyEndDate);
                },
                duty =>
                {
                    Assert.Equal("Pilot", duty.DutyTitle);
                    Assert.Equal(new DateTime(2021, 5, 31), duty.DutyEndDate);
                });
        }

        [Fact]
        public async Task GetAstronautDutiesByName_PersonWithDuty_ReturnsOnlyDutyFields()
        {
            await CreatePersonAsync("Duty Shape Person");
            await AddDutyAsync("Duty Shape Person", "2LT", "Navigator", new DateTime(2017, 4, 1));

            var response = await _client.GetAsync($"/AstronautDuty/{Uri.EscapeDataString("Duty Shape Person")}");

            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var duty = Assert.Single(json.RootElement.GetProperty("astronautDuties").EnumerateArray());
            Assert.Equal(
                new HashSet<string> { "id", "rank", "dutyTitle", "dutyStartDate", "dutyEndDate" },
                duty.EnumerateObject().Select(p => p.Name).ToHashSet());
        }

        [Fact]
        public async Task GetAstronautDutiesByName_UnknownPerson_ReturnsNotFound()
        {
            var response = await _client.GetAsync($"/AstronautDuty/{Uri.EscapeDataString("Nobody By This Name")}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BaseResponse>();
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal("No person named 'Nobody By This Name' exists.", body.Message);
        }

        private async Task<int> CreatePersonAsync(string name)
        {
            var response = await _client.PostAsJsonAsync("/Person", name);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CreatePersonResult>();
            return result!.Id;
        }

        private async Task AddDutyAsync(string name, string rank, string dutyTitle, DateTime dutyStartDate)
        {
            var response = await _client.PostAsJsonAsync("/AstronautDuty", new CreateAstronautDuty
            {
                Name = name,
                Rank = rank,
                DutyTitle = dutyTitle,
                DutyStartDate = dutyStartDate
            });
            response.EnsureSuccessStatusCode();
        }
    }
}
