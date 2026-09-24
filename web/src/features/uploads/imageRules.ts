/** Mirrors the limits in UploadEndpoints.cs so staff hear about a bad file before it uploads. The API re-checks the actual bytes. */
export const MAX_IMAGE_BYTES = 10 * 1024 * 1024;
export const ACCEPTED_IMAGE_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.webp', '.gif'] as const;

export function validateImageFile(file: { name: string; size: number }): string | null {
  if (file.size === 0) return 'That file is empty.';
  if (file.size > MAX_IMAGE_BYTES) return 'That image is larger than 10 MB. Choose a smaller one.';
  const dot = file.name.lastIndexOf('.');
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : '';
  if (!(ACCEPTED_IMAGE_EXTENSIONS as readonly string[]).includes(extension)) {
    return 'Use a JPG, PNG, WebP or GIF image.';
  }
  return null;
}
