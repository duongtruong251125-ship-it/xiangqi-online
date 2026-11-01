using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using XiangqiOnline.API.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using XiangqiOnline.API.Infrastructure;
using XiangqiOnline.Application.Authentication;
using XiangqiOnline.Application.Games;
using XiangqiOnline.Application.Players;
using XiangqiOnline.Application.Rooms;
using XiangqiOnline.Application.Interactions;
using XiangqiOnline.Application.AiGames;
using XiangqiOnline.GameEngine;
using XiangqiOnline.Contracts.Authentication;
using XiangqiOnline.Infrastructure.Authentication;
using XiangqiOnline.Infrastructure.Games;
using XiangqiOnline.Infrastructure.Persistence;
using XiangqiOnline.Infrastructure.Players;
using XiangqiOnline.Infrastructure.Rooms;
using XiangqiOnline.Infrastructure.Interactions;
using XiangqiOnline.Infrastructure.AiGames;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddControllersWithViews().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
        ApiResponses.Failure("VALIDATION_ERROR", "Dữ liệu gửi lên chưa hợp lệ.", context.HttpContext));
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR(options => options.AddFilter<GameHubRateLimitFilter>());
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Xiangqi Online API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var connectionString = builder.Configuration.GetConnectionString("XiangqiOnline");
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
EnvironmentSettingsValidator.Validate(connectionString, allowedOrigins);
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));
builder.Services.AddHealthChecks().AddCheck<SqlServerHealthCheck>("sql-server", tags: ["ready"]);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<WebRtcOptions>(builder.Configuration.GetSection(WebRtcOptions.SectionName));
builder.Services.AddSingleton<GamePresence>();
builder.Services.AddSingleton<GameCallEndpoints>();
builder.Services.AddSingleton<GameHubRateLimitFilter>();
builder.Services.AddSingleton<IXiangqiRules, XiangqiRules>();
builder.Services.AddSingleton<AiTurnQueue>();
builder.Services.AddSingleton<IAiTurnQueue>(services => services.GetRequiredService<AiTurnQueue>());
builder.Services.AddSingleton<IAiSearchCoordinator, AiSearchCoordinator>();
builder.Services.AddScoped<IAiGameRepository, EfAiGameRepository>();
builder.Services.AddScoped<IAiGameService, AiGameService>();
builder.Services.AddScoped<IAiTurnProcessor, AiTurnProcessor>();
builder.Services.AddScoped<IAuthRepository, EfAuthRepository>();
builder.Services.AddScoped<IPlayerStatsRepository, EfPlayerStatsRepository>();
builder.Services.AddScoped<IPlayerStatsService, PlayerStatsService>();
builder.Services.AddScoped<IRoomRepository, EfRoomRepository>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IInvitationRepository, EfInvitationRepository>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<IChatRepository, EfChatRepository>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ICallSessionRepository, EfCallSessionRepository>();
builder.Services.AddScoped<ICallSignalingService, CallSignalingService>();
builder.Services.AddSingleton<IRoomEventNotifier, RoomEventNotifier>();
builder.Services.AddSingleton<IGameEventNotifier, GameEventNotifier>();
builder.Services.AddHostedService<GameTimeoutWorker>();
builder.Services.AddHostedService<CallTimeoutWorker>();
builder.Services.AddHostedService<AiTurnWorker>();
builder.Services.AddScoped<IGameplayRepository, EfGameplayRepository>();
builder.Services.AddScoped<IGameplayService, GameplayService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IPasswordHashing, IdentityPasswordHashing>();
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => IsValidJwtOptions(options), "Jwt issuer, audience, and a base64 signing key of at least 256 bits must be configured through User Secrets or environment variables.")
    .ValidateOnStart();
builder.Services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
{
    var jwt = jwtOptions.Value;
    var signingKey = Convert.FromBase64String(jwt.SigningKeyBase64);
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(signingKey),
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.Sub
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if ((context.Request.Path.StartsWithSegments("/hubs/rooms") || context.Request.Path.StartsWithSegments("/hubs/game"))
                && context.Request.Query.TryGetValue("access_token", out var token))
                context.Token = token;
            return Task.CompletedTask;
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiResponses.Failure(
                context.AuthenticateFailure is SecurityTokenExpiredException ? "AUTH_TOKEN_EXPIRED" : "AUTH_UNAUTHORIZED",
                context.AuthenticateFailure is SecurityTokenExpiredException ? "Access token đã hết hạn." : "Bạn cần đăng nhập để thực hiện thao tác này.",
                context.HttpContext));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiResponses.Failure("AUTH_UNAUTHORIZED", "Bạn không có quyền thực hiện thao tác này.", context.HttpContext));
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponses.Failure("AUTH_RATE_LIMITED", "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.", context.HttpContext), cancellationToken);
    };
    AddIpPolicy(options, "auth-login", builder.Configuration.GetValue("RateLimits:AuthLoginPerMinute", 5), TimeSpan.FromMinutes(1));
    AddIpPolicy(options, "auth-register", builder.Configuration.GetValue("RateLimits:AuthRegisterPer10Minutes", 5), TimeSpan.FromMinutes(10));
    AddIpPolicy(options, "auth-refresh", builder.Configuration.GetValue("RateLimits:AuthRefreshPerMinute", 10), TimeSpan.FromMinutes(1));
    AddIpPolicy(options, "room-invitation", builder.Configuration.GetValue("RateLimits:RoomInvitationsPerMinute", 6), TimeSpan.FromMinutes(1));
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("Frontend");
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<RoomHub>("/hubs/rooms");
app.MapHub<GameHub>("/hubs/game");
app.MapControllerRoute(name: "online-game", pattern: "games/{id:guid}", defaults: new { controller = "Game", action = "Index" });
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

static void AddIpPolicy(RateLimiterOptions options, string name, int permits, TimeSpan window) =>
    options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0, AutoReplenishment = true }));

static bool IsValidJwtOptions(JwtOptions options)
{
    if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience)) return false;
    try { return Convert.FromBase64String(options.SigningKeyBase64).Length >= 32; }
    catch (FormatException) { return false; }
}

public partial class Program;
