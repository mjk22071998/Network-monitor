export interface ImportBatch {
  batch_id: number;
  uploaded_by_user_id: number;
  uploader_username?: string;
  source_file_type: string;
  total_records: number;
  valid_records: number;
  incomplete_records: number;
  conflict_records: number;
  status: 'PENDING' | 'VALIDATED' | 'COMMITTED' | 'DISCARDED';
  created_at: string;
}

export interface StagingRecord {
  record_id: number;
  batch_id: number;
  row_number: number;
  display_name: string | null;
  primary_ip: string | null;
  mac_address: string | null;
  device_type_str: string | null;
  group_path_str: string | null;
  parent_device_str: string | null;
  resolved_type_id: number | null;
  resolved_group_id: number | null;
  resolved_parent_id: number | null;
  auto_vendor: string | null;
  validation_status: 'VALID' | 'INCOMPLETE' | 'CONFLICT';
  validation_error: string | null;
}

export interface UpdateStagingRecordRequest {
  display_name?: string;
  primary_ip?: string;
  mac_address?: string;
  device_type_str?: string;
  group_path_str?: string;
  parent_device_str?: string;
}

export interface CommitBatchRequest {
  commit_valid_only: boolean;
}
