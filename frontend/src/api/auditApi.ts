import { apiClient } from './client';
import type { AuditLogsPagedResponse, AuditLogQueryParams } from '../types/audit';

export const auditApi = {
  async getLogs(params?: AuditLogQueryParams): Promise<AuditLogsPagedResponse> {
    const res = await apiClient.get<AuditLogsPagedResponse>('/audit/logs', { params });
    return res.data;
  }
};
