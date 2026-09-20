using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class UploadEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Upload_without_auth_is_rejected_with_401()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]), "file", "test.jpg" }
        };

        var response = await client.PostAsync("/uploads/image", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_upload_with_valid_image_returns_url_and_persists_file()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var bootstrapResponse = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest("Upload Test Shop", BusinessType.ConvenienceStore, "Main Branch", "Admin User", "1234"));
        var bootstrapResult = await bootstrapResponse.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions);

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(bootstrapResult!.DevicePairingCode, "1234"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);

        using var content = new MultipartFormDataContent();
        var sampleBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 }; // Fake JPEG header
        var fileContent = new ByteArrayContent(sampleBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "banner.jpg");

        var response = await client.PostAsync("/uploads/image", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("url", out var urlProp));
        Assert.Contains("/uploads/", urlProp.GetString()!);
        Assert.True(result.TryGetProperty("relativePath", out var relativeProp));
        Assert.StartsWith("/uploads/", relativeProp.GetString()!);
    }

    [Fact]
    public async Task Upload_with_disallowed_extension_returns_400()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var bootstrapResponse = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest("Upload Test Shop 2", BusinessType.ConvenienceStore, "Main Branch", "Admin User", "1234"));
        var bootstrapResult = await bootstrapResponse.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions);

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(bootstrapResult!.DevicePairingCode, "1234"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]), "file", "malicious.exe" }
        };

        var response = await client.PostAsync("/uploads/image", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record LoginResponseBody(string AccessToken);
}
