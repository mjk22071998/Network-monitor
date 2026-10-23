import { apiClient } from './client';
import type { DiscoveryScanResult, TriggerDiscoveryRequest } from '../types/discovery';

function normalizeScanResponse(data: any, fallbackCidr?: string): DiscoveryScanResult {
  const status = (data.status === 'RUNNING' ? 'IN_PROGRESS' : data.status) || 'PENDING';
  const rawAssets = data.discovered_assets || data.hosts || [];
  const total = data.total_ips ?? data.total_targets ?? 254;
  const progress = data.progress_pct ?? (status === 'COMPLETED' ? 100 : 0);
  const scanned = data.scanned_ips ?? data.scanned_hosts ?? Math.round((progress / 100) * total);

  const hosts = rawAssets.map((asset: any) => ({
    ip_address: asset.primary_ip || asset.ip_address || '',
    mac_address: asset.mac_address || null,
    vendor: asset.vendor || null,
    hostname: asset.hostname || null,
    response_time_ms: typeof asset.response_time_ms === 'number' ? asset.response_time_ms : null,
    inferred_type: asset.inferred_type || 'PC',
    is_already_inventoried: Boolean(asset.is_already_registered ?? asset.is_already_inventoried),
    open_ports: asset.open_ports || []
  }));

  return {
    scan_id: data.scan_id || '',
    cidr: data.target_cidr || data.cidr || fallbackCidr || '10.0.1.0/24',
    status,
    started_at: data.started_at,
    completed_at: data.completed_at || null,
    progress_pct: progress,
    scanned_hosts: scanned,
    discovered_hosts_count: hosts.length,
    total_targets: total,
    hosts
  };
}

export const discoveryApi = {
  async trigger(payload: TriggerDiscoveryRequest): Promise<DiscoveryScanResult> {
    const cidr = payload.subnet_cidr || payload.cidr;
    const body = {
      subnet_cidr: cidr,
      cidr: cidr,
      snmp_community: payload.snmp_community || 'public',
      snmp_version: payload.snmp_version || 'v2c'
    };
    const res = await apiClient.post('/discovery/scan', body);
    return normalizeScanResponse(res.data, cidr);
  },

  async getActive(): Promise<DiscoveryScanResult | null> {
    try {
      const res = await apiClient.get('/discovery/active');
      if (!res.data || res.status === 204) return null;
      return normalizeScanResponse(res.data);
    } catch (err: any) {
      if (err.response?.status === 404 || err.response?.status === 204) return null;
      throw err;
    }
  },

  async getScanStatus(scanId: string): Promise<DiscoveryScanResult> {
    const res = await apiClient.get(`/discovery/scan/${scanId}`);
    return normalizeScanResponse(res.data);
  },

  async cancel(scanId: string): Promise<void> {
    await apiClient.post(`/discovery/scan/${scanId}/cancel`);
  }
};
