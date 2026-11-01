using XiangqiOnline.API.Infrastructure;

namespace XiangqiOnline.IntegrationTests;

public sealed class EnvironmentSettingsValidatorTests
{
    [Fact]
    public void MissingConnectionStringIsRejected() =>
        Assert.Throws<InvalidOperationException>(() => EnvironmentSettingsValidator.Validate(null, ["https://localhost:5173"]));

    [Fact]
    public void EmptyCorsAllowlistIsRejected() =>
        Assert.Throws<InvalidOperationException>(() => EnvironmentSettingsValidator.Validate("Server=local", []));

    [Fact]
    public void WildcardCorsOriginIsRejected() =>
        Assert.Throws<InvalidOperationException>(() => EnvironmentSettingsValidator.Validate("Server=local", ["*"]));

    [Fact]
    public void WildcardSubdomainCorsOriginIsRejected() =>
        Assert.Throws<InvalidOperationException>(() => EnvironmentSettingsValidator.Validate("Server=local", ["https://*.example.com"]));

    [Fact]
    public void CorsOriginWithPathIsRejected() =>
        Assert.Throws<InvalidOperationException>(() => EnvironmentSettingsValidator.Validate("Server=local", ["https://example.com/api"]));
}
