using Purch.Application.Common;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class UploadEndpoints
{
    // SVG is deliberately not accepted: it is a document that can carry script, and in Local mode
    // uploads are served from the API's own origin.
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

    public static IEndpointRouteBuilder MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        _ = app.MapPost("/uploads/image", async (
            IFormFile file,
            IFileStorage fileStorage,
            ICurrentTenantProvider tenantProvider,
            IConfiguration configuration,
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "No file was uploaded or the file is empty." });
            }

            if (file.Length > MaxFileSize)
            {
                return Results.BadRequest(new { message = "File size exceeds the 10MB limit." });
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                return Results.BadRequest(new { message = $"File extension '{extension}' is not allowed. Supported extensions: .jpg, .jpeg, .png, .webp, .gif" });
            }

            // Trust the bytes, not the file name or the client's Content-Type header: a renamed
            // script or document must not be stored (and later served) as an image.
            string? detectedContentType;
            await using (var probe = file.OpenReadStream())
            {
                detectedContentType = await DetectImageContentTypeAsync(probe, cancellationToken);
            }

            if (detectedContentType is null)
            {
                return Results.BadRequest(new { message = "The file isn't a valid JPEG, PNG, GIF or WebP image." });
            }

            var tenantFolder = tenantProvider.TenantId?.ToString("N") ?? "public";
            var uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            // The Host header is chosen by the caller, so a configured public URL wins when set.
            var baseUrl = configuration["PUBLIC_BASE_URL"]?.TrimEnd('/') ?? $"{request.Scheme}://{request.Host}";

            await using var stream = file.OpenReadStream();
            var stored = await fileStorage.SaveAsync(stream, uniqueFileName, detectedContentType, tenantFolder, baseUrl, cancellationToken);

            return Results.Ok(new
            {
                url = stored.Url,
                relativePath = stored.RelativePath,
                fileName = uniqueFileName,
                size = file.Length,
                contentType = detectedContentType
            });
        })
        .RequireAuthorization(policy => policy.RequireRole(nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Warehouse)))
        .DisableAntiforgery();

        return app;
    }

    /// <summary>The content type implied by the file's leading bytes, or null if they aren't a
    /// JPEG, PNG, GIF or WebP.</summary>
    private static async Task<string?> DetectImageContentTypeAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var bytes = header.AsSpan(0, read);

        if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return "image/jpeg";
        }

        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        var isWebp = bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8);
        return bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)
            ? "image/gif"
            : isWebp ? "image/webp" : null;
    }
}
