export interface AuditLogEntry {
  log_id: number;
  user_id: number | null;
  username?: string | null;
  site_id: number;
  action_code: string;
  target_entity: string;
  target_id: string | null;
  details: string | null;
  ip_address: string;
  logged_at: string;
}

export interface PaginationMetadata {
  page: number;
  limit: number;
  total_records: number;
  total_pages: number;
}

export interface AuditLogsPagedResponse {
  data: AuditLogEntry[];
  pagination: PaginationMetadata;
}

export interface AuditLogQueryParams {
  page?: number;
  limit?: number;
  user_id?: number;
  action_code?: string;
  start?: string;
  end?: string;
}
