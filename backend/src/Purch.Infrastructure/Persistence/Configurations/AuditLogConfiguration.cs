using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        _ = builder.HasIndex(a => new { a.TenantId, a.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_TenantCreatedAt");
        _ = builder.HasIndex(a => new { a.TenantId, a.ActorUserId, a.ActionType })
            .HasDatabaseName("IX_AuditLogs_Actor_Action");
    }
}
