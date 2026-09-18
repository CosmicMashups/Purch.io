namespace Purch.Application.Common;

/// <summary>
/// The seam between the uploads endpoint and where a file actually lands —
/// local disk (Local deployment mode) or Supabase Storage (Cloud). Local disk
/// only survives on a single long-lived instance (Render); Cloud mode must
/// never fall back to it, since a scale-to-zero/multi-instance host would
/// silently lose uploaded files.
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string tenantFolder,
        string requestBaseUrl,
        CancellationToken cancellationToken);
}

/// <summary>
/// <paramref name="Url"/> is always a fully-qualified, directly fetchable URL.
/// <paramref name="RelativePath"/> is a storage-relative key kept for
/// reference in API responses; it is not guaranteed to be servable from this
/// API's own host (e.g. it points at Supabase Storage in Cloud mode).
/// </summary>
public sealed record StoredFile(string Url, string RelativePath);
