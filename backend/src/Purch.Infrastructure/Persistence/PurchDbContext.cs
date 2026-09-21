using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Purch.Application.Common;
using Purch.Domain.Common;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Persistence;

public class PurchDbContext(DbContextOptions<PurchDbContext> options, ICurrentTenantProvider currentTenantProvider) : DbContext(options)
{
    private readonly ICurrentTenantProvider _currentTenantProvider = currentTenantProvider;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<ItemRecipeLine> ItemRecipeLines => Set<ItemRecipeLine>();

    public DbSet<ItemVariant> ItemVariants => Set<ItemVariant>();

    public DbSet<ItemComboComponent> ItemComboComponents => Set<ItemComboComponent>();

    public DbSet<ItemBatch> ItemBatches => Set<ItemBatch>();

    public DbSet<BundlePromoRule> BundlePromoRules => Set<BundlePromoRule>();

    public DbSet<ModifierGroup> ModifierGroups => Set<ModifierGroup>();

    public DbSet<ItemModifier> ItemModifiers => Set<ItemModifier>();

    public DbSet<ItemModifierGroup> ItemModifierGroups => Set<ItemModifierGroup>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<CustomerCreditLedger> CustomerCreditLedgers => Set<CustomerCreditLedger>();

    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();

    public DbSet<BranchTransfer> BranchTransfers => Set<BranchTransfer>();

    public DbSet<BranchTransferLine> BranchTransferLines => Set<BranchTransferLine>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<TransactionLine> TransactionLines => Set<TransactionLine>();

    public DbSet<TransactionLineComboSelection> TransactionLineComboSelections => Set<TransactionLineComboSelection>();

    public DbSet<TransactionLineModifierSelection> TransactionLineModifierSelections => Set<TransactionLineModifierSelection>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentGatewayTransaction> PaymentGatewayTransactions => Set<PaymentGatewayTransaction>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<ReceiptSequence> ReceiptSequences => Set<ReceiptSequence>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();

    public DbSet<BogoPromoRule> BogoPromoRules => Set<BogoPromoRule>();

    public DbSet<ComboPromoRule> ComboPromoRules => Set<ComboPromoRule>();

    public DbSet<ItemDiscountPromoRule> ItemDiscountPromoRules => Set<ItemDiscountPromoRule>();

    public DbSet<TenantMetering> TenantMeterings => Set<TenantMetering>();

    public DbSet<SyncedRecord> SyncedRecords => Set<SyncedRecord>();

    public DbSet<KioskPrepSequence> KioskPrepSequences => Set<KioskPrepSequence>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampUpdatedAt();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampUpdatedAt();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Every inserted or changed entity gets UpdatedAt = now, which is what the catalog ETag
    /// (and any later change feed) keys off. ChangeTracker.Entries runs change detection first.</summary>
    private void StampUpdatedAt()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        _ = configurationBuilder.Properties<decimal>().HavePrecision(14, 4);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(PurchDbContext).Assembly);

        // Optimistic concurrency on the rows that are read, changed in memory, then saved: the
        // cart being paid, stock counters, and a customer's credit balance. Postgres' own xmin
        // system column is the version (Npgsql maps IsRowVersion() to it, so there is no extra
        // column). Two requests that loaded the same row can no longer both save — the second gets
        // a DbUpdateConcurrencyException (a 409) instead of double-charging or losing an update.
        _ = modelBuilder.Entity<Transaction>().Property<uint>("Version").IsRowVersion();
        _ = modelBuilder.Entity<Item>().Property<uint>("Version").IsRowVersion();
        _ = modelBuilder.Entity<InventoryItem>().Property<uint>("Version").IsRowVersion();
        _ = modelBuilder.Entity<CustomerCreditLedger>().Property<uint>("Version").IsRowVersion();

        // Same idea for the rows whose status or counters are checked and then changed: a transfer being
        // shipped or cancelled, the receipt sequence (which also carries the Z-reading counters), a shift
        // being closed, and a purchase order line being received.
        _ = modelBuilder.Entity<BranchTransfer>().Property<uint>("Version").IsRowVersion();
        _ = modelBuilder.Entity<ReceiptSequence>().Property<uint>("Version").IsRowVersion();
        _ = modelBuilder.Entity<Shift>().Property<uint>("Version").IsRowVersion();
        _ = modelBuilder.Entity<PurchaseOrder>().Property<uint>("Version").IsRowVersion();
        _ = modelBuilder.Entity<PurchaseOrderLine>().Property<uint>("Version").IsRowVersion();

        // Single enforcement point for shared-database multi-tenant isolation (NFR14):
        // every ITenantScoped entity gets this filter applied, so no repository/query
        // can accidentally omit it.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(PurchDbContext)
                .GetMethod(nameof(BuildTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            var filter = method.Invoke(this, null);
            entityType.SetQueryFilter((LambdaExpression)filter!);
        }
    }

    private Expression<Func<TEntity, bool>> BuildTenantFilter<TEntity>()
        where TEntity : class, ITenantScoped
    {
        // Fail closed: with no tenant in context (an anonymous request, a background job, a bug) a query
        // sees nothing, rather than every tenant's rows. The few reads that legitimately span tenants
        // (pairing code, email, refresh/reset token lookups) opt out explicitly with IgnoreQueryFilters().
        return entity => _currentTenantProvider.TenantId != null && entity.TenantId == _currentTenantProvider.TenantId;
    }
}
