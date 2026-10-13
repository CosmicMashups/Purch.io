using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Purch.Application.Auth;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Auth;

/// <summary>
/// Drops a device's cached session state the moment its row is changed or deleted, so a revoke, deactivation or
/// pairing reset takes effect on the next request of this instance rather than after the cache TTL. Other
/// instances still converge within the TTL. Evicts after the commit; a request that re-reads in between sees the
/// old row and is corrected by the same TTL.
/// </summary>
public sealed class DeviceSessionCacheEvictionInterceptor(IMemoryCache cache) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<string>> _pending = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Evict(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Evict(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is { } context) _pending.Remove(context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) _pending.Remove(context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Collect(DbContext? context)
    {
        if (context is null) return;
        List<string>? keys = null;
        foreach (var entry in context.ChangeTracker.Entries<Device>())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted)) continue;
            keys ??= [];
            keys.Add(DeviceSessionCacheKey.For(entry.Entity.TenantId, entry.Entity.Id));
        }

        if (keys is not null) _pending.AddOrUpdate(context, keys);
    }

    private void Evict(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var keys)) return;
        foreach (var key in keys) cache.Remove(key);
        _pending.Remove(context);
    }
}
