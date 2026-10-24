import { apiClient } from './client';
import type { 
  Device, 
  DeviceDetail, 
  CreateDeviceRequest, 
  UpdateDeviceRequest, 
  MaintenanceToggleRequest 
} from '../types/device';

export function normalizeDevice(d: any): Device {
  if (!d) return d;
  const reach = d.latest_reachability;
  const health = d.latest_health;
  
  let status: 'REACHABLE' | 'UNREACHABLE' | 'UNKNOWN' = 'UNKNOWN';
  if (reach?.status_code) {
    const sc = reach.status_code.toUpperCase();
    if (sc === 'REACHABLE' || sc === 'ONLINE') {
      status = 'REACHABLE';
    } else if (sc === 'UNREACHABLE' || sc === 'OFFLINE' || sc === 'SUPPRESSED') {
      status = 'UNREACHABLE';
    } else {
      status = 'UNKNOWN';
    }
  } else if (!d.is_monitored) {
    status = 'UNKNOWN';
  }

  return {
    ...d,
    reachability_status: status,
    latest_latency_us: reach?.latency_us ?? d.latest_latency_us ?? null,
    latest_loss_pct: reach?.loss_percentage ?? d.latest_loss_pct ?? null,
    latest_ntp_offset_us: reach?.ntp_offset_us ?? d.latest_ntp_offset_us ?? null,
    health_status: health?.status_code ?? d.health_status ?? null,
    latest_cpu_pct: health?.cpu_utilization_pct ?? d.latest_cpu_pct ?? null,
    latest_ram_pct: health?.ram_utilization_pct ?? d.latest_ram_pct ?? null,
  };
}

export function normalizeDeviceDetail(d: any): DeviceDetail {
  const base = normalizeDevice(d);
  return {
    ...base,
    reachability_history: d.reachability_history || [],
    disk_volumes: d.disk_volumes || []
  };
}

export const devicesApi = {
  async getAll(): Promise<Device[]> {
    const res = await apiClient.get<any>('/devices?limit=1000');
    const rawList = Array.isArray(res.data)
      ? res.data
      : (res.data && Array.isArray(res.data.data))
        ? res.data.data
        : [];
    return rawList.map(normalizeDevice);
  },

  async getById(id: number | string): Promise<DeviceDetail> {
    const numId = Number(id);
    if (!numId || numId <= 0 || isNaN(numId)) {
      console.warn(`[devicesApi] Suppressed invalid getById call for non-positive ID: ${id}`);
      return null as any;
    }
    const res = await apiClient.get<any>(`/devices/${numId}`);
    return normalizeDeviceDetail(res.data);
  },

  async create(payload: CreateDeviceRequest): Promise<Device> {
    const res = await apiClient.post<any>('/devices', payload);
    return normalizeDevice(res.data);
  },

  async update(id: number | string, payload: UpdateDeviceRequest): Promise<Device> {
    const numId = Number(id);
    if (!numId || numId <= 0 || isNaN(numId)) {
      throw new Error(`[devicesApi] Cannot update device with invalid ID: ${id}`);
    }
    const res = await apiClient.put<any>(`/devices/${numId}`, payload);
    return normalizeDevice(res.data);
  },

  async delete(id: number | string): Promise<void> {
    const numId = Number(id);
    if (!numId || numId <= 0 || isNaN(numId)) {
      throw new Error(`[devicesApi] Cannot delete device with invalid ID: ${id}`);
    }
    await apiClient.delete(`/devices/${numId}`);
  },

  async toggleMaintenance(id: number | string, payload: MaintenanceToggleRequest): Promise<Device> {
    const numId = Number(id);
    if (!numId || numId <= 0 || isNaN(numId)) {
      throw new Error(`[devicesApi] Cannot toggle maintenance with invalid ID: ${id}`);
    }
    const res = await apiClient.post<any>(`/devices/${numId}/maintenance`, payload);
    return normalizeDevice(res.data);
  }
};
