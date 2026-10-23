using System.Net;
using System.Net.Http.Json;
using StargateAPI.Business.Queries;
using StargateAPI.Controllers;

namespace StargateAPI.Tests
{
    // Rule 1: a Person is uniquely identified by their Name. Each test uses its own names.
    public class PersonRulesTests : IClassFixture<StargateApiFactory>
    {
        private readonly HttpClient _client;

        public PersonRulesTests(StargateApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreatePerson_DuplicateName_ReturnsConflict()
        {
            var first = await _client.PostAsJsonAsync("/Person", "John Doe");
            first.EnsureSuccessStatusCode();

            var second = await _client.PostAsJsonAsync("/Person", "John Doe");

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            var body = await second.Content.ReadFromJsonAsync<BaseResponse>();
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal("A person named 'John Doe' already exists.", body.Message);

            var people = await _client.GetFromJsonAsync<GetPeopleResult>("/Person");
            Assert.NotNull(people);
            Assert.Single(people.People, p => p.Name == "John Doe");
        }
    }
}
