using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace GoldenPappadam.Api.Common;

/// <summary>
/// The transaction a service needs for its own work, or the caller's when one is already open. SQL Server
/// cannot nest transactions, and the phone's sync opens one per item so that the record and the row saying
/// it arrived commit together - so the services it calls join that one instead of starting their own.
///
/// Only the owner commits or rolls back. When joined, <see cref="CommitAsync"/> and <see cref="RollbackAsync"/>
/// do nothing, and a failure simply propagates for the owner to roll everything back. Called from the
/// office, every service still owns its transaction exactly as before.
/// </summary>
public sealed class ServiceTransaction : IAsyncDisposable
{
    private readonly IDbContextTransaction? _owned;

    private ServiceTransaction(IDbContextTransaction? owned) => _owned = owned;

    /// <summary>False when this joined the caller's transaction.</summary>
    public bool IsOwned => _owned is not null;

    public static async Task<ServiceTransaction> BeginOrJoinAsync(DatabaseFacade database, CancellationToken ct) =>
        new(database.CurrentTransaction is null ? await database.BeginTransactionAsync(ct) : null);

    public Task CommitAsync(CancellationToken ct) => _owned?.CommitAsync(ct) ?? Task.CompletedTask;

    public Task RollbackAsync(CancellationToken ct) => _owned?.RollbackAsync(ct) ?? Task.CompletedTask;

    public ValueTask DisposeAsync() => _owned?.DisposeAsync() ?? ValueTask.CompletedTask;
}

public static class ServiceTransactionExtensions
{
    /// <inheritdoc cref="ServiceTransaction"/>
    public static Task<ServiceTransaction> BeginOrJoinTransactionAsync(this DatabaseFacade database, CancellationToken ct) =>
        ServiceTransaction.BeginOrJoinAsync(database, ct);
}
