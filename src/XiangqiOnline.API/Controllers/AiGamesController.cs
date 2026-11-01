using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XiangqiOnline.API.Infrastructure;
using XiangqiOnline.Application.AiGames;
using XiangqiOnline.Contracts;
using XiangqiOnline.Contracts.AiGames;
using XiangqiOnline.Domain.Enums;

namespace XiangqiOnline.API.Controllers;

[ApiController, Authorize, Route("api/v1/ai-games")]
public sealed class AiGamesController(IAiGameService games) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateAiGameRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<AiDifficulty>(request.Difficulty, true, out var difficulty) || !Enum.IsDefined(difficulty))
            return BadRequest(ApiResponses.Failure("AI_DIFFICULTY_INVALID", "Chọn độ khó Dễ, Vừa hoặc Khó.", HttpContext));
        if (!Enum.TryParse<Side>(request.HumanSide, true, out var side) || !Enum.IsDefined(side))
            return BadRequest(ApiResponses.Failure("AI_SIDE_INVALID", "Chọn bên Đỏ hoặc Đen.", HttpContext));
        var result = await games.CreateAiGameAsync(CurrentUserId(), difficulty, side, ct);
        return result.Game is null ? BadRequest(ApiResponses.Failure("AI_GAME_CREATE_FAILED", "Không tạo được ván AI.", HttpContext))
            : StatusCode(StatusCodes.Status201Created, ApiResponses.Success(ToResponse(result.Game), HttpContext));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await games.GetAiGameAsync(id, CurrentUserId(), ct);
        return result.Game is null ? NotFound(ApiResponses.Failure("AI_GAME_NOT_FOUND", "Không tìm thấy ván AI.", HttpContext))
            : Ok(ApiResponses.Success(ToResponse(result.Game), HttpContext));
    }

    [HttpPost("{id:guid}/moves")]
    public async Task<IActionResult> SubmitMove(Guid id, SubmitHumanMoveRequest request, CancellationToken ct)
    {
        var result = await games.SubmitHumanMoveAsync(id, CurrentUserId(), request.FromFile, request.FromRank, request.ToFile, request.ToRank,
            request.ExpectedVersion, request.ClientMoveId, ct);
        if (result.Game is null) return ToError(result.Status);
        if (result.Status == AiGameOperationStatus.Ok)
            return Ok(ApiResponses.Success(new SubmitHumanMoveResponse(true, result.MoveId, result.Game.Fen, result.Game.CurrentTurn.ToString(),
                result.Game.Version, result.Game.AiTurnStatus.ToString(), result.Game.Status.ToString(), result.Duplicate), HttpContext));
        return ToError(result.Status, result.Game);
    }

    [HttpGet("{id:guid}/moves")]
    public async Task<IActionResult> GetMoves(Guid id, CancellationToken ct)
    {
        var history = await games.GetAiMoveHistoryAsync(id, CurrentUserId(), ct);
        return history is null ? NotFound(ApiResponses.Failure("AI_GAME_NOT_FOUND", "Không tìm thấy ván AI.", HttpContext))
            : Ok(ApiResponses.Success(new AiGameHistoryResponse(ToResponse(history.Game), history.Moves.Select(ToResponse).ToArray()), HttpContext));
    }

    [HttpPost("{id:guid}/restart")]
    public async Task<IActionResult> Restart(Guid id, RestartAiGameRequest request, CancellationToken ct)
    {
        var result = await games.RestartAiGameAsync(id, CurrentUserId(), request.ExpectedVersion, ct);
        return result.Game is null ? ToError(result.Status) : Ok(ApiResponses.Success(ToResponse(result.Game), HttpContext));
    }

    [HttpPost("{id:guid}/resign")]
    public async Task<IActionResult> Resign(Guid id, ResignAiGameRequest request, CancellationToken ct)
    {
        var result = await games.ResignAiGameAsync(id, CurrentUserId(), request.ExpectedVersion, ct);
        return result.Game is null ? ToError(result.Status) : Ok(ApiResponses.Success(ToResponse(result.Game), HttpContext));
    }

    [HttpGet("{id:guid}/analysis-status")]
    public async Task<IActionResult> GetAnalysisStatus(Guid id, CancellationToken ct)
    {
        var status = await games.GetAnalysisStatusAsync(id, CurrentUserId(), ct);
        return status is null ? NotFound(ApiResponses.Failure("AI_GAME_NOT_FOUND", "Không tìm thấy ván AI.", HttpContext))
            : Ok(ApiResponses.Success(new AiAnalysisStatusResponse(status.GameId, status.Status.ToString(), (int)status.CompletedDepth,
                status.ElapsedMs, status.NodesSearched, status.Version), HttpContext));
    }

    private IActionResult ToError(AiGameOperationStatus status, AiGameInfo? game = null) => status switch
    {
        AiGameOperationStatus.Forbidden => StatusCode(403, ApiResponses.Failure("AI_GAME_FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.", HttpContext)),
        AiGameOperationStatus.InvalidMove => BadRequest(ApiResponses.Failure("AI_MOVE_INVALID", "Nước đi không hợp lệ theo luật Cờ Tướng.", HttpContext)),
        AiGameOperationStatus.WrongTurn => Conflict(ApiResponses.Failure("AI_WRONG_TURN", "Chưa đến lượt bạn hoặc AI đang suy nghĩ.", HttpContext)),
        AiGameOperationStatus.Finished => Conflict(ApiResponses.Failure("AI_GAME_FINISHED", "Ván đấu đã kết thúc.", HttpContext)),
        AiGameOperationStatus.IdempotencyConflict => Conflict(ApiResponses.Failure("AI_MOVE_IDEMPOTENCY_CONFLICT", "Mã nước đi đã được dùng cho nước khác.", HttpContext)),
        AiGameOperationStatus.SearchConflict or AiGameOperationStatus.VersionConflict => Conflict(ApiResponses.Failure("AI_GAME_VERSION_CONFLICT", "Ván đã thay đổi. Hãy tải lại trạng thái.", HttpContext)),
        _ => NotFound(ApiResponses.Failure("AI_GAME_NOT_FOUND", "Không tìm thấy ván AI.", HttpContext))
    };

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : Guid.Empty;
    private static AiGameResponse ToResponse(AiGameInfo g) => new(g.GameId, g.Difficulty.ToString(), g.HumanSide.ToString(), g.AiSide.ToString(),
        g.Fen, g.CurrentTurn.ToString(), g.Status.ToString(), g.AiTurnStatus.ToString(), g.Version, g.WinnerSide?.ToString(),
        g.ResultReason?.ToString(), g.EndedAtUtc, g.Error, g.IsCurrentTurnInCheck);
    private static AiGameMoveResponse ToResponse(AiMoveInfo m) => new(m.Ply, m.FromSquare, m.ToSquare, m.Piece, m.CapturedPiece, m.IsAiMove, m.CreatedAtUtc);
}
