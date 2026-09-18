using Purch.Application.Common;

namespace Purch.Infrastructure.Storage;

/// <summary>
/// Writes to the API's own wwwroot, served back out via app.UseStaticFiles().
/// Only safe for Local mode's single long-lived instance — see IFileStorage.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _webRootPath;

    public LocalFileStorage(string webRootPath)
    {
        _webRootPath = webRootPath;
    }

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string tenantFolder,
        string requestBaseUrl,
        CancellationToken cancellationToken)
    {
        var uploadsDir = Path.Combine(_webRootPath, "uploads", tenantFolder);
        Directory.CreateDirectory(uploadsDir);

        var filePath = Path.Combine(uploadsDir, fileName);
        await using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var relativePath = $"/uploads/{tenantFolder}/{fileName}";
        return new StoredFile($"{requestBaseUrl}{relativePath}", relativePath);
    }
}
