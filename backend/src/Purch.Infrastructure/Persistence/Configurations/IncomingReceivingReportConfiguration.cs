using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence.Configurations;

public class IncomingReceivingReportConfiguration : IEntityTypeConfiguration<IncomingReceivingReport>
{
    public void Configure(EntityTypeBuilder<IncomingReceivingReport> builder)
    {
        _ = builder.HasIndex(report => report.PurchaseOrderId);
        _ = builder.HasIndex(report => report.SupplierId);
    }
}

public class IncomingReceivingReportLineConfiguration : IEntityTypeConfiguration<IncomingReceivingReportLine>
{
    public void Configure(EntityTypeBuilder<IncomingReceivingReportLine> builder)
    {
        _ = builder.HasIndex(line => line.ReportId);
    }
}
