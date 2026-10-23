import { defineStore } from 'pinia';
import { ref, shallowRef } from 'vue';
import { discoveryApi } from '../api/discoveryApi';
import type { DiscoveryScanResult } from '../types/discovery';

export const useDiscoveryStore = defineStore('discovery', () => {
  const currentScan = shallowRef<DiscoveryScanResult | null>(null);
  const isScanning = ref<boolean>(false);
  const error = ref<string | null>(null);
  let pollInterval: any = null;

  async function checkActiveScan() {
    try {
      const active = await discoveryApi.getActive();
      currentScan.value = active;
      if (active && (active.status === 'PENDING' || active.status === 'IN_PROGRESS')) {
        startPolling();
      } else {
        stopPolling();
      }
    } catch {
      stopPolling();
    }
  }

  async function startScan(cidr: string, timeoutMs: number = 1000) {
    error.value = null;
    isScanning.value = true;
    try {
      const scan = await discoveryApi.trigger({ cidr, timeout_ms: timeoutMs });
      currentScan.value = scan;
      startPolling();
    } catch (err: any) {
      isScanning.value = false;
      error.value = err.response?.data?.error || err.message || 'Failed to start subnet sweep';
      throw err;
    }
  }

  async function cancelScan() {
    if (!currentScan.value) return;
    try {
      await discoveryApi.cancel(currentScan.value.scan_id);
      stopPolling();
      await checkActiveScan();
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to cancel sweep';
    }
  }

  function startPolling() {
    stopPolling();
    isScanning.value = true;
    pollInterval = setInterval(async () => {
      try {
        const scan = await discoveryApi.getActive();
        currentScan.value = scan;
        if (!scan || scan.status === 'COMPLETED' || scan.status === 'CANCELLED' || scan.status === 'FAILED') {
          stopPolling();
        }
      } catch {
        stopPolling();
      }
    }, 1500);
  }

  function stopPolling() {
    if (pollInterval) {
      clearInterval(pollInterval);
      pollInterval = null;
    }
    isScanning.value = false;
  }

  return {
    currentScan,
    isScanning,
    error,
    checkActiveScan,
    startScan,
    cancelScan,
    stopPolling
  };
});
