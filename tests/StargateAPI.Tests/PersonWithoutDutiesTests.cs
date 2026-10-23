using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace StargateAPI.Tests
{
    // Rule 2: someone who has had no astronaut assignment has no astronaut records, so every astronaut field comes back
    // null, not as an empty value a client could mistake for a real one. Each test adds its own person, because tests in
    // a class share a database.
    public class PersonWithoutDutiesTests : IClassFixture<StargateApiFactory>
    {
        private static readonly string[] AstronautFields = { "currentRank", "currentDutyTitle", "careerStartDate", "careerEndDate" };

        private readonly HttpClient _client;

        public PersonWithoutDutiesTests(StargateApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetPersonByName_NoDuties_ReturnsNullAstronautFields()
        {
            await AddPersonAsync("Zed Park");

            using var document = await GetJsonAsync($"/Person/{Uri.EscapeDataString("Zed Park")}");

            AssertNoAstronautFields(document.RootElement.GetProperty("person"));
        }

        [Fact]
        public async Task GetPeople_NoDuties_ReturnsNullAstronautFields()
        {
            await AddPersonAsync("Ann Lee");

            using var document = await GetJsonAsync("/Person");

            var person = document.RootElement.GetProperty("people").EnumerateArray()
                .Single(p => p.GetProperty("name").GetString() == "Ann Lee");
            AssertNoAstronautFields(person);
        }

        [Fact]
        public async Task GetAstronautDutiesByName_NoDuties_ReturnsNullAstronautFieldsAndNoDuties()
        {
            await AddPersonAsync("Bo Chen");

            using var document = await GetJsonAsync($"/AstronautDuty/{Uri.EscapeDataString("Bo Chen")}");

            AssertNoAstronautFields(document.RootElement.GetProperty("person"));
            Assert.Empty(document.RootElement.GetProperty("astronautDuties").EnumerateArray());
        }

        private async Task AddPersonAsync(string name)
        {
            var response = await _client.PostAsJsonAsync("/Person", name);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private async Task<JsonDocument> GetJsonAsync(string url)
        {
            var response = await _client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }

        // Compares field by field, so a failure names the fields that weren't null and what they were instead.
        private static void AssertNoAstronautFields(JsonElement person)
        {
            var expected = AstronautFields.ToDictionary(field => field, _ => JsonValueKind.Null);
            var actual = AstronautFields.ToDictionary(field => field, field => person.GetProperty(field).ValueKind);
            Assert.Equal(expected, actual);
        }
    }
}
