using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace StargateAPI.Tests
{
    // The browser lets the UI read the API's responses only if the API names the UI's origin. Most tests run the
    // API in Development, so they check the real appsettings.Development.json. Each test uses its own names.
    public class CorsTests : IClassFixture<StargateApiFactory>
    {
        private const string UiOrigin = "http://localhost:4200";

        private readonly HttpClient _client;
        private readonly HttpClient _developmentClient;

        public CorsTests(StargateApiFactory factory)
        {
            _client = factory.CreateClient();
            _developmentClient = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development")).CreateClient();
        }

        [Theory]
        [InlineData("POST", "/Person")]
        [InlineData("PUT", "/Person/Anyone")]
        public async Task Preflight_UiOrigin_IsAllowed(string method, string path)
        {
            var response = await _developmentClient.SendAsync(Preflight(UiOrigin, method, path));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(UiOrigin, AllowedOrigin(response));
            Assert.Contains(method, HeaderValue(response, "Access-Control-Allow-Methods"));
            Assert.Contains("content-type", HeaderValue(response, "Access-Control-Allow-Headers"), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Preflight_OtherOrigin_IsNotAllowed()
        {
            var response = await _developmentClient.SendAsync(Preflight("http://evil.example", "POST", "/Person"));

            Assert.Null(AllowedOrigin(response));
        }

        [Fact]
        public async Task Preflight_MethodTheApiDoesNotUse_IsNotAllowed()
        {
            var response = await _developmentClient.SendAsync(Preflight(UiOrigin, "DELETE", "/Person/Anyone"));

            Assert.DoesNotContain("DELETE", HeaderValue(response, "Access-Control-Allow-Methods"));
        }

        [Fact]
        public async Task Get_UiOrigin_ResponseNamesOrigin()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/Person");
            request.Headers.Add("Origin", UiOrigin);

            var response = await _developmentClient.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(UiOrigin, AllowedOrigin(response));
        }

        [Fact]
        public async Task ErrorResponse_UiOrigin_ResponseNamesOrigin()
        {
            // Rule violations are written by the exception handler. Without the header here, the UI couldn't read
            // the message and the browser would report a CORS failure instead.
            (await _developmentClient.PostAsJsonAsync("/Person", "Cors Duplicate Person")).EnsureSuccessStatusCode();
            var request = new HttpRequestMessage(HttpMethod.Post, "/Person") { Content = JsonContent.Create("Cors Duplicate Person") };
            request.Headers.Add("Origin", UiOrigin);

            var response = await _developmentClient.SendAsync(request);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(UiOrigin, AllowedOrigin(response));
        }

        [Fact]
        public async Task Preflight_BaseConfiguration_AllowsNoOrigin()
        {
            // Outside Development, a deployment has to name its UI's origin on purpose.
            var response = await _client.SendAsync(Preflight(UiOrigin, "POST", "/Person"));

            Assert.Null(AllowedOrigin(response));
        }

        private static HttpRequestMessage Preflight(string origin, string method, string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, path);
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", method);
            request.Headers.Add("Access-Control-Request-Headers", "content-type");
            return request;
        }

        private static string? AllowedOrigin(HttpResponseMessage response)
        {
            return response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? string.Join(",", values) : null;
        }

        private static string HeaderValue(HttpResponseMessage response, string name)
        {
            return response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : string.Empty;
        }
    }
}
