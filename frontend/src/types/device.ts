export interface Device {
  device_id: number;
  display_name: string;
  primary_ip: string;
  mac_address: string;
  type_id: number;
  type_code?: string;
  type_name?: string;
  group_id: number;
  group_name?: string;
  parent_device_id: number | null;
  parent_display_name?: string | null;
  zone: string | null;
  vendor: string | null;
  model: string | null;
  is_monitored: boolean;
  is_in_maintenance: boolean;
  maintenance_reason?: string | null;
  created_at: string;
  updated_at: string;
  
  // Dynamic telemetry fields
  reachability_status?: 'REACHABLE' | 'UNREACHABLE' | 'SUPPRESSED' | 'UNKNOWN';
  latest_latency_us?: number | null;
  latest_loss_pct?: number | null;
  latest_ntp_offset_us?: number | null;
  health_status?: 'AVAILABLE' | 'UNAVAILABLE' | 'STALE' | 'UNSUPPORTED' | 'ERROR';
  latest_cpu_pct?: number | null;
  latest_ram_pct?: number | null;
  latest_temp_c?: number | null;

  latest_reachability?: {
    status_id: number;
    status_code: string;
    latency_us: number | null;
    loss_percentage: number;
    ntp_offset_us: number | null;
    is_suppressed: boolean;
    recorded_at: string;
  } | null;
  latest_health?: {
    status_id: number;
    status_code: string;
    cpu_utilization_pct: number | null;
    ram_utilization_pct: number | null;
    recorded_at: string;
  } | null;
}

export interface DiskVolume {
  partition_name: string;
  total_bytes: number;
  free_bytes: number;
  used_bytes: number;
  used_pct: number;
}

export interface DeviceDetail extends Device {
  reachability_history?: ReachabilityHistoryPoint[];
  disk_volumes?: DiskVolume[];
}

export interface ReachabilityHistoryPoint {
  recorded_at: string;
  status_code: string;
  latency_us: number | null;
  loss_percentage: number;
  ntp_offset_us: number | null;
}

export interface CreateDeviceRequest {
  display_name: string;
  primary_ip: string;
  mac_address: string;
  type_id: number;
  group_id: number;
  parent_device_id?: number | null;
  zone?: string | null;
  vendor?: string | null;
  model?: string | null;
  is_monitored?: boolean;
}

export interface UpdateDeviceRequest {
  display_name?: string;
  primary_ip?: string;
  group_id?: number;
  parent_device_id?: number | null;
  zone?: string | null;
  vendor?: string | null;
  model?: string | null;
  is_monitored?: boolean;
}

export interface MaintenanceToggleRequest {
  is_in_maintenance: boolean;
  reason?: string;
}
