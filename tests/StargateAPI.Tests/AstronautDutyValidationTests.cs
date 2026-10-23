using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;

namespace StargateAPI.Tests
{
    // Checks on the values in an Astronaut Duty request itself, before any rule about the person's history.
    // Each test uses its own person.
    public class AstronautDutyValidationTests : IClassFixture<StargateApiFactory>
    {
        private readonly StargateApiFactory _factory;
        private readonly HttpClient _client;

        public AstronautDutyValidationTests(StargateApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAstronautDuty_BlankRank_ReturnsBadRequest(string rank)
        {
            var name = $"Blank Rank Person {rank.Length}";
            await CreatePersonAsync(name);

            var response = await PostDutyAsync(name, rank, "Pilot", new DateTime(2020, 1, 1));

            await AssertBadRequestAsync(response, "A rank is required.");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAstronautDuty_BlankDutyTitle_ReturnsBadRequest(string dutyTitle)
        {
            var name = $"Blank Title Person {dutyTitle.Length}";
            await CreatePersonAsync(name);

            var response = await PostDutyAsync(name, "CPT", dutyTitle, new DateTime(2020, 1, 1));

            await AssertBadRequestAsync(response, "A duty title is required.");
        }

        [Fact]
        public async Task CreateAstronautDuty_DutyTitleTooLong_ReturnsBadRequest()
        {
            await CreatePersonAsync("Long Title Person");

            var response = await PostDutyAsync("Long Title Person", "CPT", new string('x', 101), new DateTime(2020, 1, 1));

            await AssertBadRequestAsync(response, "A duty title can't be longer than 100 characters.");
        }

        [Fact]
        public async Task CreateAstronautDuty_MissingStartDate_ReturnsBadRequestAndSavesNothing()
        {
            await CreatePersonAsync("No Start Date Person");

            var response = await PostJsonAsync("""{ "name": "No Start Date Person", "rank": "CPT", "dutyTitle": "Pilot" }""");

            await AssertBadRequestAsync(response, "A duty start date is required.");
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            Assert.False(await context.AstronautDuties.AnyAsync(d => d.Person.Name == "No Start Date Person"));
            Assert.False(await context.AstronautDetails.AnyAsync(d => d.Person.Name == "No Start Date Person"));
        }

        [Fact]
        public async Task CreateAstronautDuty_MissingRankProperty_ReturnsBadRequest()
        {
            await CreatePersonAsync("No Rank Person");

            var response = await PostJsonAsync("""{ "name": "No Rank Person", "dutyTitle": "Pilot", "dutyStartDate": "2020-01-01" }""");

            await AssertBadRequestAsync(response, "A rank is required.");
        }

        [Fact]
        public async Task CreateAstronautDuty_BlankName_ReturnsBadRequest()
        {
            var response = await PostDutyAsync("   ", "CPT", "Pilot", new DateTime(2020, 1, 1));

            await AssertBadRequestAsync(response, "A person's name is required.");
        }

        [Fact]
        public async Task CreateAstronautDuty_ValuesWithSurroundingSpaces_StoresTrimmedValues()
        {
            await CreatePersonAsync("Spaced Values Person");

            var response = await PostDutyAsync(" Spaced Values Person ", "  CPT  ", "  Pilot  ", new DateTime(2020, 1, 1));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            var duty = await context.AstronautDuties.AsNoTracking().SingleAsync(d => d.Person.Name == "Spaced Values Person");
            Assert.Equal("CPT", duty.Rank);
            Assert.Equal("Pilot", duty.DutyTitle);
            var detail = await context.AstronautDetails.AsNoTracking().SingleAsync(d => d.Person.Name == "Spaced Values Person");
            Assert.Equal("CPT", detail.CurrentRank);
            Assert.Equal("Pilot", detail.CurrentDutyTitle);
        }

        [Fact]
        public async Task CreateAstronautDuty_RetiredWithSurroundingSpaces_RetiresPerson()
        {
            await CreatePersonAsync("Spaced Retiree");
            (await PostDutyAsync("Spaced Retiree", "MAJ", "Engineer", new DateTime(2018, 3, 1))).EnsureSuccessStatusCode();

            var response = await PostDutyAsync("Spaced Retiree", "MAJ", " RETIRED ", new DateTime(2024, 3, 1));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            var detail = await context.AstronautDetails.AsNoTracking().SingleAsync(d => d.Person.Name == "Spaced Retiree");
            Assert.Equal("RETIRED", detail.CurrentDutyTitle);
            Assert.Equal(new DateTime(2024, 2, 29), detail.CareerEndDate);
        }

        private async Task CreatePersonAsync(string name)
        {
            var response = await _client.PostAsJsonAsync("/Person", name);
            response.EnsureSuccessStatusCode();
        }

        private Task<HttpResponseMessage> PostDutyAsync(string name, string rank, string dutyTitle, DateTime dutyStartDate)
        {
            return _client.PostAsJsonAsync("/AstronautDuty", new CreateAstronautDuty
            {
                Name = name,
                Rank = rank,
                DutyTitle = dutyTitle,
                DutyStartDate = dutyStartDate
            });
        }

        private Task<HttpResponseMessage> PostJsonAsync(string json)
        {
            return _client.PostAsync("/AstronautDuty", new StringContent(json, Encoding.UTF8, "application/json"));
        }

        private static async Task AssertBadRequestAsync(HttpResponseMessage response, string expectedMessage)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BaseResponse>();
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal(expectedMessage, body.Message);
        }
    }
}
