import { defineStore } from 'pinia';
import { ref, shallowRef, computed } from 'vue';
import { devicesApi } from '../api/devicesApi';
import type { 
  Device, 
  DeviceDetail, 
  CreateDeviceRequest, 
  UpdateDeviceRequest, 
  MaintenanceToggleRequest 
} from '../types/device';

export const useDevicesStore = defineStore('devices', () => {
  // shallowRef prevents deep reactive proxying, bounding memory under 60MB
  const devices = shallowRef<Device[]>([]);
  const selectedDevice = shallowRef<DeviceDetail | null>(null);
  const isLoading = ref<boolean>(false);
  const isInspectorLoading = ref<boolean>(false);
  const error = ref<string | null>(null);

  // Filters & Search
  const searchQuery = ref<string>('');
  const selectedTypeFilter = ref<string>('ALL');
  const selectedStatusFilter = ref<string>('ALL');

  // Pagination for tabular device view
  const currentPage = ref<number>(1);
  const pageSize = ref<number>(25);

  // Safe helper to always get an array
  const rawList = computed(() => Array.isArray(devices.value) ? devices.value : []);

  // Computed HUD Metrics
  const totalCount = computed(() => rawList.value.length);
  
  const upCount = computed(() => 
    rawList.value.filter(d => (d.reachability_status === 'REACHABLE' || !d.reachability_status) && !d.is_in_maintenance).length
  );
  
  const downCount = computed(() => 
    rawList.value.filter(d => d.reachability_status === 'UNREACHABLE' && !d.is_in_maintenance).length
  );
  
  const suppressedCount = computed(() => 
    rawList.value.filter(d => d.reachability_status === 'SUPPRESSED' || d.is_in_maintenance).length
  );
  
  const highCpuCount = computed(() => 
    rawList.value.filter(d => (d.latest_cpu_pct ?? 0) >= 80).length
  );
  
  const highRamCount = computed(() => 
    rawList.value.filter(d => (d.latest_ram_pct ?? 0) >= 80).length
  );

  // Filtered devices
  const filteredDevices = computed(() => {
    let list = rawList.value;

    if (searchQuery.value.trim()) {
      const q = searchQuery.value.toLowerCase().trim();
      list = list.filter(d => 
        (d.display_name && d.display_name.toLowerCase().includes(q)) ||
        (d.primary_ip && d.primary_ip.toLowerCase().includes(q)) ||
        (d.mac_address && d.mac_address.toLowerCase().includes(q)) ||
        (d.vendor && d.vendor.toLowerCase().includes(q))
      );
    }

    if (selectedTypeFilter.value !== 'ALL') {
      list = list.filter(d => d.type_code === selectedTypeFilter.value);
    }

    if (selectedStatusFilter.value !== 'ALL') {
      if (selectedStatusFilter.value === 'MAINTENANCE') {
        list = list.filter(d => d.is_in_maintenance);
      } else {
        list = list.filter(d => d.reachability_status === selectedStatusFilter.value);
      }
    }

    return list;
  });

  // Paginated devices
  const paginatedDevices = computed(() => {
    const list = filteredDevices.value;
    const start = (currentPage.value - 1) * pageSize.value;
    return list.slice(start, start + pageSize.value);
  });

  const totalFilteredPages = computed(() => 
    Math.max(1, Math.ceil(filteredDevices.value.length / pageSize.value))
  );

  async function fetchDevices() {
    isLoading.value = true;
    error.value = null;
    try {
      const data = await devicesApi.getAll();
      devices.value = Array.isArray(data) ? data : [];
    } catch (err: any) {
      devices.value = [];
      error.value = err.response?.data?.error || err.message || 'Failed to fetch devices';
    } finally {
      isLoading.value = false;
    }
  }

  async function selectDevice(id: number) {
    if (!id) return;
    isInspectorLoading.value = true;
    error.value = null;
    try {
      const detail = await devicesApi.getById(id);
      selectedDevice.value = detail;
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to load device details';
    } finally {
      isInspectorLoading.value = false;
    }
  }

  function clearSelectedDevice() {
    selectedDevice.value = null;
  }

  async function createDevice(payload: CreateDeviceRequest) {
    isLoading.value = true;
    error.value = null;
    try {
      await devicesApi.create(payload);
      await fetchDevices();
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to create device';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  async function updateDevice(id: number, payload: UpdateDeviceRequest) {
    isLoading.value = true;
    error.value = null;
    try {
      await devicesApi.update(id, payload);
      await fetchDevices();
      if (selectedDevice.value?.device_id === id) {
        await selectDevice(id);
      }
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to update device';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  async function deleteDevice(id: number) {
    isLoading.value = true;
    error.value = null;
    try {
      await devicesApi.delete(id);
      if (selectedDevice.value?.device_id === id) {
        clearSelectedDevice();
      }
      await fetchDevices();
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to delete device';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  async function toggleMaintenance(id: number, payload: MaintenanceToggleRequest) {
    try {
      await devicesApi.toggleMaintenance(id, payload);
      await fetchDevices();
      if (selectedDevice.value?.device_id === id) {
        await selectDevice(id);
      }
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to toggle maintenance mode';
      throw err;
    }
  }

  return {
    devices,
    selectedDevice,
    isLoading,
    isInspectorLoading,
    error,
    searchQuery,
    selectedTypeFilter,
    selectedStatusFilter,
    currentPage,
    pageSize,
    totalCount,
    upCount,
    downCount,
    suppressedCount,
    highCpuCount,
    highRamCount,
    filteredDevices,
    paginatedDevices,
    totalFilteredPages,
    fetchDevices,
    selectDevice,
    clearSelectedDevice,
    createDevice,
    updateDevice,
    deleteDevice,
    toggleMaintenance
  };
});
