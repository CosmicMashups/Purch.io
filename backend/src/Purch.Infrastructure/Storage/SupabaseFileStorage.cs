using System.Net.Http.Headers;
using Purch.Application.Common;

namespace Purch.Infrastructure.Storage;

/// <summary>
/// Uploads to Supabase Storage's REST API directly (no Supabase SDK dependency —
/// this is one PUT call). Used for Cloud mode, where the API container may be
/// stateless/scaled-to-zero between requests, so local disk can't hold uploads.
/// </summary>
public sealed class SupabaseFileStorage(HttpClient httpClient, string storageUrl, string storageKey, string bucket) : IFileStorage
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _storageUrl = storageUrl.TrimEnd('/');
    private readonly string _storageKey = storageKey;
    private readonly string _bucket = bucket;

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string tenantFolder,
        string requestBaseUrl,
        CancellationToken cancellationToken)
    {
        var objectPath = $"{tenantFolder}/{fileName}";

        using var requestContent = new StreamContent(content);
        requestContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{_storageUrl}/object/{_bucket}/{objectPath}")
        {
            Content = requestContent,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _storageKey);
        request.Headers.Add("apikey", _storageKey);
        request.Headers.Add("x-upsert", "true");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Supabase Storage upload failed with {(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        var relativePath = $"/{_bucket}/{objectPath}";
        var url = $"{_storageUrl}/object/public/{_bucket}/{objectPath}";
        return new StoredFile(url, relativePath);
    }
}
