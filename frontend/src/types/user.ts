export interface UserListItem {
  user_id: number;
  username: string;
  role: string;
  site_name: string;
  is_active: boolean;
  created_at: string;
}

export interface UserListResponse {
  users: UserListItem[];
}

export interface CreateUserPayload {
  username: string;
  password: string;
  role_id: number;
  site_id: number;
}

export interface UpdateUserPayload {
  role_id?: number;
  site_id?: number;
  is_active?: boolean;
  reset_password?: string;
}

export interface UserDetail {
  user_id: number;
  username: string;
  role_id: number;
  role: string;
  site_id: number;
  site_name: string;
  is_active: boolean;
  created_at: string;
  updated_at: string;
}
