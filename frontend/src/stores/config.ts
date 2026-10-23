import { defineStore } from 'pinia';
import { ref, shallowRef } from 'vue';
import { configApi } from '../api/configApi';
import { sitesApi } from '../api/sitesApi';
import type { MonitoringConfig, UpdateMonitoringConfigRequest } from '../types/config';
import type { Site, UpdateSiteRequest } from '../types/site';

export const useConfigStore = defineStore('config', () => {
  const monitoringConfig = shallowRef<MonitoringConfig | null>(null);
  const site = shallowRef<Site | null>(null);
  const isLoading = ref<boolean>(false);
  const error = ref<string | null>(null);

  async function fetchConfig() {
    isLoading.value = true;
    error.value = null;
    try {
      const [cfg, s] = await Promise.all([
        configApi.get(),
        sitesApi.get()
      ]);
      monitoringConfig.value = cfg;
      site.value = s;
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to load configuration';
    } finally {
      isLoading.value = false;
    }
  }

  async function updateMonitoringConfig(payload: UpdateMonitoringConfigRequest) {
    isLoading.value = true;
    error.value = null;
    try {
      const updated = await configApi.update(payload);
      monitoringConfig.value = updated;
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to update monitoring config';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  async function updateSite(payload: UpdateSiteRequest) {
    isLoading.value = true;
    error.value = null;
    try {
      const updated = await sitesApi.update(payload);
      site.value = updated;
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to update site metadata';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  return {
    monitoringConfig,
    site,
    isLoading,
    error,
    fetchConfig,
    updateMonitoringConfig,
    updateSite
  };
});
