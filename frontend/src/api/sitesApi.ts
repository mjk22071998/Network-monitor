import { apiClient } from './client';
import type { Site, UpdateSiteRequest } from '../types/site';

export const sitesApi = {
  async get(): Promise<Site> {
    const res = await apiClient.get<Site>('/site');
    return res.data;
  },

  async update(payload: UpdateSiteRequest): Promise<Site> {
    const res = await apiClient.put<Site>('/site', payload);
    return res.data;
  }
};
