using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Security;

/// <summary>Forged, unsigned and wrongly-signed bearer tokens must never reach an endpoint.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class TokenForgeryTests(PostgresContainerFixture postgres)
{
    private static string B64(string json) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));

    private static async Task<HttpStatusCode> GetItemsWithAsync(PurchApiFactory factory, string token)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await client.GetAsync("/items")).StatusCode;
    }

    [Fact]
    public async Task Unsigned_alg_none_admin_token_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var token = $"{B64("{\"alg\":\"none\",\"typ\":\"JWT\"}")}.{B64($"{{\"sub\":\"{Guid.NewGuid()}\",\"role\":\"Admin\",\"tenant_id\":\"{Guid.NewGuid()}\",\"exp\":{exp}}}")}.";

        Assert.Equal(HttpStatusCode.Unauthorized, await GetItemsWithAsync(factory, token));
    }

    [Fact]
    public async Task Token_signed_with_a_different_key_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("an-attacker-chosen-key-of-at-least-32-bytes!!"));
        var jwt = new JwtSecurityToken(
            claims: [new System.Security.Claims.Claim("role", "Admin"), new System.Security.Claims.Claim("tenant_id", Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        Assert.Equal(HttpStatusCode.Unauthorized, await GetItemsWithAsync(factory, new JwtSecurityTokenHandler().WriteToken(jwt)));
    }

    [Fact]
    public async Task Garbage_bearer_token_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        Assert.Equal(HttpStatusCode.Unauthorized, await GetItemsWithAsync(factory, "not-a-jwt"));
    }
}
