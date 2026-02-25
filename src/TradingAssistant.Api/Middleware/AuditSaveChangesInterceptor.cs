using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TradingAssistant.Application.Services;
using TradingAssistant.Domain.Audit;

namespace TradingAssistant.Api.Middleware;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;

    public AuditSaveChangesInterceptor(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var auditEntries = CreateAuditEntries(eventData.Context);

        if (auditEntries.Count > 0)
        {
            eventData.Context.Set<AuditLog>().AddRange(auditEntries);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private List<AuditLog> CreateAuditEntries(DbContext context)
    {
        var auditEntries = new List<AuditLog>();
        var userId = _currentUser.IsAuthenticated ? _currentUser.UserId : (Guid?)null;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Skip AuditLog entities to avoid infinite recursion
            if (entry.Entity is AuditLog)
                continue;

            // Only audit Added, Modified, Deleted
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var entityType = entry.Entity.GetType().Name;
            var entityId = GetEntityId(entry);

            var auditLog = new AuditLog
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Modified => "Updated",
                    EntityState.Deleted => "Deleted",
                    _ => entry.State.ToString()
                },
                UserId = userId,
                Timestamp = DateTime.UtcNow
            };

            switch (entry.State)
            {
                case EntityState.Added:
                    auditLog.NewValues = SerializeValues(entry.CurrentValues);
                    break;

                case EntityState.Deleted:
                    auditLog.OldValues = SerializeValues(entry.OriginalValues);
                    break;

                case EntityState.Modified:
                    var oldValues = new Dictionary<string, object?>();
                    var newValues = new Dictionary<string, object?>();

                    foreach (var property in entry.Properties)
                    {
                        if (!property.IsModified)
                            continue;

                        var propertyName = property.Metadata.Name;
                        oldValues[propertyName] = property.OriginalValue;
                        newValues[propertyName] = property.CurrentValue;
                    }

                    if (oldValues.Count > 0)
                    {
                        auditLog.OldValues = JsonSerializer.Serialize(oldValues);
                        auditLog.NewValues = JsonSerializer.Serialize(newValues);
                    }
                    break;
            }

            auditEntries.Add(auditLog);
        }

        return auditEntries;
    }

    private static string GetEntityId(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count == 0)
            return string.Empty;

        var keyValues = keyProperties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "null");

        return string.Join(",", keyValues);
    }

    private static string SerializeValues(PropertyValues values)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var property in values.Properties)
        {
            dict[property.Name] = values[property];
        }
        return JsonSerializer.Serialize(dict);
    }
}
