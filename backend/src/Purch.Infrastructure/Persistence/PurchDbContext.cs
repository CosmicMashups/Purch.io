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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        _ = configurationBuilder.Properties<decimal>().HavePrecision(14, 4);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(PurchDbContext).Assembly);

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
        return entity => _currentTenantProvider.TenantId == null || entity.TenantId == _currentTenantProvider.TenantId;
    }
}
