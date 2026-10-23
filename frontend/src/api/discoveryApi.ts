import { apiClient } from './client';
import type { DiscoveryScanResult, TriggerDiscoveryRequest } from '../types/discovery';

export const discoveryApi = {
  async trigger(payload: TriggerDiscoveryRequest): Promise<DiscoveryScanResult> {
    const res = await apiClient.post<DiscoveryScanResult>('/discovery/scan', payload);
    return res.data;
  },

  async getActive(): Promise<DiscoveryScanResult | null> {
    try {
      const res = await apiClient.get<DiscoveryScanResult>('/discovery/active');
      return res.data;
    } catch (err: any) {
      if (err.response?.status === 404) return null;
      throw err;
    }
  },

  async cancel(scanId: string): Promise<void> {
    await apiClient.post(`/discovery/scan/${scanId}/cancel`);
  }
};
