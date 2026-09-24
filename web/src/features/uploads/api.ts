import { apiClient } from '../../lib/apiClient';

interface UploadResponse {
  url: string;
}

export const uploadsApi = {
  /** Multipart upload. The browser sets the multipart boundary, so no Content-Type is forced here. */
  uploadImage: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return apiClient.post<UploadResponse>('/uploads/image', form, { timeout: 60_000 }).then((r) => r.data.url);
  },
};
