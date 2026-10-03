using System.Net;
using System.Text;
using System.Text.Json;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Enums;
using Purch.Infrastructure.Auth;

namespace Purch.UnitTests.Auth;

public sealed class SupabaseIdentityProviderTests
{
    private sealed class Context : IDeploymentContext
    {
        public DeploymentMode Mode => DeploymentMode.Cloud;

        public string DatabaseConnectionString => string.Empty;

        public string StorageLocation => string.Empty;

        public string? StorageKey => null;

        public string? StorageBucket => null;

        public string? IdentityUrl { get; init; } = "https://project.supabase.test/";

        public string? IdentityAnonKey { get; init; } = "anon-key";

        public string? IdentityServiceKey { get; init; } = "service-key";
    }

    private sealed class Recorder(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static SupabaseIdentityProvider Provider(Recorder handler, Context? context = null)
    {
        return new SupabaseIdentityProvider(new HttpClient(handler), context ?? new Context());
    }

    [Fact]
    public async Task Creating_a_login_uses_the_admin_endpoint_with_the_service_key_and_marks_the_email_confirmed()
    {
        var id = Guid.NewGuid();
        var handler = new Recorder(HttpStatusCode.OK, $"{{\"id\":\"{id}\"}}");

        var created = await Provider(handler).CreateUserAsync("ana@example.com", "secret-pass");

        Assert.Equal(id, created);
        Assert.Equal("https://project.supabase.test/auth/v1/admin/users", handler.Request!.RequestUri!.ToString());
        Assert.Equal("service-key", handler.Request.Headers.GetValues("apikey").Single());
        Assert.Equal("service-key", handler.Request.Headers.Authorization!.Parameter);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("ana@example.com", body.RootElement.GetProperty("email").GetString());
        Assert.True(body.RootElement.GetProperty("email_confirm").GetBoolean());
    }

    [Fact]
    public async Task An_email_that_already_has_a_login_is_a_conflict()
    {
        var handler = new Recorder(HttpStatusCode.UnprocessableEntity, "{\"error_code\":\"email_exists\"}");

        _ = await Assert.ThrowsAsync<ConflictException>(() => Provider(handler).CreateUserAsync("ana@example.com", "secret-pass"));
    }

    [Fact]
    public async Task A_correct_password_returns_the_supabase_user_id_using_the_public_key()
    {
        var id = Guid.NewGuid();
        var handler = new Recorder(HttpStatusCode.OK, $"{{\"access_token\":\"x\",\"user\":{{\"id\":\"{id}\"}}}}");

        var verified = await Provider(handler).VerifyPasswordAsync("ana@example.com", "secret-pass");

        Assert.Equal(id, verified);
        Assert.Equal("https://project.supabase.test/auth/v1/token?grant_type=password", handler.Request!.RequestUri!.ToString());
        Assert.Equal("anon-key", handler.Request.Headers.GetValues("apikey").Single());
    }

    [Fact]
    public async Task A_wrong_password_returns_null_without_saying_why()
    {
        var handler = new Recorder(HttpStatusCode.BadRequest, "{\"error_code\":\"invalid_credentials\"}");

        Assert.Null(await Provider(handler).VerifyPasswordAsync("ana@example.com", "wrong"));
    }

    [Fact]
    public async Task Without_an_anon_key_the_service_key_is_used_to_check_a_password()
    {
        var handler = new Recorder(HttpStatusCode.BadRequest, "{}");

        _ = await Provider(handler, new Context { IdentityAnonKey = null }).VerifyPasswordAsync("ana@example.com", "wrong");

        Assert.Equal("service-key", handler.Request!.Headers.GetValues("apikey").Single());
    }

    [Fact]
    public async Task A_deployment_without_the_auth_settings_says_so_instead_of_failing_obscurely()
    {
        var handler = new Recorder(HttpStatusCode.OK, "{}");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Provider(handler, new Context { IdentityUrl = null }).VerifyPasswordAsync("ana@example.com", "x"));

        Assert.Contains("SUPABASE_AUTH_URL", exception.Message);
    }
}
