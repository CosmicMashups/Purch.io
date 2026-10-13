using System.Reflection;
using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Application.Reporting;
using Purch.Domain.Entities;

namespace Purch.UnitTests.Reporting;

public sealed class TransactionExportCapTests
{
    [Fact]
    public async Task A_period_with_more_sales_than_the_cap_is_refused_rather_than_truncated()
    {
        var service = ServiceReturning(IReportingRepository.MaxExportRows + 1);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            service.GenerateTransactionsCsvAsync(null, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow));

        Assert.Contains("range", error.Errors.Keys);
    }

    [Fact]
    public async Task A_period_with_exactly_the_cap_is_exported_in_full()
    {
        var service = ServiceReturning(IReportingRepository.MaxExportRows);

        var csv = await service.GenerateTransactionsCsvAsync(null, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        // Header plus one line per sale.
        Assert.Equal(IReportingRepository.MaxExportRows + 1, csv.TrimEnd('\n', '\r').Split('\n').Length);
    }

    private static TransactionExportService ServiceReturning(int saleCount)
    {
        var sales = Enumerable.Range(0, saleCount).Select(_ => new Transaction { BranchId = Guid.NewGuid() }).ToList();
        return new TransactionExportService(
            Stub<IReportingRepository>(new() { [nameof(IReportingRepository.ListCompletedTransactionsAsync)] = sales }),
            Stub<IBranchRepository>(new() { [nameof(IBranchRepository.ListByTenantAsync)] = new List<Branch>() }),
            Stub<IUserRepository>(new() { [nameof(IUserRepository.ListActorsAsync)] = new List<User>() }),
            Stub<ICurrentTenantProvider>(new() { ["get_" + nameof(ICurrentTenantProvider.TenantId)] = (Guid?)Guid.NewGuid() }),
            Stub<IReportScopeResolver>(new() { [nameof(IReportScopeResolver.ResolveBranchIdAsync)] = (Guid?)null }));
    }

    private static T Stub<T>(Dictionary<string, object?> results)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, ResultProxy>();
        ((ResultProxy)(object)proxy).Results = results;
        return proxy;
    }

    /// <summary>Answers each interface call with a canned value (wrapped in a completed Task when the method is async),
    /// and fails loudly on anything the test did not set up.</summary>
    public class ResultProxy : DispatchProxy
    {
        public Dictionary<string, object?> Results { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var method = targetMethod ?? throw new InvalidOperationException("No method.");
            if (!Results.TryGetValue(method.Name, out var value))
            {
                throw new InvalidOperationException($"Unexpected call to {method.Name}.");
            }

            if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var inner = method.ReturnType.GetGenericArguments()[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, [value]);
            }

            return value;
        }
    }
}
