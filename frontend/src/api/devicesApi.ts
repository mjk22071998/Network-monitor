import { apiClient } from './client';
import type { 
  Device, 
  DeviceDetail, 
  CreateDeviceRequest, 
  UpdateDeviceRequest, 
  MaintenanceToggleRequest 
} from '../types/device';

export const devicesApi = {
  async getAll(): Promise<Device[]> {
    const res = await apiClient.get<any>('/devices?limit=1000');
    if (Array.isArray(res.data)) {
      return res.data;
    }
    if (res.data && Array.isArray(res.data.data)) {
      return res.data.data;
    }
    return [];
  },

  async getById(id: number): Promise<DeviceDetail> {
    const res = await apiClient.get<DeviceDetail>(`/devices/${id}`);
    return res.data;
  },

  async create(payload: CreateDeviceRequest): Promise<Device> {
    const res = await apiClient.post<Device>('/devices', payload);
    return res.data;
  },

  async update(id: number, payload: UpdateDeviceRequest): Promise<Device> {
    const res = await apiClient.put<Device>(`/devices/${id}`, payload);
    return res.data;
  },

  async delete(id: number): Promise<void> {
    await apiClient.delete(`/devices/${id}`);
  },

  async toggleMaintenance(id: number, payload: MaintenanceToggleRequest): Promise<Device> {
    const res = await apiClient.post<Device>(`/devices/${id}/maintenance`, payload);
    return res.data;
  }
};
