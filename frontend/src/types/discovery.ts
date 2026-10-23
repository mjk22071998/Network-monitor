export interface DiscoveredHost {
  ip_address: string;
  mac_address: string | null;
  vendor: string | null;
  hostname: string | null;
  response_time_ms: number | null;
  inferred_type?: string;
  open_ports?: number[];
  is_already_inventoried: boolean;
}

export interface DiscoveryScanResult {
  scan_id: string;
  cidr: string;
  status: 'PENDING' | 'RUNNING' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED' | 'FAILED';
  started_at?: string;
  completed_at?: string | null;
  progress_pct: number;
  scanned_hosts: number;
  discovered_hosts_count: number;
  total_targets: number;
  hosts: DiscoveredHost[];
}

export interface TriggerDiscoveryRequest {
  cidr: string;
  timeout_ms?: number;
  subnet_cidr?: string;
  snmp_community?: string;
  snmp_version?: string;
}
