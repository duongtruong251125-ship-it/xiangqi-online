namespace XiangqiOnline.IntegrationTests;

[Collection(AuthIntegrationCollection.Name)]
public sealed class MvcRouteTests(AuthIntegrationFixture fixture)
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Profile/Index")]
    [InlineData("/Lobby/Index")]
    [InlineData("/Room/Index/demo")]
    [InlineData("/Game/Index/demo")]
    [InlineData("/AiGame/Index")]
    [InlineData("/AiGame/Play/00000000-0000-0000-0000-000000000001")]
    [InlineData("/games/00000000-0000-0000-0000-000000000001")]
    public async Task RazorPagesRenderAtExpectedRoutes(string path)
    {
        using var client = fixture.CreateClient();
        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"app\"", html, StringComparison.Ordinal);
        Assert.Contains("vue.global.prod.js", html, StringComparison.Ordinal);
        Assert.Contains("/js/app.js", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaticVueAssetsAreServedWithoutCapturingApiRoutes()
    {
        using var client = fixture.CreateClient();
        using var vue = await client.GetAsync("/lib/vue/vue.global.prod.js");
        using var app = await client.GetAsync("/js/app.js");
        using var aiCss = await client.GetAsync("/css/phase7.css");
        using var health = await client.GetAsync("/api/v1/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, vue.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, app.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, aiCss.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, health.StatusCode);
    }
}
