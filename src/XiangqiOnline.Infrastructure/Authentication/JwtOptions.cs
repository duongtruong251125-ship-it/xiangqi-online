using System.ComponentModel.DataAnnotations;

namespace XiangqiOnline.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    [Required, MinLength(1)] public string Issuer { get; init; } = string.Empty;
    [Required, MinLength(1)] public string Audience { get; init; } = string.Empty;
    [Required] public string SigningKeyBase64 { get; init; } = string.Empty;
    [Range(1, 60)] public int AccessTokenMinutes { get; init; } = 15;
}
