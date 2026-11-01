namespace XiangqiOnline.API.Infrastructure;

public static class EnvironmentSettingsValidator
{
    public static void Validate(string? connectionString, IReadOnlyCollection<string> allowedOrigins)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:XiangqiOnline must be configured.");
        if (allowedOrigins.Count == 0)
            throw new InvalidOperationException("Cors:AllowedOrigins must include at least one origin.");
        foreach (var origin in allowedOrigins)
            if (origin.Contains('*', StringComparison.Ordinal)
                || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")
                || uri.UserInfo.Length > 0
                || uri.AbsolutePath != "/"
                || uri.Query.Length > 0
                || uri.Fragment.Length > 0)
                throw new InvalidOperationException("Cors:AllowedOrigins must contain only explicit HTTP or HTTPS origins.");
    }
}
