using System.Net;
using System.Net.Http.Json;
using StargateAPI.Business.Queries;
using StargateAPI.Controllers;

namespace StargateAPI.Tests
{
    public class PersonEndpointTests : IClassFixture<StargateApiFactory>
    {
        private readonly HttpClient _client;

        public PersonEndpointTests(StargateApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetPeople_EmptyDatabase_ReturnsOkWithNoPeople()
        {
            var response = await _client.GetAsync("/Person");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<GetPeopleResult>();
            Assert.NotNull(body);
            Assert.True(body.Success);
            Assert.Empty(body.People);
        }

        [Fact]
        public async Task GetPersonByName_UnknownPerson_ReturnsNotFound()
        {
            var response = await _client.GetAsync($"/Person/{Uri.EscapeDataString("Nobody By This Name")}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BaseResponse>();
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal("No person named 'Nobody By This Name' exists.", body.Message);
        }
    }
}
