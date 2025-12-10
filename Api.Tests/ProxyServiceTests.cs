using Xunit;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace Api.Tests
{
    // Reduced Unit Tests
    public class ProxyServiceTests
    {
        [Theory]
        [InlineData("http://example.com:8080", true)]
        [InlineData("not-a-valid-url", false)]
        public void ValidateProxyUrl_VariousInputs_ReturnsExpected(string url, bool expected)
        {
            // Act
            var result = Uri.TryCreate(url, UriKind.Absolute, out _);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("192.168.1.1", 8080, true)]
        [InlineData("256.256.256.256", 70000, false)]
        public void ValidateIpAndPort_VariousInputs_ReturnsExpected(string ip, int port, bool expected)
        {
            // Act
            bool ipValid = System.Net.IPAddress.TryParse(ip, out _);
            bool portValid = port >= 1 && port <= 65535;
            bool result = ipValid && portValid;

            // Assert
            Assert.Equal(expected, result);
        }
    }

    // Reduced Integration Tests
    public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public ApiIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task HealthEndpoint_ReturnsOkAndHealthy()
        {
            // Act
            var response = await _client.GetAsync("/health");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Healthy", content, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ApiEndpoint_InvalidRoute_ReturnsNotFound()
        {
            // Act
            var response = await _client.GetAsync("/api/nonexistent");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
