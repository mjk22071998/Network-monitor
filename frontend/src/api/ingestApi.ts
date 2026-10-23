import { apiClient } from './client';
import type { 
  ImportBatch, 
  StagingRecord, 
  UpdateStagingRecordRequest, 
  CommitBatchRequest 
} from '../types/ingest';

export const ingestApi = {
  async uploadCsv(file: File): Promise<ImportBatch> {
    const formData = new FormData();
    formData.append('file', file);
    const res = await apiClient.post<ImportBatch>('/ingest/upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    });
    return res.data;
  },

  async getBatch(id: number): Promise<ImportBatch> {
    const res = await apiClient.get<ImportBatch>(`/ingest/batches/${id}`);
    return res.data;
  },

  async getRecords(batchId: number, limit: number = 50, offset: number = 0): Promise<{ records: StagingRecord[]; total: number }> {
    const res = await apiClient.get<{ records: StagingRecord[]; total: number }>(`/ingest/batches/${batchId}/records`, {
      params: { limit, offset }
    });
    return res.data;
  },

  async updateRecord(recordId: number, payload: UpdateStagingRecordRequest): Promise<StagingRecord> {
    const res = await apiClient.put<StagingRecord>(`/ingest/records/${recordId}`, payload);
    return res.data;
  },

  async commitBatch(batchId: number, payload: CommitBatchRequest): Promise<{ committed_count: number; message: string }> {
    const res = await apiClient.post<{ committed_count: number; message: string }>(`/ingest/batches/${batchId}/commit`, payload);
    return res.data;
  },

  async discardBatch(batchId: number): Promise<void> {
    await apiClient.delete(`/ingest/batches/${batchId}`);
  }
};
