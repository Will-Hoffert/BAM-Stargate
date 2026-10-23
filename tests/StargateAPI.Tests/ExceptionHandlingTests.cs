using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;

namespace StargateAPI.Tests
{
    // Every failure comes back as the BaseResponse envelope with a matching status code,
    // and unexpected failures never expose internal details such as SQL.
    public class ExceptionHandlingTests : IClassFixture<StargateApiFactory>
    {
        private readonly HttpClient _client;

        public ExceptionHandlingTests(StargateApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreateAstronautDuty_UnknownPerson_ReturnsBadRequestEnvelope()
        {
            var response = await _client.PostAsJsonAsync("/AstronautDuty", new CreateAstronautDuty
            {
                Name = "Nobody By This Name",
                Rank = "1LT",
                DutyTitle = "Commander",
                DutyStartDate = new DateTime(2020, 1, 1)
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BaseResponse>();
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal((int)HttpStatusCode.BadRequest, body.ResponseCode);
        }

        [Fact]
        public async Task GetPeople_DatabaseFailure_ReturnsGenericServerError()
        {
            // This test breaks its database, so it uses its own instead of the shared fixture's.
            using var factory = new StargateApiFactory();
            var client = factory.CreateClient();
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
                await context.Database.ExecuteSqlRawAsync("DROP TABLE [AstronautDetail]");
            }

            var response = await client.GetAsync("/Person");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BaseResponse>();
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal((int)HttpStatusCode.InternalServerError, body.ResponseCode);
            Assert.DoesNotContain("SQLite", body.Message);
            Assert.DoesNotContain("AstronautDetail", body.Message);
        }
    }
}
