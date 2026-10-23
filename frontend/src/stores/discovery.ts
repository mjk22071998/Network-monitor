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
      if (active) {
        currentScan.value = active;
        if (active.status === 'PENDING' || active.status === 'IN_PROGRESS' || active.status === 'RUNNING') {
          startPolling(active.scan_id);
        } else {
          stopPolling();
        }
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
      startPolling(scan.scan_id);
    } catch (err: any) {
      isScanning.value = false;
      error.value = err.response?.data?.error || err.response?.data?.message || err.message || 'Failed to start subnet sweep';
      throw err;
    }
  }

  async function cancelScan() {
    if (!currentScan.value?.scan_id) return;
    try {
      await discoveryApi.cancel(currentScan.value.scan_id);
      stopPolling();
      await checkActiveScan();
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to cancel sweep';
    }
  }

  function startPolling(scanId?: string) {
    stopPolling();
    isScanning.value = true;
    const targetScanId = scanId || currentScan.value?.scan_id;

    pollInterval = setInterval(async () => {
      try {
        if (!targetScanId) {
          const active = await discoveryApi.getActive();
          if (active) {
            currentScan.value = active;
            if (active.status === 'COMPLETED' || active.status === 'CANCELLED' || active.status === 'FAILED') {
              stopPolling();
            }
          } else {
            stopPolling();
          }
          return;
        }

        const scan = await discoveryApi.getScanStatus(targetScanId);
        currentScan.value = scan;
        if (!scan || scan.status === 'COMPLETED' || scan.status === 'CANCELLED' || scan.status === 'FAILED') {
          stopPolling();
        }
      } catch {
        stopPolling();
      }
    }, 1000);
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
