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
    // Requirement 3: a person can be renamed with PUT /Person/{name}. Each test uses its own names.
    public class UpdatePersonTests : IClassFixture<StargateApiFactory>
    {
        private readonly StargateApiFactory _factory;
        private readonly HttpClient _client;

        public UpdatePersonTests(StargateApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task UpdatePerson_FreeName_RenamesPerson()
        {
            var personId = await CreatePersonAsync("Original Name Person");

            var response = await PutNameAsync("Original Name Person", "Renamed Person");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<UpdatePersonResult>();
            Assert.NotNull(body);
            Assert.True(body.Success);
            Assert.Equal(personId, body.Id);

            var renamed = await _client.GetFromJsonAsync<GetPersonByNameResult>($"/Person/{Uri.EscapeDataString("Renamed Person")}");
            Assert.Equal(personId, renamed?.Person?.PersonId);
            var oldName = await _client.GetAsync($"/Person/{Uri.EscapeDataString("Original Name Person")}");
            Assert.Equal(HttpStatusCode.NotFound, oldName.StatusCode);
        }

        [Fact]
        public async Task UpdatePerson_PersonWithDuties_KeepsDuties()
        {
            await CreatePersonAsync("Duty Holder Before");
            (await _client.PostAsJsonAsync("/AstronautDuty", new CreateAstronautDuty
            {
                Name = "Duty Holder Before",
                Rank = "CPT",
                DutyTitle = "Pilot",
                DutyStartDate = new DateTime(2020, 1, 1)
            })).EnsureSuccessStatusCode();

            var response = await PutNameAsync("Duty Holder Before", "Duty Holder After");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var duties = await _client.GetFromJsonAsync<GetAstronautDutiesByNameResult>($"/AstronautDuty/{Uri.EscapeDataString("Duty Holder After")}");
            Assert.NotNull(duties);
            Assert.Equal("Duty Holder After", duties.Person?.Name);
            Assert.Equal("Pilot", duties.Person?.CurrentDutyTitle);
            var duty = Assert.Single(duties.AstronautDuties);
            Assert.Equal("Pilot", duty.DutyTitle);
        }

        [Fact]
        public async Task UpdatePerson_UnknownPerson_ReturnsNotFound()
        {
            var response = await PutNameAsync("Nobody By This Name", "Somebody New");

            await AssertFailureAsync(response, HttpStatusCode.NotFound, "No person named 'Nobody By This Name' exists.");
        }

        [Theory]
        [InlineData("Taken Exact Person", "Taken Exact Person")]
        [InlineData("Taken Case Person", "TAKEN CASE PERSON")]
        public async Task UpdatePerson_NameUsedByAnotherPerson_ReturnsConflictAndKeepsName(string takenName, string newName)
        {
            await CreatePersonAsync(takenName);
            var renamerName = $"{takenName} Renamer";
            await CreatePersonAsync(renamerName);

            var response = await PutNameAsync(renamerName, newName);

            await AssertFailureAsync(response, HttpStatusCode.Conflict, $"A person named '{newName}' already exists.");
            Assert.Equal(renamerName, await GetStoredNameAsync(renamerName));
        }

        [Fact]
        public async Task UpdatePerson_DifferentCaseOfOwnName_StoresNewCase()
        {
            await CreatePersonAsync("case fix person");

            var response = await PutNameAsync("case fix person", "Case Fix Person");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Case Fix Person", await GetStoredNameAsync("case fix person"));
        }

        [Fact]
        public async Task UpdatePerson_SameName_ReturnsOkAndKeepsName()
        {
            await CreatePersonAsync("Unchanged Person");

            var response = await PutNameAsync("Unchanged Person", "Unchanged Person");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Unchanged Person", await GetStoredNameAsync("Unchanged Person"));
        }

        [Fact]
        public async Task UpdatePerson_NamesWithSurroundingSpaces_StoresTrimmedName()
        {
            await CreatePersonAsync("Untrimmed Rename Person");

            var response = await PutNameAsync(" Untrimmed Rename Person ", "  Trimmed Rename Person  ");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Trimmed Rename Person", await GetStoredNameAsync("Trimmed Rename Person"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task UpdatePerson_BlankNewName_ReturnsBadRequest(string newName)
        {
            var name = $"Blank Rename Person {newName.Length}";
            await CreatePersonAsync(name);

            var response = await PutNameAsync(name, newName);

            await AssertFailureAsync(response, HttpStatusCode.BadRequest, "A person's name is required.");
        }

        [Fact]
        public async Task UpdatePerson_NewNameTooLong_ReturnsBadRequest()
        {
            await CreatePersonAsync("Long Rename Person");

            var response = await PutNameAsync("Long Rename Person", new string('x', 101));

            await AssertFailureAsync(response, HttpStatusCode.BadRequest, "A person's name can't be longer than 100 characters.");
        }

        [Fact]
        public async Task UpdatePerson_BlankCurrentName_ReturnsBadRequest()
        {
            // ASP.NET binds a blank route value as missing and rejects it before UpdatePersonPreProcessor runs,
            // with its own message, the same as GET /Person/{name}.
            var response = await PutNameAsync("   ", "Anyone New");

            await AssertFailureAsync(response, HttpStatusCode.BadRequest, "The name field is required.");
        }

        [Fact]
        public async Task UpdatePersonHandler_NameSavedByConcurrentRequest_ThrowsConflictAndKeepsName()
        {
            // Two requests claiming one name can both pass UpdatePersonPreProcessor before either saves.
            // Calling the handler directly replays the one that saves second.
            await CreatePersonAsync("Racing Renamer");
            await CreatePersonAsync("Racing Taken Name");

            using (var scope = _factory.Services.CreateScope())
            {
                var handler = new UpdatePersonHandler(scope.ServiceProvider.GetRequiredService<StargateContext>());

                var exception = await Assert.ThrowsAsync<BadHttpRequestException>(
                    () => handler.Handle(new UpdatePerson { Name = "Racing Renamer", NewName = "Racing Taken Name" }, CancellationToken.None));

                Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
                Assert.Equal("A person named 'Racing Taken Name' already exists.", exception.Message);
            }

            Assert.Equal("Racing Renamer", await GetStoredNameAsync("Racing Renamer"));
        }

        [Fact]
        public async Task UpdatePersonHandler_PersonRenamedAway_ThrowsNotFound()
        {
            // UpdatePersonPreProcessor checks the person exists, but another rename can land before the handler runs.
            await CreatePersonAsync("Renamed Away Person");
            (await PutNameAsync("Renamed Away Person", "Renamed Away Person Later")).EnsureSuccessStatusCode();
            using var scope = _factory.Services.CreateScope();
            var handler = new UpdatePersonHandler(scope.ServiceProvider.GetRequiredService<StargateContext>());

            var exception = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => handler.Handle(new UpdatePerson { Name = "Renamed Away Person", NewName = "Anyone New" }, CancellationToken.None));

            Assert.Equal(StatusCodes.Status404NotFound, exception.StatusCode);
            Assert.Equal("No person named 'Renamed Away Person' exists.", exception.Message);
        }

        private async Task<int> CreatePersonAsync(string name)
        {
            var response = await _client.PostAsJsonAsync("/Person", name);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<CreatePersonResult>();
            return body!.Id;
        }

        private Task<HttpResponseMessage> PutNameAsync(string name, string newName)
        {
            return _client.PutAsJsonAsync($"/Person/{Uri.EscapeDataString(name)}", newName);
        }

        // Lookups ignore case, so this returns the name exactly as stored.
        private async Task<string?> GetStoredNameAsync(string name)
        {
            var body = await _client.GetFromJsonAsync<GetPersonByNameResult>($"/Person/{Uri.EscapeDataString(name)}");
            return body?.Person?.Name;
        }

        private static async Task AssertFailureAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedMessage)
        {
            Assert.Equal(expectedStatus, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BaseResponse>();
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal(expectedMessage, body.Message);
        }
    }
}
