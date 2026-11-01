using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using XiangqiOnline.Application.Authentication;
using XiangqiOnline.Domain.Entities;
using XiangqiOnline.Infrastructure.Persistence;

namespace XiangqiOnline.Infrastructure.Authentication;

public sealed class EfAuthRepository(AppDbContext db) : IAuthRepository
{
    public Task<User?> FindUserByIdentifierAsync(string normalizedIdentifier, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.NormalizedUsername == normalizedIdentifier || user.NormalizedEmail == normalizedIdentifier, cancellationToken);

    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public async Task CreateUserAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(user);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (FindSqlException(exception) is { Number: 2601 or 2627 } sqlException)
        {
            var field = sqlException.Message.Contains("NormalizedEmail", StringComparison.OrdinalIgnoreCase) ? "email" : "username";
            throw new DuplicateUserException(field);
        }
    }

    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshRotationResult> RotateRefreshTokenAsync(string presentedHash, RefreshToken replacement, DateTime now, TimeSpan reuseGracePeriod, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var previous = await db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == presentedHash, cancellationToken);
            if (previous is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new RefreshRotationResult(RefreshRotationStatus.Invalid, null);
            }

            var user = await db.Users.SingleOrDefaultAsync(item => item.Id == previous.UserId, cancellationToken);
            if (user is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new RefreshRotationResult(RefreshRotationStatus.Invalid, null);
            }

            if (previous.RevokedAt.HasValue)
            {
                if (previous.ReplacedByTokenId.HasValue && now - previous.RevokedAt.Value > reuseGracePeriod)
                {
                    await db.RefreshTokens.Where(token => token.UserId == previous.UserId && token.RevokedAt == null)
                        .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return new RefreshRotationResult(RefreshRotationStatus.ReuseDetected, null);
                }

                await transaction.CommitAsync(cancellationToken);
                return new RefreshRotationResult(RefreshRotationStatus.ConcurrentReplay, null);
            }

            if (previous.ExpiresAt <= now)
            {
                previous.RevokedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new RefreshRotationResult(RefreshRotationStatus.Invalid, null);
            }

            previous.RevokedAt = now;
            previous.ReplacedByTokenId = replacement.Id;
            replacement.UserId = previous.UserId;
            db.RefreshTokens.Add(replacement);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RefreshRotationResult(RefreshRotationStatus.Rotated, user, previous.IsPersistent);
        });
    }

    public async Task RevokeRefreshTokenAsync(string tokenHash, DateTime now, CancellationToken cancellationToken) =>
        await db.RefreshTokens.Where(token => token.TokenHash == tokenHash && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);

    public async Task RevokeAllRefreshTokensAsync(Guid userId, DateTime now, CancellationToken cancellationToken) =>
        await db.RefreshTokens.Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);

    public async Task UpdateUserAndRevokeSessionsAsync(User user, DateTime now, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.Users.Update(user);
            await db.SaveChangesAsync(cancellationToken);
            await db.RefreshTokens.Where(token => token.UserId == user.Id && token.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Update(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
            if (current is SqlException sql) return sql;
        return null;
    }
}
