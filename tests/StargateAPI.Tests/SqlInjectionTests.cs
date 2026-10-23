using System.Net;
using System.Net.Http.Json;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Queries;

namespace StargateAPI.Tests
{
    // Names reach SQL through every lookup, so a name containing a quote must be
    // treated as data, never as part of the query. Each test uses its own person.
    public class SqlInjectionTests : IClassFixture<StargateApiFactory>
    {
        private readonly HttpClient _client;

        public SqlInjectionTests(StargateApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetPersonByName_NameWithApostrophe_ReturnsPerson()
        {
            await CreatePersonAsync("Miles O'Brien");

            var response = await _client.GetAsync($"/Person/{Uri.EscapeDataString("Miles O'Brien")}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<GetPersonByNameResult>();
            Assert.Equal("Miles O'Brien", body?.Person?.Name);
        }

        [Fact]
        public async Task GetPersonByName_SqlInjectionAttempt_ReturnsNotFound()
        {
            // A person the injected condition would match if the name were spliced into the SQL.
            await CreatePersonAsync("Injection Target");

            var response = await _client.GetAsync($"/Person/{Uri.EscapeDataString("' OR 1=1 OR '")}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateAstronautDuty_NameWithApostrophe_CreatesDuty()
        {
            await CreatePersonAsync("Jack O'Neill");

            var response = await _client.PostAsJsonAsync("/AstronautDuty", new CreateAstronautDuty
            {
                Name = "Jack O'Neill",
                Rank = "COL",
                DutyTitle = "Commander",
                DutyStartDate = new DateTime(2020, 1, 1)
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetAstronautDutiesByName_NameWithApostrophe_ReturnsDuties()
        {
            await CreatePersonAsync("Teal'c");
            var dutyResponse = await _client.PostAsJsonAsync("/AstronautDuty", new CreateAstronautDuty
            {
                Name = "Teal'c",
                Rank = "CIV",
                DutyTitle = "Pilot",
                DutyStartDate = new DateTime(2021, 5, 1)
            });
            dutyResponse.EnsureSuccessStatusCode();

            var response = await _client.GetAsync($"/AstronautDuty/{Uri.EscapeDataString("Teal'c")}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<GetAstronautDutiesByNameResult>();
            Assert.NotNull(body);
            Assert.Equal("Teal'c", body.Person.Name);
            Assert.Single(body.AstronautDuties);
        }

        private async Task CreatePersonAsync(string name)
        {
            var response = await _client.PostAsJsonAsync("/Person", name);
            response.EnsureSuccessStatusCode();
        }
    }
}
