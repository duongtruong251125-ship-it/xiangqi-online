using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XiangqiOnline.API.Infrastructure;
using XiangqiOnline.Application.Authentication;
using XiangqiOnline.Application.Games;
using XiangqiOnline.Contracts.Games;
using XiangqiOnline.Contracts;
using XiangqiOnline.GameEngine;

namespace XiangqiOnline.API.Controllers;

[ApiController, Authorize]
[Route("api/v1/games")]
public sealed class GamesController(IGameplayService games, IXiangqiRules rules, IGameEventNotifier events, TimeProvider timeProvider) : ControllerBase
{
    [HttpPost("local")]
    public async Task<IActionResult> CreateLocal(CancellationToken cancellationToken)
    {
        var game = await games.CreateLocalAsync(CurrentUserId(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = game.Id }, ApiResponses.Success(ToResponse(game, CurrentUserId()), HttpContext));
    }

    [HttpPost("local/move")]
    public Task<IActionResult> ApplyLocalMove(ApplyLocalGameMoveRequest request, CancellationToken cancellationToken) =>
        ApplyMoveCore(request.GameId, request.FromFile, request.FromRank, request.ToFile, request.ToRank, request.ExpectedVersion, request.ClientMoveId, cancellationToken);

    [HttpGet("{id:guid}/moves")]
    public async Task<IActionResult> GetMoveHistory(Guid id, CancellationToken cancellationToken)
    {
        var moves = await games.GetMoveHistoryAsync(id, CurrentUserId(), cancellationToken);
        return moves is null
            ? NotFound(ApiResponses.Failure("GAME_NOT_FOUND", "Không tìm thấy ván đấu hoặc bạn không có quyền xem.", HttpContext))
            : Ok(ApiResponses.Success(moves.Select(move => new GameMoveResponse(move.Ply, move.FromSquare, move.ToSquare, move.Piece, move.CapturedPiece, move.CreatedAt)).ToArray(), HttpContext));
    }

    [HttpGet("{id:guid}/snapshot")]
    public async Task<IActionResult> GetSnapshot(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await games.GetSyncAsync(id, CurrentUserId(), cancellationToken);
        return snapshot is null
            ? NotFound(ApiResponses.Failure("GAME_NOT_FOUND", "Không tìm thấy ván đấu hoặc bạn không có quyền xem.", HttpContext))
            : Ok(ApiResponses.Success(new GameSnapshotResponse(ToResponse(snapshot.Game, CurrentUserId()), snapshot.RedPlayerName,
                snapshot.BlackPlayerName, ToMoveResponses(snapshot.Moves)), HttpContext));
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken cancellationToken)
    {
        var game = await games.GetAsync(id, CurrentUserId(), cancellationToken);
        return game is null
            ? NotFound(ApiResponses.Failure("GAME_NOT_FOUND", "Không tìm thấy ván đấu hoặc bạn không có quyền xem.", HttpContext))
            : Ok(ApiResponses.Success(new GameStatusResponse(game.Id, game.Status.ToString(), game.CurrentTurn.ToString(), game.WinnerId,
                game.ResultReason?.ToString(), game.Version, game.EndedAtUtc, game.RedRemainingMs, game.BlackRemainingMs,
                game.TurnStartedAtUtc, timeProvider.GetUtcNow().UtcDateTime), HttpContext));
    }

    [HttpPost("{id:guid}/restart")]
    public async Task<IActionResult> RestartLocal(Guid id, RestartGameRequest request, CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion < 0) return BadRequest(ApiResponses.Failure("VALIDATION_ERROR", "Phiên bản ván đấu không hợp lệ.", HttpContext));
        var result = await games.RestartLocalAsync(id, CurrentUserId(), request.ExpectedVersion, cancellationToken);
        if (result.Status == RestartGameStatus.Restarted)
            return CreatedAtAction(nameof(Get), new { id = result.Game!.Id }, ApiResponses.Success(ToResponse(result.Game!, CurrentUserId()), HttpContext));
        return result.Status switch
        {
            RestartGameStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ApiResponses.Failure("GAME_FORBIDDEN", "Chỉ chủ sở hữu mới có thể khởi động lại ván local.", HttpContext)),
            RestartGameStatus.VersionConflict => Conflict(ApiResponses.Failure("VERSION_CONFLICT", "Ván đấu đã thay đổi. Hãy tải lại trạng thái mới.", HttpContext)),
            _ => NotFound(ApiResponses.Failure("GAME_NOT_FOUND", "Không tìm thấy ván local hoặc bạn không có quyền.", HttpContext))
        };
    }

    [HttpGet("{id:guid}/legal-moves")]
    public async Task<IActionResult> GetLegalMoves(Guid id, [FromQuery] int fromFile, [FromQuery] int fromRank, CancellationToken cancellationToken)
    {
        var result = await games.GetLegalMovesAsync(id, CurrentUserId(), fromFile, fromRank, cancellationToken);
        return result.Status switch
        {
            ApplyMoveStatus.Applied => Ok(ApiResponses.Success(result.Moves.Select(move => new { file = move.To.File, rank = move.To.Rank }).ToArray(), HttpContext)),
            ApplyMoveStatus.WrongTurn => Conflict(ApiResponses.Failure("MOVE_WRONG_TURN", "Chưa đến lượt của bạn.", HttpContext)),
            ApplyMoveStatus.Finished => Conflict(ApiResponses.Failure("GAME_FINISHED", "Ván đấu đã kết thúc.", HttpContext)),
            ApplyMoveStatus.InvalidMove => BadRequest(ApiResponses.Failure("POSITION_INVALID", "Không tìm thấy quân hợp lệ tại ô này.", HttpContext)),
            _ => NotFound(ApiResponses.Failure("GAME_NOT_FOUND", "Không tìm thấy ván đấu hoặc bạn không phải người chơi.", HttpContext))
        };
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var game = await games.GetAsync(id, CurrentUserId(), cancellationToken);
        return game is null
            ? NotFound(ApiResponses.Failure("GAME_NOT_FOUND", "Không tìm thấy ván đấu hoặc bạn không phải người chơi.", HttpContext))
            : Ok(ApiResponses.Success(ToResponse(game, CurrentUserId()), HttpContext));
    }

    [HttpPost("{id:guid}/moves")]
    public async Task<IActionResult> ApplyMove(Guid id, ApplyGameMoveRequest request, CancellationToken cancellationToken)
        => await ApplyMoveCore(id, request.FromFile, request.FromRank, request.ToFile, request.ToRank, request.ExpectedVersion, request.ClientMoveId, cancellationToken);

    private async Task<IActionResult> ApplyMoveCore(Guid id, int fromFile, int fromRank, int toFile, int toRank, long expectedVersion, Guid clientMoveId, CancellationToken cancellationToken)
    {
        if (clientMoveId == Guid.Empty || expectedVersion < 0)
            return BadRequest(ApiResponses.Failure("VALIDATION_ERROR", "Thông tin nước đi chưa hợp lệ.", HttpContext));
        var result = await games.ApplyMoveAsync(id, CurrentUserId(), fromFile, fromRank, toFile, toRank, expectedVersion, clientMoveId, cancellationToken);
        if (result.Move is not null) await events.MoveAppliedAsync(result.Move, cancellationToken);
        if (result.Finished is not null) await events.GameFinishedAsync(result.Finished, cancellationToken);
        return result.Status switch
        {
            ApplyMoveStatus.Applied or ApplyMoveStatus.IdempotentRetry => Ok(ApiResponses.Success(ToResponse(result.Game!, CurrentUserId()), HttpContext)),
            ApplyMoveStatus.InvalidMove => BadRequest(ApiResponses.Failure("MOVE_INVALID", "Nước đi không hợp lệ theo luật cờ Tướng.", HttpContext)),
            ApplyMoveStatus.WrongTurn => Conflict(ApiResponses.Failure("MOVE_WRONG_TURN", "Chưa đến lượt của bạn.", HttpContext)),
            ApplyMoveStatus.VersionConflict => Conflict(ApiResponses.Failure("VERSION_CONFLICT", "Ván đấu đã thay đổi. Hãy tải lại trạng thái mới.", HttpContext)),
            ApplyMoveStatus.DuplicateMove => Conflict(ApiResponses.Failure("MOVE_ALREADY_APPLIED", "Nước đi này đã được xử lý.", HttpContext)),
            ApplyMoveStatus.IdempotencyConflict => Conflict(ApiResponses.Failure("MOVE_IDEMPOTENCY_CONFLICT", "ClientMoveId đã được dùng cho một nước đi khác.", HttpContext)),
            ApplyMoveStatus.TimedOut => Conflict(ApiResponses.Failure("GAME_TIMEOUT", "Hết giờ trước khi nước đi được áp dụng.", HttpContext)),
            ApplyMoveStatus.Finished => Conflict(ApiResponses.Failure("GAME_FINISHED", "Ván đấu đã kết thúc.", HttpContext)),
            _ => NotFound(ApiResponses.Failure("GAME_NOT_FOUND", "Không tìm thấy ván đấu hoặc bạn không phải người chơi.", HttpContext))
        };
    }

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id
        : throw new AuthException("AUTH_UNAUTHORIZED", "Phiên đăng nhập không hợp lệ.", 401);

    private GameResponse ToResponse(OnlineGameInfo game, Guid userId)
    {
        var board = BoardState.FromFen(game.CurrentFen);
        var winnerSide = game.WinnerId == game.RedPlayerId ? "Red" : game.WinnerId == game.BlackPlayerId ? "Black"
            : game.Mode == XiangqiOnline.Domain.Enums.GameMode.Local && game.Status == XiangqiOnline.Domain.Enums.GameStatus.Finished
                ? (game.CurrentTurn == XiangqiOnline.Domain.Enums.Side.Red ? "Black" : "Red") : null;
        return new(game.Id, game.RoomId, game.Mode.ToString(), game.Status.ToString(), game.CurrentFen, game.CurrentTurn.ToString(),
            game.Mode == XiangqiOnline.Domain.Enums.GameMode.Local ? "Local" : game.RedPlayerId == userId ? "Red" : "Black",
            rules.IsInCheck(board, board.SideToMove), game.WinnerId, winnerSide,
            game.ResultReason?.ToString(), game.Version, game.Ply, game.RedRemainingMs, game.BlackRemainingMs,
            game.TurnStartedAtUtc, game.TimeControlInitialMs, game.TimeControlIncrementMs, timeProvider.GetUtcNow().UtcDateTime);
    }

    private static IReadOnlyList<GameMoveResponse> ToMoveResponses(IReadOnlyList<GameMoveHistoryInfo> moves) =>
        moves.Select(move => new GameMoveResponse(move.Ply, move.FromSquare, move.ToSquare, move.Piece, move.CapturedPiece, move.CreatedAt)).ToArray();
}
