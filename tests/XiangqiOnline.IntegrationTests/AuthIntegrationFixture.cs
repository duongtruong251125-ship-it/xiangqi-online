using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XiangqiOnline.Infrastructure.Persistence;

namespace XiangqiOnline.IntegrationTests;

public class AuthIntegrationFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"XiangqiOnlineAuthTest_{Guid.NewGuid():N}";
    private readonly string _masterConnectionString;
    private readonly string _testKey = Convert.ToBase64String(Enumerable.Repeat((byte)0x42, 64).ToArray());
    private bool _databaseCreated;
    private readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.OrdinalIgnoreCase);

    public AuthIntegrationFixture()
    {
        var configured = Environment.GetEnvironmentVariable("XIANGQI_TEST_SQL_CONNECTION")
            ?? "Server=.;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";
        var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master" };
        _masterConnectionString = builder.ConnectionString;
        TestConnectionString = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = _databaseName }.ConnectionString;
    }

    public string TestConnectionString { get; }
    public string TestSigningKey => _testKey;

    public async Task InitializeAsync()
    {
        SetEnvironment("ConnectionStrings__XiangqiOnline", TestConnectionString);
        SetEnvironment("Jwt__Issuer", "XiangqiOnline.Tests");
        SetEnvironment("Jwt__Audience", "XiangqiOnline.Tests.Web");
        SetEnvironment("Jwt__SigningKeyBase64", _testKey);
        SetEnvironment("RateLimits__AuthLoginPerMinute", "100");
        SetEnvironment("RateLimits__AuthRefreshPerMinute", "100");
        SetEnvironment("RateLimits__RoomInvitationsPerMinute", "100");
        SetEnvironment("RateLimits__AuthRegisterPer10Minutes", RegistrationPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await using (var connection = new SqlConnection(_masterConnectionString))
        {
            await connection.OpenAsync();
            await using var create = connection.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await create.ExecuteNonQueryAsync();
            _databaseCreated = true;
        }

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        try
        {
            if (_databaseCreated)
            {
                await using var connection = new SqlConnection(_masterConnectionString);
                await connection.OpenAsync();
                await using var drop = connection.CreateCommand();
                drop.CommandText = $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
                await drop.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            foreach (var (key, value) in _previousEnvironment) Environment.SetEnvironmentVariable(key, value);
        }
    }

    private void SetEnvironment(string key, string value)
    {
        _previousEnvironment.TryAdd(key, Environment.GetEnvironmentVariable(key));
        Environment.SetEnvironmentVariable(key, value);
    }

    protected virtual int RegistrationPermitLimit => 100;
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AuthIntegrationCollection : ICollectionFixture<AuthIntegrationFixture>
{
    public const string Name = "Authentication SQL integration";
}

[CollectionDefinition(RateLimitCollection.Name, DisableParallelization = true)]
public sealed class RateLimitCollection : ICollectionFixture<RateLimitAuthFixture>
{
    public const string Name = "Authentication rate limit SQL integration";
}

public sealed class RateLimitAuthFixture : AuthIntegrationFixture
{
    protected override int RegistrationPermitLimit => 5;
}
