using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        public async Task CreatePerson_NullName_ReturnsBadRequestEnvelope()
        {
            // ASP.NET rejects a body it can't bind before the controller action runs.
            var response = await _client.PostAsync("/Person", new StringContent("null", Encoding.UTF8, "application/json"));

            var body = await AssertBadRequestEnvelopeAsync(response);
            Assert.Contains("A non-empty request body is required.", body.Message);
        }

        [Fact]
        public async Task UpdatePerson_NullNewName_ReturnsBadRequestEnvelope()
        {
            var response = await _client.PutAsync($"/Person/{Uri.EscapeDataString("Anyone")}", new StringContent("null", Encoding.UTF8, "application/json"));

            var body = await AssertBadRequestEnvelopeAsync(response);
            Assert.Contains("A non-empty request body is required.", body.Message);
        }

        [Fact]
        public async Task CreateAstronautDuty_InvalidDate_ReturnsBadRequestEnvelope()
        {
            var json = """{ "name": "Anyone", "rank": "CPT", "dutyTitle": "Pilot", "dutyStartDate": "not a date" }""";

            var response = await _client.PostAsync("/AstronautDuty", new StringContent(json, Encoding.UTF8, "application/json"));

            var body = await AssertBadRequestEnvelopeAsync(response);
            Assert.Contains("$.dutyStartDate", body.Message);
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

        private static async Task<BaseResponse> AssertBadRequestEnvelopeAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            // Checked on the raw JSON, because any body would deserialize into BaseResponse's defaults.
            Assert.True(document.RootElement.TryGetProperty("success", out _), $"Not a BaseResponse: {json}");

            var body = JsonSerializer.Deserialize<BaseResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal((int)HttpStatusCode.BadRequest, body.ResponseCode);
            return body;
        }
    }
}
