import { apiClient } from '../../lib/apiClient';

export const syncApi = {
  /** Marks a flagged record as reviewed. It does not change any data; the record already lost the sync race. */
  acknowledge: (syncedRecordId: string) => apiClient.post(`/sync/flagged/${syncedRecordId}/acknowledge`).then((r) => r.data),
};
