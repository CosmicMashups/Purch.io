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

    private static async Task<(HttpClient Client, BootstrapTenantResult Tenant)> AdminClientAsync(PurchApiFactory factory, string tenantName)
    {
        var client = factory.CreateClient();
        var bootstrapResponse = await client.PostAsJsonAsync(
            "/onboarding/bootstrap",
            new BootstrapTenantRequest(tenantName, BusinessType.ConvenienceStore, "Main Branch", "Admin User", "1234"));
        var tenant = (await bootstrapResponse.Content.ReadFromJsonAsync<BootstrapTenantResult>(JsonOptions))!;
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(tenant.DevicePairingCode, "1234"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return (client, tenant);
    }

    private static MultipartFormDataContent ImageForm(byte[] bytes, string fileName, string contentType = "image/jpeg")
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(part, "file", fileName);
        return form;
    }

    [Fact]
    public async Task An_svg_is_rejected_because_it_can_carry_script()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var (client, _) = await AdminClientAsync(factory, "Svg Shop");
        using var _client = client;

        using var form = ImageForm("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"u8.ToArray(), "logo.svg", "image/svg+xml");
        var response = await client.PostAsync("/uploads/image", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_file_that_is_not_an_image_is_rejected_even_when_named_and_labelled_as_one()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var (client, _) = await AdminClientAsync(factory, "Disguise Shop");
        using var _client = client;

        using var form = ImageForm("<html><script>alert(1)</script></html>"u8.ToArray(), "photo.jpg", "image/jpeg");
        var response = await client.PostAsync("/uploads/image", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_stored_content_type_comes_from_the_bytes_not_the_clients_header()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var (client, _) = await AdminClientAsync(factory, "Sniff Shop");
        using var _client = client;

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        using var form = ImageForm(png, "pic.png", "text/html");
        var response = await client.PostAsync("/uploads/image", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("image/png", result.GetProperty("contentType").GetString());
    }

    [Fact]
    public async Task The_returned_url_uses_the_configured_public_base_url_not_the_callers_host_header()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString) { ExtraSettings = { ["PUBLIC_BASE_URL"] = "https://media.example.test/" } };
        var (client, _) = await AdminClientAsync(factory, "Base Url Shop");
        using var _client = client;

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        using var form = ImageForm(png, "pic.png", "image/png");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/uploads/image") { Content = form };
        request.Headers.Host = "attacker.example";
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var url = (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("url").GetString()!;
        Assert.StartsWith("https://media.example.test/", url);
        Assert.DoesNotContain("attacker.example", url);
    }

    [Fact]
    public async Task A_cashier_cannot_upload_images()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var (admin, tenant) = await AdminClientAsync(factory, "Role Shop");
        using var _admin = admin;
        _ = await admin.PostAsJsonAsync("/staff", new CreateStaffRequest("Cash Ier", Role.Cashier, ScopeType.Tenant, null, null, "5678"));

        using var cashier = factory.CreateClient();
        var loginResponse = await cashier.PostAsJsonAsync("/auth/login", new LoginRequest(tenant.DevicePairingCode, "5678"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        cashier.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        using var form = ImageForm([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46], "banner.jpg");
        var response = await cashier.PostAsync("/uploads/image", form);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record LoginResponseBody(string AccessToken);
}
