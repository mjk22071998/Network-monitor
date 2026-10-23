export interface DiscoveredHost {
  ip_address: string;
  mac_address: string | null;
  vendor: string | null;
  hostname: string | null;
  response_time_ms: number;
  open_ports: number[];
  is_already_inventoried: boolean;
}

export interface DiscoveryScanResult {
  scan_id: string;
  cidr: string;
  status: 'PENDING' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED' | 'FAILED';
  started_at: string;
  completed_at?: string | null;
  scanned_hosts: number;
  discovered_hosts_count: number;
  total_targets: number;
  hosts: DiscoveredHost[];
}

export interface TriggerDiscoveryRequest {
  cidr: string;
  timeout_ms?: number;
}
