using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;
using StargateAPI.Business.Queries;
using StargateAPI.Controllers;

namespace StargateAPI.Tests
{
    // Rule 1: a Person is uniquely identified by their Name. Each test uses its own names.
    public class PersonRulesTests : IClassFixture<StargateApiFactory>
    {
        private readonly StargateApiFactory _factory;
        private readonly HttpClient _client;

        public PersonRulesTests(StargateApiFactory factory)
        {
            _factory = factory;
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

        [Fact]
        public async Task CreatePersonHandler_NameSavedByConcurrentRequest_ThrowsConflict()
        {
            // Two requests racing for one name can both pass CreatePersonPreProcessor before either
            // saves. Calling the handler directly, once per request scope, replays that race.
            await CreatePersonDirectlyAsync("Racing Person");

            var exception = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => CreatePersonDirectlyAsync("Racing Person"));

            Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
            Assert.Equal("A person named 'Racing Person' already exists.", exception.Message);
        }

        private async Task CreatePersonDirectlyAsync(string name)
        {
            using var scope = _factory.Services.CreateScope();
            var handler = new CreatePersonHandler(scope.ServiceProvider.GetRequiredService<StargateContext>());
            await handler.Handle(new CreatePerson { Name = name }, CancellationToken.None);
        }
    }
}
