using Purch.Application.Common;

namespace Purch.Api.Endpoints;

public static class UploadEndpoints
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg"
    };

    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

    public static IEndpointRouteBuilder MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        _ = app.MapPost("/uploads/image", async (
            IFormFile file,
            IFileStorage fileStorage,
            ICurrentTenantProvider tenantProvider,
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
                return Results.BadRequest(new { message = $"File extension '{extension}' is not allowed. Supported extensions: .jpg, .jpeg, .png, .webp, .svg, .gif" });
            }

            var tenantFolder = tenantProvider.TenantId?.ToString("N") ?? "public";
            var uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var baseUrl = $"{request.Scheme}://{request.Host}";

            await using var stream = file.OpenReadStream();
            var stored = await fileStorage.SaveAsync(stream, uniqueFileName, file.ContentType, tenantFolder, baseUrl, cancellationToken);

            return Results.Ok(new
            {
                url = stored.Url,
                relativePath = stored.RelativePath,
                fileName = uniqueFileName,
                size = file.Length,
                contentType = file.ContentType
            });
        })
        .RequireAuthorization()
        .DisableAntiforgery();

        return app;
    }
}
