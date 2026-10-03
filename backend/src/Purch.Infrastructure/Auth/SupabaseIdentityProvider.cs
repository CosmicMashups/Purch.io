using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;

namespace Purch.Infrastructure.Auth;

/// <summary>Checks passwords with Supabase Auth (GoTrue). Accounts are only ever created here, by the admin endpoint with
/// the service key and with the email already confirmed, so no verification email is sent. Public sign-up must be
/// switched off in the Supabase project (Authentication, Providers, Email: "Allow new users to sign up" off), otherwise
/// anyone with the public key could create logins directly.</summary>
public sealed class SupabaseIdentityProvider(HttpClient httpClient, IDeploymentContext deploymentContext) : IIdentityProvider
{
    public const string HttpClientName = nameof(SupabaseIdentityProvider);

    private string BaseUrl => (deploymentContext.IdentityUrl
        ?? throw new InvalidOperationException("SUPABASE_AUTH_URL is not configured.")).TrimEnd('/');

    private string ServiceKey => deploymentContext.IdentityServiceKey
        ?? throw new InvalidOperationException("SUPABASE_AUTH_SERVICE_KEY is not configured.");

    // The password grant only needs the public (anon) key, but the service key also works, so a project that has set
    // only the service key still signs people in.
    private string PublicKey => deploymentContext.IdentityAnonKey ?? ServiceKey;

    public async Task<Guid> CreateUserAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/auth/v1/admin/users")
        {
            Content = JsonContent.Create(new { email, password, email_confirm = true }),
        };
        Authorize(request, ServiceKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity || response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ConflictException("That email already has a login.");
        }

        _ = response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<SupabaseUser>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Supabase Auth returned no user.");
        return user.Id;
    }

    public async Task<Guid?> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/auth/v1/token?grant_type=password")
        {
            Content = JsonContent.Create(new { email, password }),
        };
        Authorize(request, PublicKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.UnprocessableEntity)
        {
            return null;
        }

        _ = response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SupabaseSession>(cancellationToken: cancellationToken);
        return session?.User?.Id;
    }

    public async Task SetPasswordAsync(Guid providerUserId, string newPassword, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}/auth/v1/admin/users/{providerUserId}")
        {
            Content = JsonContent.Create(new { password = newPassword }),
        };
        Authorize(request, ServiceKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
    }

    private static void Authorize(HttpRequestMessage request, string key)
    {
        request.Headers.Add("apikey", key);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
    }

    private sealed record SupabaseUser([property: JsonPropertyName("id")] Guid Id);

    private sealed record SupabaseSession([property: JsonPropertyName("user")] SupabaseUser? User);
}
