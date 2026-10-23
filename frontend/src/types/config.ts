export interface MonitoringConfig {
  config_id: number;
  site_id: number;
  reachability_interval_sec: number;
  reachability_timeout_ms: number;
  health_interval_sec: number;
  health_timeout_ms: number;
  cascade_suppression_enabled: boolean;
}

export interface UpdateMonitoringConfigRequest {
  reachability_interval_sec: number;
  reachability_timeout_ms: number;
  health_interval_sec: number;
  health_timeout_ms: number;
  cascade_suppression_enabled: boolean;
}
