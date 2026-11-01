using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace XiangqiOnline.IntegrationTests;

[Collection("Sql health tests")]
public sealed class HealthEndpointTests
{
    [Fact]
    public async Task HealthReturnsOkWhenSqlServerMasterIsReachable()
    {
        var (status, body, hasRequestId) = await RequestWithConnection("Server=.;Database=XiangqiOnlineDB;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=3");
        Assert.Equal(System.Net.HttpStatusCode.OK, status);
        Assert.Contains("Healthy", body, StringComparison.Ordinal);
        Assert.True(hasRequestId);
    }

    [Fact]
    public async Task HealthReturnsUnavailableWhenSqlServerCannotBeReached()
    {
        var (status, body, _) = await RequestWithConnection("Server=127.0.0.1,1;Database=missing;Connect Timeout=1;ConnectRetryCount=0;Pooling=False;TrustServerCertificate=True");
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, status);
        Assert.Contains("Unhealthy", body, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", body, StringComparison.Ordinal);
    }

    private static async Task<(System.Net.HttpStatusCode Status, string Body, bool HasRequestId)> RequestWithConnection(string connectionString)
    {
        var previous = Environment.GetEnvironmentVariable("ConnectionStrings__XiangqiOnline");
        var previousIssuer = Environment.GetEnvironmentVariable("Jwt__Issuer");
        var previousAudience = Environment.GetEnvironmentVariable("Jwt__Audience");
        var previousKey = Environment.GetEnvironmentVariable("Jwt__SigningKeyBase64");
        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__XiangqiOnline", connectionString);
            Environment.SetEnvironmentVariable("Jwt__Issuer", "XiangqiOnline.Tests");
            Environment.SetEnvironmentVariable("Jwt__Audience", "XiangqiOnline.Tests.Web");
            Environment.SetEnvironmentVariable("Jwt__SigningKeyBase64", Convert.ToBase64String(Enumerable.Repeat((byte)0x42, 64).ToArray()));
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/api/v1/health");
            return (response.StatusCode, await response.Content.ReadAsStringAsync(), response.Headers.Contains("X-Request-Id"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__XiangqiOnline", previous);
            Environment.SetEnvironmentVariable("Jwt__Issuer", previousIssuer);
            Environment.SetEnvironmentVariable("Jwt__Audience", previousAudience);
            Environment.SetEnvironmentVariable("Jwt__SigningKeyBase64", previousKey);
        }
    }
}

[CollectionDefinition("Sql health tests", DisableParallelization = true)]
public sealed class SqlHealthTestCollection;
