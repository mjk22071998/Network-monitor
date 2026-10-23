import { defineStore } from 'pinia';
import { ref, shallowRef } from 'vue';
import { auditApi } from '../api/auditApi';
import type { AuditLogEntry, PaginationMetadata, AuditLogQueryParams } from '../types/audit';

export const useAuditStore = defineStore('audit', () => {
  const logs = shallowRef<AuditLogEntry[]>([]);
  const pagination = ref<PaginationMetadata>({
    page: 1,
    limit: 50,
    total_records: 0,
    total_pages: 0
  });

  const isLoading = ref<boolean>(false);
  const error = ref<string | null>(null);

  // Filters
  const selectedActionCode = ref<string>('');
  const filterUserId = ref<number | undefined>(undefined);
  const filterStartDate = ref<string>('');
  const filterEndDate = ref<string>('');

  async function fetchLogs(page: number = 1, limit: number = 50) {
    isLoading.value = true;
    error.value = null;
    try {
      const params: AuditLogQueryParams = {
        page,
        limit,
        action_code: selectedActionCode.value || undefined,
        user_id: filterUserId.value || undefined,
        start: filterStartDate.value || undefined,
        end: filterEndDate.value || undefined
      };
      const res = await auditApi.getLogs(params);
      logs.value = res.data;
      pagination.value = res.pagination;
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to load audit logs';
    } finally {
      isLoading.value = false;
    }
  }

  function setPage(p: number) {
    if (p < 1 || (pagination.value.total_pages > 0 && p > pagination.value.total_pages)) return;
    fetchLogs(p, pagination.value.limit);
  }

  function setLimit(l: number) {
    fetchLogs(1, l);
  }

  function resetFilters() {
    selectedActionCode.value = '';
    filterUserId.value = undefined;
    filterStartDate.value = '';
    filterEndDate.value = '';
    fetchLogs(1, pagination.value.limit);
  }

  return {
    logs,
    pagination,
    isLoading,
    error,
    selectedActionCode,
    filterUserId,
    filterStartDate,
    filterEndDate,
    fetchLogs,
    setPage,
    setLimit,
    resetFilters
  };
});
