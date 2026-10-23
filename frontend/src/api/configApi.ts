import { apiClient } from './client';
import type { MonitoringConfig, UpdateMonitoringConfigRequest } from '../types/config';

export const configApi = {
  async get(): Promise<MonitoringConfig> {
    const res = await apiClient.get<MonitoringConfig>('/monitoring/config');
    return res.data;
  },

  async update(payload: UpdateMonitoringConfigRequest): Promise<MonitoringConfig> {
    const res = await apiClient.put<MonitoringConfig>('/monitoring/config', payload);
    return res.data;
  }
};
