using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using XiangqiOnline.Infrastructure.Persistence;

namespace XiangqiOnline.IntegrationTests;

[Collection(AuthIntegrationCollection.Name)]
public sealed class RoomEndpointTests(AuthIntegrationFixture fixture)
{
    [Fact]
    public async Task GameChatPersistsBroadcastsDeduplicatesAndPagesForParticipants()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var redToken = await RegisterAndLogin(client);
        var blackToken = await RegisterAndLogin(client);
        var outsiderToken = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { isPrivate = false }, redToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var room = createJson.RootElement.GetProperty("data");
        var roomId = room.GetProperty("id").GetGuid();
        using var join = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code = room.GetProperty("code").GetString() }, blackToken);
        using var readyRed = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 1 }, redToken);
        using var readyBlack = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 2 }, blackToken);
        using var start = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/start", new { expectedVersion = 3 }, redToken);
        using var startJson = await JsonDocument.ParseAsync(await start.Content.ReadAsStreamAsync());
        var gameId = startJson.RootElement.GetProperty("data").GetProperty("gameId").GetGuid();

        await using var red = BuildGameConnection(redToken);
        await using var black = BuildGameConnection(blackToken);
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        black.On<JsonElement>("ChatMessageReceived", envelope => received.TrySetResult(envelope.GetProperty("message")));
        await red.StartAsync(); await black.StartAsync();
        await red.InvokeAsync("JoinGame", gameId); await black.InvokeAsync("JoinGame", gameId);
        await red.InvokeAsync("JoinInteraction", gameId); await black.InvokeAsync("JoinInteraction", gameId);
        var clientMessageId = Guid.NewGuid();
        var request = new { gameId, clientMessageId, content = "  Chào bạn  " };
        var sent = await red.InvokeAsync<JsonElement>("SendChatMessage", request);
        Assert.True(sent.GetProperty("accepted").GetBoolean());
        var broadcast = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("Chào bạn", broadcast.GetProperty("content").GetString());
        var retry = await red.InvokeAsync<JsonElement>("SendChatMessage", request);
        Assert.True(retry.GetProperty("duplicate").GetBoolean());

        using var page = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}/messages?limit=10", null, redToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var pageJson = await JsonDocument.ParseAsync(await page.Content.ReadAsStreamAsync());
        Assert.Single(pageJson.RootElement.GetProperty("data").GetProperty("items").EnumerateArray());
        using var forbidden = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}/messages", null, outsiderToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task WebRtcSignalingIsBoundToSelectedGameConnectionsAndCallState()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var redToken = await RegisterAndLogin(client);
        var blackToken = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { isPrivate = false }, redToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var room = createJson.RootElement.GetProperty("data");
        var roomId = room.GetProperty("id").GetGuid();
        using var join = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code = room.GetProperty("code").GetString() }, blackToken);
        using var readyRed = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 1 }, redToken);
        using var readyBlack = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 2 }, blackToken);
        using var start = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/start", new { expectedVersion = 3 }, redToken);
        using var startJson = await JsonDocument.ParseAsync(await start.Content.ReadAsStreamAsync());
        var gameId = startJson.RootElement.GetProperty("data").GetProperty("gameId").GetGuid();
        await using var red = BuildGameConnection(redToken);
        await using var black = BuildGameConnection(blackToken);
        await using var redSecondTab = BuildGameConnection(redToken);
        var incoming = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedSignal = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        black.On<JsonElement>("IncomingCall", evt => incoming.TrySetResult(evt));
        red.On<JsonElement>("CallStateChanged", evt => { if (evt.GetProperty("call").GetProperty("status").GetString() == "Accepted") accepted.TrySetResult(evt); });
        black.On<JsonElement>("CallSignalReceived", evt => receivedSignal.TrySetResult(evt));
        await red.StartAsync(); await black.StartAsync();
        await red.InvokeAsync("JoinGame", gameId); await black.InvokeAsync("JoinGame", gameId);
        await red.InvokeAsync("JoinInteraction", gameId); await black.InvokeAsync("JoinInteraction", gameId);
        var startCall = await red.InvokeAsync<JsonElement>("StartCall", gameId);
        Assert.True(startCall.GetProperty("accepted").GetBoolean());
        var invitation = await incoming.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var callId = invitation.GetProperty("call").GetProperty("id").GetGuid();
        var accept = await black.InvokeAsync<JsonElement>("AcceptCall", callId);
        Assert.True(accept.GetProperty("accepted").GetBoolean());
        await accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await redSecondTab.StartAsync();
        await redSecondTab.InvokeAsync("JoinInteraction", gameId);
        var wrongEndpoint = await redSecondTab.InvokeAsync<JsonElement>("SendCallSignal", new { callId, gameId, payload = "{\"type\":\"offer\",\"sdp\":\"other-tab\"}" }, "Offer");
        Assert.False(wrongEndpoint.GetProperty("accepted").GetBoolean());
        var offer = await red.InvokeAsync<JsonElement>("SendCallSignal", new { callId, gameId, payload = "{\"type\":\"offer\",\"sdp\":\"test-sdp\"}" }, "Offer");
        Assert.True(offer.GetProperty("accepted").GetBoolean());
        var forwarded = await receivedSignal.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(callId, forwarded.GetProperty("callId").GetGuid());
        Assert.Equal("Offer", forwarded.GetProperty("kind").GetString());
        var end = await red.InvokeAsync<JsonElement>("EndCall", callId);
        Assert.True(end.GetProperty("accepted").GetBoolean());
    }

    [Fact]
    public async Task SignalRGameHubBroadcastsCommittedMovesBetweenPlayers()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var redToken = await RegisterAndLogin(client);
        var blackToken = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { isPrivate = false }, redToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var room = createJson.RootElement.GetProperty("data");
        var roomId = room.GetProperty("id").GetGuid();
        var code = room.GetProperty("code").GetString();
        using var join = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code }, blackToken);
        using var readyRed = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 1 }, redToken);
        using var readyBlack = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 2 }, blackToken);
        using var start = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/start", new { expectedVersion = 3 }, redToken);
        using var startJson = await JsonDocument.ParseAsync(await start.Content.ReadAsStreamAsync());
        var gameId = startJson.RootElement.GetProperty("data").GetProperty("gameId").GetGuid();
        var receivedMove = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedState = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var redReceivedMove = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedFinished = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var red = BuildGameConnection(redToken);
        await using var black = BuildGameConnection(blackToken);
        red.On<JsonElement>("MoveApplied", payload => redReceivedMove.TrySetResult(payload));
        black.On<JsonElement>("MoveApplied", payload => receivedMove.TrySetResult(payload));
        black.On<JsonElement>("GameStateSynced", payload => receivedState.TrySetResult(payload));
        black.On<JsonElement>("GameFinished", payload => receivedFinished.TrySetResult(payload));
        await red.StartAsync();
        await black.StartAsync();
        await red.InvokeAsync("JoinGame", gameId);
        await black.InvokeAsync("JoinGame", gameId);
        var initialState = await receivedState.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, initialState.GetProperty("version").GetInt64());
        Assert.InRange(initialState.GetProperty("clock").GetProperty("redRemainingMs").GetInt64(), 1, 600_000);
        Assert.Equal(600_000, initialState.GetProperty("clock").GetProperty("blackRemainingMs").GetInt64());

        var result = await red.InvokeAsync<JsonElement>("SubmitMove", new {
            gameId, clientMoveId = Guid.NewGuid(), fromSquare = "b9", toSquare = "c7", expectedGameVersion = 0L
        });
        Assert.True(result.GetProperty("accepted").GetBoolean());
        var moveEvent = await receivedMove.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(gameId, moveEvent.GetProperty("gameId").GetGuid());
        Assert.Equal(1, moveEvent.GetProperty("version").GetInt64());
        Assert.Equal("c7", moveEvent.GetProperty("toSquare").GetString());
        Assert.InRange(moveEvent.GetProperty("clock").GetProperty("redRemainingMs").GetInt64(), 1, 600_000);

        await black.StopAsync();
        receivedState = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await black.StartAsync();
        await black.InvokeAsync("JoinGame", gameId);
        var restoredState = await receivedState.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, restoredState.GetProperty("version").GetInt64());
        Assert.Equal(moveEvent.GetProperty("fenAfter").GetString(), restoredState.GetProperty("fen").GetString());

        redReceivedMove = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResult = await black.InvokeAsync<JsonElement>("SubmitMove", new {
            gameId, clientMoveId = Guid.NewGuid(), fromSquare = "b0", toSquare = "c2", expectedGameVersion = 1L
        });
        Assert.True(secondResult.GetProperty("accepted").GetBoolean());
        var secondMoveEvent = await redReceivedMove.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(2, secondMoveEvent.GetProperty("version").GetInt64());
        using var redSnapshot = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}/snapshot", null, redToken);
        using var blackSnapshot = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}/snapshot", null, blackToken);
        using var redSnapshotJson = await JsonDocument.ParseAsync(await redSnapshot.Content.ReadAsStreamAsync());
        using var blackSnapshotJson = await JsonDocument.ParseAsync(await blackSnapshot.Content.ReadAsStreamAsync());
        Assert.Equal(redSnapshotJson.RootElement.GetProperty("data").GetProperty("game").GetProperty("version").GetInt64(),
            blackSnapshotJson.RootElement.GetProperty("data").GetProperty("game").GetProperty("version").GetInt64());
        Assert.Equal(redSnapshotJson.RootElement.GetProperty("data").GetProperty("game").GetProperty("currentFen").GetString(),
            blackSnapshotJson.RootElement.GetProperty("data").GetProperty("game").GetProperty("currentFen").GetString());
        await red.InvokeAsync("ResignGame", gameId);
        var finished = await receivedFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("Resignation", finished.GetProperty("resultReason").GetString());
        Assert.Equal(3, finished.GetProperty("finalVersion").GetInt64());
    }

    [Fact]
    public async Task ExpiredOnlineClockFinishesTheGameAndRejectsTheMove()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var redToken = await RegisterAndLogin(client);
        var blackToken = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { isPrivate = false }, redToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var room = createJson.RootElement.GetProperty("data");
        var roomId = room.GetProperty("id").GetGuid();
        var code = room.GetProperty("code").GetString();
        using var join = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code }, blackToken);
        using var readyRed = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 1 }, redToken);
        using var readyBlack = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 2 }, blackToken);
        using var start = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/start", new { expectedVersion = 3 }, redToken);
        using var startJson = await JsonDocument.ParseAsync(await start.Content.ReadAsStreamAsync());
        var gameId = startJson.RootElement.GetProperty("data").GetProperty("gameId").GetGuid();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var game = await db.Games.SingleAsync(item => item.Id == gameId);
            game.RedRemainingMs = 1;
            game.TurnStartedAtUtc = DateTime.UtcNow.AddSeconds(-2);
            await db.SaveChangesAsync();
        }

        using var move = await Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/moves", new {
            fromFile = 1, fromRank = 9, toFile = 2, toRank = 7, expectedVersion = 0, clientMoveId = Guid.NewGuid()
        }, redToken);
        Assert.Equal(HttpStatusCode.Conflict, move.StatusCode);
        using var status = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}/status", null, redToken);
        using var statusJson = await JsonDocument.ParseAsync(await status.Content.ReadAsStreamAsync());
        var gameStatus = statusJson.RootElement.GetProperty("data");
        Assert.Equal("Finished", gameStatus.GetProperty("status").GetString());
        Assert.Equal("Timeout", gameStatus.GetProperty("resultReason").GetString());
        await using var verifyScope = fixture.Services.CreateAsyncScope();
        var verifiedDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedGame = await verifiedDb.Games.SingleAsync(item => item.Id == gameId);
        Assert.Equal(persistedGame.BlackPlayerId, persistedGame.WinnerId);
        Assert.Empty(await verifiedDb.GameMoves.Where(item => item.GameId == gameId).ToListAsync());
    }

    [Fact]
    public async Task ConcurrentOnlineMovesWithTheSameVersionOnlyCommitOneMove()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var redToken = await RegisterAndLogin(client);
        var blackToken = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { isPrivate = false }, redToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var room = createJson.RootElement.GetProperty("data");
        var roomId = room.GetProperty("id").GetGuid();
        var code = room.GetProperty("code").GetString();
        using var join = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code }, blackToken);
        using var readyRed = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 1 }, redToken);
        using var readyBlack = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 2 }, blackToken);
        using var start = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/start", new { expectedVersion = 3 }, redToken);
        using var startJson = await JsonDocument.ParseAsync(await start.Content.ReadAsStreamAsync());
        var gameId = startJson.RootElement.GetProperty("data").GetProperty("gameId").GetGuid();

        var horseMove = Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/moves", new {
            fromFile = 1, fromRank = 9, toFile = 2, toRank = 7, expectedVersion = 0, clientMoveId = Guid.NewGuid()
        }, redToken);
        var pawnMove = Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/moves", new {
            fromFile = 0, fromRank = 6, toFile = 0, toRank = 5, expectedVersion = 0, clientMoveId = Guid.NewGuid()
        }, redToken);
        using var first = await horseMove;
        using var second = await pawnMove;
        var statuses = new[] { first.StatusCode, second.StatusCode };
        Assert.Single(statuses.Where(status => status == HttpStatusCode.OK));
        Assert.Single(statuses.Where(status => status == HttpStatusCode.Conflict));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Games.Where(game => game.Id == gameId).Select(game => game.Version).SingleAsync());
        Assert.Equal(1, await db.GameMoves.CountAsync(move => move.GameId == gameId));
    }

    [Fact]
    public async Task CreateJoinAndCapacityAreEnforcedAndOnlyMembersCanReadRoom()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var ownerToken = await RegisterAndLogin(client);
        var guestToken = await RegisterAndLogin(client);
        var outsiderToken = await RegisterAndLogin(client);

        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { isPrivate = false }, ownerToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createdJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var room = createdJson.RootElement.GetProperty("data");
        var roomId = room.GetProperty("id").GetGuid();
        var code = room.GetProperty("code").GetString()!;
        Assert.Equal(6, code.Length);
        Assert.Equal(1, room.GetProperty("members").GetArrayLength());

        using var join = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code = code.ToLowerInvariant() }, guestToken);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        using var joinedJson = await JsonDocument.ParseAsync(await join.Content.ReadAsStreamAsync());
        var joined = joinedJson.RootElement.GetProperty("data");
        Assert.Equal(roomId, joined.GetProperty("id").GetGuid());
        Assert.Equal(2, joined.GetProperty("members").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, joined.GetProperty("gameId").ValueKind);
        Assert.Equal("Black", joined.GetProperty("members")[1].GetProperty("side").GetString());
        using var ownerReady = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 1 }, ownerToken);
        Assert.Equal(HttpStatusCode.OK, ownerReady.StatusCode);
        using var guestReady = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 2 }, guestToken);
        Assert.Equal(HttpStatusCode.OK, guestReady.StatusCode);
        using var start = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/start", new { expectedVersion = 3 }, ownerToken);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var startJson = await JsonDocument.ParseAsync(await start.Content.ReadAsStreamAsync());
        var gameId = startJson.RootElement.GetProperty("data").GetProperty("gameId").GetGuid();
        Assert.NotEqual(Guid.Empty, gameId);

        using var legalMoves = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}/legal-moves?fromFile=1&fromRank=9", null, ownerToken);
        Assert.Equal(HttpStatusCode.OK, legalMoves.StatusCode);
        using var legalJson = await JsonDocument.ParseAsync(await legalMoves.Content.ReadAsStreamAsync());
        Assert.Contains(legalJson.RootElement.GetProperty("data").EnumerateArray(), move => move.GetProperty("file").GetInt32() == 2 && move.GetProperty("rank").GetInt32() == 7);

        using var invalidMove = await Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/moves", new { fromFile = 1, fromRank = 9, toFile = 1, toRank = 8, expectedVersion = 0, clientMoveId = Guid.NewGuid() }, ownerToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidMove.StatusCode);
        var firstClientMoveId = Guid.NewGuid();
        var firstMoveRequest = new { fromFile = 1, fromRank = 9, toFile = 2, toRank = 7, expectedVersion = 0, clientMoveId = firstClientMoveId };
        using var move = await Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/moves", firstMoveRequest, ownerToken);
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        using var moveJson = await JsonDocument.ParseAsync(await move.Content.ReadAsStreamAsync());
        Assert.Equal(1, moveJson.RootElement.GetProperty("data").GetProperty("version").GetInt64());
        Assert.Equal(1, moveJson.RootElement.GetProperty("data").GetProperty("ply").GetInt32());
        using var retryMove = await Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/moves", firstMoveRequest, ownerToken);
        Assert.Equal(HttpStatusCode.OK, retryMove.StatusCode);
        using var retryJson = await JsonDocument.ParseAsync(await retryMove.Content.ReadAsStreamAsync());
        Assert.Equal(1, retryJson.RootElement.GetProperty("data").GetProperty("version").GetInt64());
        using var reusedMoveId = await Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/moves", new {
            fromFile = 0, fromRank = 6, toFile = 0, toRank = 5, expectedVersion = 0, clientMoveId = firstClientMoveId
        }, ownerToken);
        Assert.Equal(HttpStatusCode.Conflict, reusedMoveId.StatusCode);

        using var wrongTurn = await Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/moves", new { fromFile = 1, fromRank = 0, toFile = 2, toRank = 2, expectedVersion = 1, clientMoveId = Guid.NewGuid() }, ownerToken);
        Assert.Equal(HttpStatusCode.Conflict, wrongTurn.StatusCode);
        using var staleMove = await Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/moves", new { fromFile = 1, fromRank = 9, toFile = 2, toRank = 7, expectedVersion = 0, clientMoveId = Guid.NewGuid() }, guestToken);
        Assert.Equal(HttpStatusCode.Conflict, staleMove.StatusCode);
        using var blackMove = await Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/moves", new { fromFile = 1, fromRank = 0, toFile = 2, toRank = 2, expectedVersion = 1, clientMoveId = Guid.NewGuid() }, guestToken);
        Assert.Equal(HttpStatusCode.OK, blackMove.StatusCode);

        using var full = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code }, outsiderToken);
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        using var fullJson = await JsonDocument.ParseAsync(await full.Content.ReadAsStreamAsync());
        Assert.Equal("ROOM_ALREADY_STARTED", fullJson.RootElement.GetProperty("error").GetProperty("code").GetString());

        using var forbiddenRead = await Send(client, HttpMethod.Get, $"/api/v1/rooms/{roomId}", null, outsiderToken);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenRead.StatusCode);
        using var forbiddenGameRead = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}", null, outsiderToken);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenGameRead.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedRoom = await db.Rooms.SingleAsync(item => item.Id == roomId);
        Assert.Equal(XiangqiOnline.Domain.Enums.RoomStatus.InGame, storedRoom.Status);
        Assert.Equal(1, await db.Games.CountAsync(game => game.RoomId == roomId));
        Assert.Equal(2, await db.GameMoves.CountAsync(gameMove => gameMove.GameId == gameId));
    }

    [Fact]
    public async Task InvalidRoomCodeReturnsNotFound()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await RegisterAndLogin(client);
        using var response = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code = "bad!" }, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LobbySearchHidesPrivateRoomsAndJoiningByIdRequiresPublicVisibility()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var ownerToken = await RegisterAndLogin(client);
        var guestToken = await RegisterAndLogin(client);
        using var publicCreate = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { name = "Public morning", isPrivate = false }, ownerToken);
        using var privateCreate = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { name = "Hidden room", isPrivate = true }, ownerToken);
        Assert.Equal(HttpStatusCode.Created, publicCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, privateCreate.StatusCode);
        using var publicJson = await JsonDocument.ParseAsync(await publicCreate.Content.ReadAsStreamAsync());
        using var privateJson = await JsonDocument.ParseAsync(await privateCreate.Content.ReadAsStreamAsync());
        var publicRoom = publicJson.RootElement.GetProperty("data");
        var privateRoom = privateJson.RootElement.GetProperty("data");

        using var listing = await Send(client, HttpMethod.Get, "/api/v1/rooms?search=morning&status=Waiting&page=1", null, guestToken);
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        using var listingJson = await JsonDocument.ParseAsync(await listing.Content.ReadAsStreamAsync());
        var items = listingJson.RootElement.GetProperty("data").GetProperty("items");
        Assert.Single(items.EnumerateArray());
        Assert.Equal(publicRoom.GetProperty("id").GetGuid(), items[0].GetProperty("id").GetGuid());

        using var privateById = await Send(client, HttpMethod.Post, "/api/v1/rooms/join-public", new { roomId = privateRoom.GetProperty("id").GetGuid() }, guestToken);
        Assert.Equal(HttpStatusCode.NotFound, privateById.StatusCode);
        using var privateByCode = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code = privateRoom.GetProperty("code").GetString() }, guestToken);
        Assert.Equal(HttpStatusCode.OK, privateByCode.StatusCode);
    }

    [Fact]
    public async Task InvitationIsPersistedAcceptedAndDeclinedByRecipient()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var ownerToken = await RegisterAndLogin(client);
        var inviteeToken = await RegisterAndLogin(client);
        var declineToken = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { name = "Invite table", isPrivate = true }, ownerToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var roomId = createJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var inviteeName = createJson.RootElement.GetProperty("data").GetProperty("members")[0].GetProperty("username").GetString();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        inviteeName = await UsernameFromToken(client, inviteeToken);

        using var sent = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/invitations", new { username = inviteeName }, ownerToken);
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        using var sentJson = await JsonDocument.ParseAsync(await sent.Content.ReadAsStreamAsync());
        var invitationId = sentJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        using var duplicate = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/invitations", new { username = inviteeName }, ownerToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var inbox = await Send(client, HttpMethod.Get, "/api/v1/invitations", null, inviteeToken);
        using var inboxJson = await JsonDocument.ParseAsync(await inbox.Content.ReadAsStreamAsync());
        Assert.Single(inboxJson.RootElement.GetProperty("data").EnumerateArray());
        using var unauthorizedAccept = await Send(client, HttpMethod.Post, $"/api/v1/invitations/{invitationId}/accept", new { }, declineToken);
        Assert.Equal(HttpStatusCode.NotFound, unauthorizedAccept.StatusCode);
        using var accepted = await Send(client, HttpMethod.Post, $"/api/v1/invitations/{invitationId}/accept", new { }, inviteeToken);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var acceptedJson = await JsonDocument.ParseAsync(await accepted.Content.ReadAsStreamAsync());
        Assert.Equal("Accepted", acceptedJson.RootElement.GetProperty("data").GetProperty("status").GetString());

        using var secondRoom = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { name = "Decline table", isPrivate = false }, ownerToken);
        using var secondRoomJson = await JsonDocument.ParseAsync(await secondRoom.Content.ReadAsStreamAsync());
        var secondRoomId = secondRoomJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var declineName = await UsernameFromToken(client, declineToken);
        using var sentForDecline = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{secondRoomId}/invitations", new { username = declineName }, ownerToken);
        using var declineInviteJson = await JsonDocument.ParseAsync(await sentForDecline.Content.ReadAsStreamAsync());
        var declineId = declineInviteJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        using var declined = await Send(client, HttpMethod.Post, $"/api/v1/invitations/{declineId}/decline", new { }, declineToken);
        Assert.Equal(HttpStatusCode.OK, declined.StatusCode);
        using var declinedJson = await JsonDocument.ParseAsync(await declined.Content.ReadAsStreamAsync());
        Assert.Equal("Declined", declinedJson.RootElement.GetProperty("data").GetProperty("status").GetString());

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.Invitations.CountAsync(invitation => invitation.RoomId == roomId || invitation.RoomId == secondRoomId));
        Assert.Equal(2, await db.RoomMembers.CountAsync(member => member.RoomId == roomId && member.LeftAt == null));
    }

    [Fact]
    public async Task FullRoomDoesNotAcceptInvitationAndExpiryIsReported()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var ownerToken = await RegisterAndLogin(client);
        var guestToken = await RegisterAndLogin(client);
        var inviteeToken = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { name = "Capacity table", isPrivate = false }, ownerToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var room = createJson.RootElement.GetProperty("data");
        var roomId = room.GetProperty("id").GetGuid();
        var code = room.GetProperty("code").GetString();
        var inviteeName = await UsernameFromToken(client, inviteeToken);
        using var sent = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/invitations", new { username = inviteeName }, ownerToken);
        using var sentJson = await JsonDocument.ParseAsync(await sent.Content.ReadAsStreamAsync());
        var invitationId = sentJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        using var join = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code }, guestToken);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        using var fullAccept = await Send(client, HttpMethod.Post, $"/api/v1/invitations/{invitationId}/accept", new { }, inviteeToken);
        Assert.Equal(HttpStatusCode.Conflict, fullAccept.StatusCode);
        using var fullJson = await JsonDocument.ParseAsync(await fullAccept.Content.ReadAsStreamAsync());
        Assert.Equal("ROOM_FULL", fullJson.RootElement.GetProperty("error").GetProperty("code").GetString());

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invitation = await db.Invitations.SingleAsync(item => item.Id == invitationId);
            Assert.Equal(XiangqiOnline.Domain.Enums.InvitationStatus.Pending, invitation.Status);
            invitation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        using var expiredAccept = await Send(client, HttpMethod.Post, $"/api/v1/invitations/{invitationId}/accept", new { }, inviteeToken);
        Assert.Equal(HttpStatusCode.Conflict, expiredAccept.StatusCode);
        using var expiredJson = await JsonDocument.ParseAsync(await expiredAccept.Content.ReadAsStreamAsync());
        Assert.Equal("INVITATION_EXPIRED", expiredJson.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task LeavingWaitingRoomTransfersOwnershipAndConcurrentJoinProtectsLastSlot()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var ownerToken = await RegisterAndLogin(client);
        var firstGuestToken = await RegisterAndLogin(client);
        var secondGuestToken = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { name = "Transfer table", isPrivate = false }, ownerToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var room = createJson.RootElement.GetProperty("data");
        var roomId = room.GetProperty("id").GetGuid();
        var code = room.GetProperty("code").GetString();
        var joinA = Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code }, firstGuestToken);
        var joinB = Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code }, secondGuestToken);
        using var responseA = await joinA;
        using var responseB = await joinB;
        Assert.Single(new[] { responseA.StatusCode, responseB.StatusCode }.Where(status => status == HttpStatusCode.OK));
        Assert.Single(new[] { responseA.StatusCode, responseB.StatusCode }.Where(status => status == HttpStatusCode.Conflict));

        var winningToken = responseA.StatusCode == HttpStatusCode.OK ? firstGuestToken : secondGuestToken;
        using var leave = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/leave", new { }, ownerToken);
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        using var state = await Send(client, HttpMethod.Get, $"/api/v1/rooms/{roomId}", null, winningToken);
        using var stateJson = await JsonDocument.ParseAsync(await state.Content.ReadAsStreamAsync());
        var newOwner = stateJson.RootElement.GetProperty("data").GetProperty("ownerId").GetGuid();
        Assert.Equal(newOwner, stateJson.RootElement.GetProperty("data").GetProperty("members")[0].GetProperty("userId").GetGuid());
        Assert.Equal("Owner", stateJson.RootElement.GetProperty("data").GetProperty("members")[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task ConcurrentStartRequestsCreateOnlyOneGame()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var ownerToken = await RegisterAndLogin(client);
        var guestToken = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { name = "Start table", isPrivate = false }, ownerToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var room = createJson.RootElement.GetProperty("data");
        var roomId = room.GetProperty("id").GetGuid();
        var code = room.GetProperty("code").GetString();
        using var join = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code }, guestToken);
        using var readyOwner = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 1 }, ownerToken);
        using var readyGuest = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 2 }, guestToken);
        var first = Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/start", new { expectedVersion = 3 }, ownerToken);
        var second = Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/start", new { expectedVersion = 3 }, ownerToken);
        using var firstResponse = await first;
        using var secondResponse = await second;
        Assert.Single(new[] { firstResponse.StatusCode, secondResponse.StatusCode }.Where(status => status == HttpStatusCode.OK));
        Assert.Single(new[] { firstResponse.StatusCode, secondResponse.StatusCode }.Where(status => status == HttpStatusCode.Conflict));
        using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Games.CountAsync(game => game.RoomId == roomId));
    }

    [Fact]
    public async Task RoomRequiresDistinctSidesReadyPlayersAndHostForStart()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var ownerToken = await RegisterAndLogin(client);
        var guestToken = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { name = "Rules table", isPrivate = false }, ownerToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var room = createJson.RootElement.GetProperty("data");
        var roomId = room.GetProperty("id").GetGuid();
        var code = room.GetProperty("code").GetString();
        using var join = await Send(client, HttpMethod.Post, "/api/v1/rooms/join", new { code }, guestToken);

        using var wrongSide = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/side", new { side = "Red", expectedVersion = 1 }, guestToken);
        Assert.Equal(HttpStatusCode.Conflict, wrongSide.StatusCode);
        using var notReady = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/start", new { expectedVersion = 1 }, ownerToken);
        Assert.Equal(HttpStatusCode.Conflict, notReady.StatusCode);
        using var notReadyJson = await JsonDocument.ParseAsync(await notReady.Content.ReadAsStreamAsync());
        Assert.Equal("ROOM_NOT_READY", notReadyJson.RootElement.GetProperty("error").GetProperty("code").GetString());
        using var hostOnly = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/start", new { expectedVersion = 1 }, guestToken);
        Assert.Equal(HttpStatusCode.Forbidden, hostOnly.StatusCode);

        using var readyOwner = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 1 }, ownerToken);
        using var readyGuest = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/ready", new { isReady = true, expectedVersion = 2 }, guestToken);
        using var start = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/start", new { expectedVersion = 3 }, ownerToken);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
    }

    [Fact]
    public async Task ConcurrentAcceptRequestsAddInviteeOnlyOnce()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var ownerToken = await RegisterAndLogin(client);
        var inviteeToken = await RegisterAndLogin(client);
        var inviteeName = await UsernameFromToken(client, inviteeToken);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/rooms", new { name = "Concurrent invite", isPrivate = true }, ownerToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var roomId = createJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        using var sendInvite = await Send(client, HttpMethod.Post, $"/api/v1/rooms/{roomId}/invitations", new { username = inviteeName }, ownerToken);
        using var inviteJson = await JsonDocument.ParseAsync(await sendInvite.Content.ReadAsStreamAsync());
        var invitationId = inviteJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var first = Send(client, HttpMethod.Post, $"/api/v1/invitations/{invitationId}/accept", new { }, inviteeToken);
        var second = Send(client, HttpMethod.Post, $"/api/v1/invitations/{invitationId}/accept", new { }, inviteeToken);
        using var firstResponse = await first;
        using var secondResponse = await second;
        Assert.Single(new[] { firstResponse.StatusCode, secondResponse.StatusCode }.Where(status => status == HttpStatusCode.OK));
        Assert.Single(new[] { firstResponse.StatusCode, secondResponse.StatusCode }.Where(status => status == HttpStatusCode.Conflict));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.RoomMembers.CountAsync(member => member.RoomId == roomId && member.UserId != Guid.Empty && member.LeftAt == null && member.Role == XiangqiOnline.Domain.Enums.RoomRole.Player));
        Assert.Equal(XiangqiOnline.Domain.Enums.InvitationStatus.Accepted, await db.Invitations.Where(item => item.Id == invitationId).Select(item => item.Status).SingleAsync());
    }

    [Fact]
    public async Task RoomHubNegotiationRequiresAuthentication()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var anonymousRequest = new HttpRequestMessage(HttpMethod.Post, "/hubs/rooms/negotiate?negotiateVersion=1");
        using var anonymousResponse = await client.SendAsync(anonymousRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var token = await RegisterAndLogin(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/rooms/negotiate?negotiateVersion=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.NotEmpty(json.RootElement.GetProperty("availableTransports").EnumerateArray());
    }

    [Fact]
    public async Task LocalGameSupportsBothSidesHistoryOwnershipAndRestart()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var ownerToken = await RegisterAndLogin(client);
        var outsiderToken = await RegisterAndLogin(client);

        using var create = await Send(client, HttpMethod.Post, "/api/v1/games/local", new { }, ownerToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createdJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var createdGame = createdJson.RootElement.GetProperty("data");
        var gameId = createdGame.GetProperty("id").GetGuid();
        Assert.Equal("Local", createdGame.GetProperty("mode").GetString());
        Assert.Equal("Red", createdGame.GetProperty("currentTurn").GetString());
        Assert.Equal("Local", createdGame.GetProperty("playerSide").GetString());

        using var legal = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}/legal-moves?fromFile=1&fromRank=9", null, ownerToken);
        Assert.Equal(HttpStatusCode.OK, legal.StatusCode);
        using var redMove = await Send(client, HttpMethod.Post, "/api/v1/games/local/move", new { gameId, fromFile = 1, fromRank = 9, toFile = 2, toRank = 7, expectedVersion = 0, clientMoveId = Guid.NewGuid() }, ownerToken);
        Assert.Equal(HttpStatusCode.OK, redMove.StatusCode);
        using var blackMove = await Send(client, HttpMethod.Post, "/api/v1/games/local/move", new { gameId, fromFile = 1, fromRank = 0, toFile = 2, toRank = 2, expectedVersion = 1, clientMoveId = Guid.NewGuid() }, ownerToken);
        Assert.Equal(HttpStatusCode.OK, blackMove.StatusCode);

        using var history = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}/moves", null, ownerToken);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using var historyJson = await JsonDocument.ParseAsync(await history.Content.ReadAsStreamAsync());
        Assert.Equal(2, historyJson.RootElement.GetProperty("data").GetArrayLength());

        using var outsiderRead = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}", null, outsiderToken);
        Assert.Equal(HttpStatusCode.NotFound, outsiderRead.StatusCode);
        using var outsiderRestart = await Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/restart", new { expectedVersion = 2 }, outsiderToken);
        Assert.Equal(HttpStatusCode.NotFound, outsiderRestart.StatusCode);

        using var restart = await Send(client, HttpMethod.Post, $"/api/v1/games/{gameId}/restart", new { expectedVersion = 2 }, ownerToken);
        Assert.Equal(HttpStatusCode.Created, restart.StatusCode);
        using var restartJson = await JsonDocument.ParseAsync(await restart.Content.ReadAsStreamAsync());
        var restarted = restartJson.RootElement.GetProperty("data");
        Assert.NotEqual(gameId, restarted.GetProperty("id").GetGuid());
        Assert.Equal(0, restarted.GetProperty("ply").GetInt32());
        Assert.Equal("Red", restarted.GetProperty("currentTurn").GetString());

        using var originalAfterRestart = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}", null, ownerToken);
        using var originalJson = await JsonDocument.ParseAsync(await originalAfterRestart.Content.ReadAsStreamAsync());
        Assert.Equal("Abandoned", originalJson.RootElement.GetProperty("data").GetProperty("status").GetString());
        using var preservedHistory = await Send(client, HttpMethod.Get, $"/api/v1/games/{gameId}/moves", null, ownerToken);
        using var preservedJson = await JsonDocument.ParseAsync(await preservedHistory.Content.ReadAsStreamAsync());
        Assert.Equal(2, preservedJson.RootElement.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task ConcurrentLocalMovesCannotBothAdvanceTheSameVersion()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/games/local", new { }, token);
        using var createdJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var gameId = createdJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var first = Send(client, HttpMethod.Post, "/api/v1/games/local/move", new { gameId, fromFile = 1, fromRank = 9, toFile = 2, toRank = 7, expectedVersion = 0, clientMoveId = Guid.NewGuid() }, token);
        var second = Send(client, HttpMethod.Post, "/api/v1/games/local/move", new { gameId, fromFile = 0, fromRank = 6, toFile = 0, toRank = 5, expectedVersion = 0, clientMoveId = Guid.NewGuid() }, token);
        using var firstResponse = await first;
        using var secondResponse = await second;
        var statuses = new[] { firstResponse.StatusCode, secondResponse.StatusCode };
        Assert.Single(statuses.Where(status => status == HttpStatusCode.OK));
        Assert.Single(statuses.Where(status => status == HttpStatusCode.Conflict));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Games.Where(game => game.Id == gameId).Select(game => game.Version).SingleAsync());
        Assert.Equal(1, await db.GameMoves.CountAsync(move => move.GameId == gameId));
    }

    [Fact]
    public async Task AiGameValidatesHumanMovesPersistsBothSidesAndLetsAiOpenAsRed()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await RegisterAndLogin(client);

        using var createRed = await Send(client, HttpMethod.Post, "/api/v1/ai-games", new { difficulty = "Easy", humanSide = "Red" }, token);
        Assert.Equal(HttpStatusCode.Created, createRed.StatusCode);
        using var redJson = await JsonDocument.ParseAsync(await createRed.Content.ReadAsStreamAsync());
        var redGame = redJson.RootElement.GetProperty("data");
        var redGameId = redGame.GetProperty("gameId").GetGuid();

        using var invalid = await Send(client, HttpMethod.Post, $"/api/v1/ai-games/{redGameId}/moves",
            new { fromFile = 0, fromRank = 6, toFile = 0, toRank = 4, expectedVersion = 0, clientMoveId = Guid.NewGuid() }, token);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var humanMove = await Send(client, HttpMethod.Post, $"/api/v1/ai-games/{redGameId}/moves",
            new { fromFile = 1, fromRank = 9, toFile = 2, toRank = 7, expectedVersion = 0, clientMoveId = Guid.NewGuid() }, token);
        Assert.Equal(HttpStatusCode.OK, humanMove.StatusCode);
        await WaitForAiMove(client, token, redGameId, expectedVersion: 2);

        using var historyResponse = await Send(client, HttpMethod.Get, $"/api/v1/ai-games/{redGameId}/moves", null, token);
        using var history = await JsonDocument.ParseAsync(await historyResponse.Content.ReadAsStreamAsync());
        var moves = history.RootElement.GetProperty("data").GetProperty("moves").EnumerateArray().ToArray();
        Assert.Equal(2, moves.Length);
        Assert.False(moves[0].GetProperty("isAiMove").GetBoolean());
        Assert.True(moves[1].GetProperty("isAiMove").GetBoolean());

        using var createBlack = await Send(client, HttpMethod.Post, "/api/v1/ai-games", new { difficulty = "Easy", humanSide = "Black" }, token);
        Assert.Equal(HttpStatusCode.Created, createBlack.StatusCode);
        using var blackJson = await JsonDocument.ParseAsync(await createBlack.Content.ReadAsStreamAsync());
        var blackGameId = blackJson.RootElement.GetProperty("data").GetProperty("gameId").GetGuid();
        var duplicateWorkers = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<XiangqiOnline.Application.AiGames.IAiTurnProcessor>();
            await processor.ProcessAsync(blackGameId, CancellationToken.None);
        }));
        await Task.WhenAll(duplicateWorkers);
        await WaitForAiMove(client, token, blackGameId, expectedVersion: 1);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.GameMoves.CountAsync(move => move.GameId == blackGameId && move.IsAiMove));
        }
    }

    [Fact]
    public async Task AiGameResignRestartAndOwnershipAreEnforced()
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var ownerToken = await RegisterAndLogin(client);
        var outsiderToken = await RegisterAndLogin(client);
        using var create = await Send(client, HttpMethod.Post, "/api/v1/ai-games", new { difficulty = "Medium", humanSide = "Red" }, ownerToken);
        using var createJson = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var gameId = createJson.RootElement.GetProperty("data").GetProperty("gameId").GetGuid();

        using var forbidden = await Send(client, HttpMethod.Get, $"/api/v1/ai-games/{gameId}", null, outsiderToken);
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        using var resign = await Send(client, HttpMethod.Post, $"/api/v1/ai-games/{gameId}/resign", new { expectedVersion = 0 }, ownerToken);
        Assert.Equal(HttpStatusCode.OK, resign.StatusCode);
        using var resignJson = await JsonDocument.ParseAsync(await resign.Content.ReadAsStreamAsync());
        Assert.Equal("Abandoned", resignJson.RootElement.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal("Black", resignJson.RootElement.GetProperty("data").GetProperty("winnerSide").GetString());

        using var restart = await Send(client, HttpMethod.Post, $"/api/v1/ai-games/{gameId}/restart", new { expectedVersion = 1 }, ownerToken);
        Assert.Equal(HttpStatusCode.OK, restart.StatusCode);
        using var restartJson = await JsonDocument.ParseAsync(await restart.Content.ReadAsStreamAsync());
        var newId = restartJson.RootElement.GetProperty("data").GetProperty("gameId").GetGuid();
        Assert.NotEqual(gameId, newId);
        Assert.Equal("Medium", restartJson.RootElement.GetProperty("data").GetProperty("difficulty").GetString());
    }

    private static async Task WaitForAiMove(HttpClient client, string token, Guid gameId, long expectedVersion)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!timeout.IsCancellationRequested)
        {
            using var response = await Send(client, HttpMethod.Get, $"/api/v1/ai-games/{gameId}", null, token);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(), cancellationToken: timeout.Token);
                if (json.RootElement.GetProperty("data").GetProperty("version").GetInt64() >= expectedVersion) return;
            }
            await Task.Delay(150, timeout.Token);
        }
        Assert.Fail("AI worker did not commit a move before the integration-test timeout.");
    }

    private async Task<string> RegisterAndLogin(HttpClient client)
    {
        var unique = Guid.NewGuid().ToString("N");
        var username = $"Room_{unique}"[..18];
        var email = $"{unique}@example.com";
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { username, email, password = "Strong horse 456!", confirmPassword = "Strong horse 456!" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = username, password = "Strong horse 456!", rememberMe = true });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var json = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> UsernameFromToken(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("data").GetProperty("username").GetString()!;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body, string token)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private HubConnection BuildGameConnection(string token) => new HubConnectionBuilder()
        .WithUrl("http://localhost/hubs/game", options =>
        {
            options.HttpMessageHandlerFactory = _ => fixture.Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;
            options.AccessTokenProvider = () => Task.FromResult<string?>(token);
        })
        .Build();
}
