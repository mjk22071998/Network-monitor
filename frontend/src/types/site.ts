export interface Site {
  site_id: number;
  site_name: string;
  facility_code: string | null;
  site_code?: string | null;
  description: string | null;
  created_at: string;
  updated_at: string;
}

export interface UpdateSiteRequest {
  site_name: string;
  facility_code?: string | null;
  site_code?: string | null;
  description?: string | null;
}
