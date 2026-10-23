import { defineStore } from 'pinia';
import { ref, shallowRef } from 'vue';
import { ingestApi } from '../api/ingestApi';
import type { 
  ImportBatch, 
  StagingRecord, 
  UpdateStagingRecordRequest, 
  CommitBatchRequest 
} from '../types/ingest';

export const useIngestStore = defineStore('ingest', () => {
  const currentBatch = shallowRef<ImportBatch | null>(null);
  const stagingRecords = shallowRef<StagingRecord[]>([]);
  const totalRecords = ref<number>(0);
  const currentPage = ref<number>(1);
  const pageSize = ref<number>(50);
  const isLoading = ref<boolean>(false);
  const error = ref<string | null>(null);

  async function uploadCsv(file: File) {
    isLoading.value = true;
    error.value = null;
    try {
      const batch = await ingestApi.uploadCsv(file);
      currentBatch.value = batch;
      currentPage.value = 1;
      await fetchRecords(batch.batch_id, 1, pageSize.value);
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'CSV Ingestion failed';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  async function fetchRecords(batchId: number, page: number = 1, limit: number = 50) {
    isLoading.value = true;
    error.value = null;
    try {
      const offset = (page - 1) * limit;
      const res = await ingestApi.getRecords(batchId, limit, offset);
      stagingRecords.value = res.records;
      totalRecords.value = res.total;
      currentPage.value = page;
      pageSize.value = limit;
      // Refresh batch summary
      currentBatch.value = await ingestApi.getBatch(batchId);
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to fetch staging records';
    } finally {
      isLoading.value = false;
    }
  }

  async function updateRecord(recordId: number, payload: UpdateStagingRecordRequest) {
    isLoading.value = true;
    error.value = null;
    try {
      await ingestApi.updateRecord(recordId, payload);
      if (currentBatch.value) {
        await fetchRecords(currentBatch.value.batch_id, currentPage.value, pageSize.value);
      }
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to update record';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  async function commitBatch(payload: CommitBatchRequest) {
    if (!currentBatch.value) return;
    isLoading.value = true;
    error.value = null;
    try {
      const res = await ingestApi.commitBatch(currentBatch.value.batch_id, payload);
      currentBatch.value = null;
      stagingRecords.value = [];
      totalRecords.value = 0;
      return res;
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to commit batch';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  async function discardBatch() {
    if (!currentBatch.value) return;
    isLoading.value = true;
    error.value = null;
    try {
      await ingestApi.discardBatch(currentBatch.value.batch_id);
      currentBatch.value = null;
      stagingRecords.value = [];
      totalRecords.value = 0;
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to discard batch';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  function clearBatch() {
    currentBatch.value = null;
    stagingRecords.value = [];
    totalRecords.value = 0;
  }

  return {
    currentBatch,
    stagingRecords,
    totalRecords,
    currentPage,
    pageSize,
    isLoading,
    error,
    uploadCsv,
    fetchRecords,
    updateRecord,
    commitBatch,
    discardBatch,
    clearBatch
  };
});
