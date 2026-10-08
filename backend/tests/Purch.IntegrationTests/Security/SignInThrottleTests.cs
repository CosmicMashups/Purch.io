using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Application.Onboarding;
using Purch.Common.TestUtilities;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Security;

/// <summary>Password guessing is slowed per email address: five misses start a short, growing block that ends by itself,
/// applies to unknown emails exactly as to real ones, and is cleared by a correct password.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class SignInThrottleTests(PostgresContainerFixture postgres)
{
    private const string Password = "correct horse battery";

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private PurchDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        return new PurchDbContext(options, new TestCurrentTenantProvider());
    }

    private static async Task<string> RegisterAsync(PurchApiFactory factory)
    {
        using var client = factory.CreateClient();
        var email = NewEmail();
        var response = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest($"Shop {Guid.NewGuid():N}", BusinessType.ConvenienceStore, "Main", "Ana Reyes", "123412", email, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return email;
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password)
        => client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, password));

    [Fact]
    public async Task Five_wrong_passwords_block_that_email_even_for_the_right_password_and_leave_others_alone()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var email = await RegisterAsync(factory);
        var other = await RegisterAsync(factory);
        using var client = factory.CreateClient();

        for (var i = 0; i < SignInThrottlePolicy.FreeAttempts; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, email, "wrong password " + i)).StatusCode);
        }

        var blocked = await SignInAsync(client, email, Password);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        // A different account, from the same address, is unaffected.
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, other, Password)).StatusCode);
    }

    [Fact]
    public async Task An_unknown_email_is_throttled_exactly_like_a_real_one()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var email = NewEmail();

        for (var i = 0; i < SignInThrottlePolicy.FreeAttempts; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, email, "guess " + i)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await SignInAsync(client, email, "guess again")).StatusCode);
    }

    [Fact]
    public async Task The_block_ends_by_itself_and_a_correct_password_clears_the_count()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var email = await RegisterAsync(factory);
        using var client = factory.CreateClient();
        for (var i = 0; i < SignInThrottlePolicy.FreeAttempts; i++)
        {
            _ = await SignInAsync(client, email, "wrong " + i);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await SignInAsync(client, email, Password)).StatusCode);

        // Time passes: the block is over, the right password works, and the history is gone.
        await using (var db = NewContext())
        {
            var hash = SignInThrottlePolicy.HashEmail(AccountService.NormalizeEmail(email));
            var row = await db.SignInThrottles.SingleAsync(t => t.EmailHash == hash);

            // The block the database computed matches the C# policy (one minute after the fifth miss).
            var length = row.BlockedUntil!.Value - DateTimeOffset.UtcNow;
            Assert.InRange(length, SignInThrottlePolicy.BlockFor(SignInThrottlePolicy.FreeAttempts) - TimeSpan.FromSeconds(30), SignInThrottlePolicy.BlockFor(SignInThrottlePolicy.FreeAttempts));

            row.BlockedUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
            _ = await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, email, Password)).StatusCode);
        await using var check = NewContext();
        Assert.Empty(await check.SignInThrottles.Where(t => t.EmailHash == SignInThrottlePolicy.HashEmail(AccountService.NormalizeEmail(email))).ToListAsync());
    }

    [Fact]
    public async Task Parallel_guesses_all_count_toward_the_block()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var email = await RegisterAsync(factory);
        using var client = factory.CreateClient();

        _ = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => SignInAsync(client, email, "wrong " + i)));

        await using var db = NewContext();
        var row = await db.SignInThrottles.SingleAsync(t => t.EmailHash == SignInThrottlePolicy.HashEmail(AccountService.NormalizeEmail(email)));
        Assert.True(row.Failures >= SignInThrottlePolicy.FreeAttempts, $"only {row.Failures} of 8 parallel misses were counted");
    }

    [Fact]
    public void Backoff_doubles_from_one_minute_and_is_capped()
    {
        Assert.Equal(TimeSpan.Zero, SignInThrottlePolicy.BlockFor(4));
        Assert.Equal(TimeSpan.FromMinutes(1), SignInThrottlePolicy.BlockFor(5));
        Assert.Equal(TimeSpan.FromMinutes(2), SignInThrottlePolicy.BlockFor(6));
        Assert.Equal(TimeSpan.FromMinutes(8), SignInThrottlePolicy.BlockFor(8));
        Assert.Equal(TimeSpan.FromMinutes(15), SignInThrottlePolicy.BlockFor(40));
    }
}
