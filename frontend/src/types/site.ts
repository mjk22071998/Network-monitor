export interface Site {
  site_id: number;
  site_name: string;
  site_code: string;
  description: string | null;
  created_at: string;
  updated_at: string;
}

export interface UpdateSiteRequest {
  site_name: string;
  site_code: string;
  description?: string | null;
}
