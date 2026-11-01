using Xunit;

namespace XiangqiOnline.UnitTests;

public sealed class LayeringTests
{
    [Fact]
    public void SolutionIsTargetedAtNet8()
    {
        var root = FindRepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories);
        Assert.NotEmpty(projects);
        Assert.All(projects, project => Assert.Contains("net8.0", File.ReadAllText(project), StringComparison.Ordinal));
    }

    [Fact]
    public void DomainAndGameEngineDoNotDependOnPersistenceOrEntityFramework()
    {
        var root = FindRepositoryRoot();
        foreach (var project in new[] { "XiangqiOnline.Domain", "XiangqiOnline.GameEngine" })
        {
            var path = Path.Combine(root, "src", project, $"{project}.csproj");
            var content = File.ReadAllText(path);
            Assert.DoesNotContain("XiangqiOnline.Infrastructure", content, StringComparison.Ordinal);
            Assert.DoesNotContain("EntityFrameworkCore", content, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XiangqiOnline.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Solution root was not found.");
    }
}
