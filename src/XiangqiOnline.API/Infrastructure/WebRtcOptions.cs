namespace XiangqiOnline.API.Infrastructure;

public sealed class WebRtcOptions
{
    public const string SectionName = "WebRtc";
    public List<IceServerOptions> IceServers { get; set; } = [new() { Urls = ["stun:stun.l.google.com:19302"] }];
}

public sealed class IceServerOptions
{
    public string[] Urls { get; set; } = [];
    public string? Username { get; set; }
    public string? Credential { get; set; }
}
