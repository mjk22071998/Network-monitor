export enum RoleType {
  NetworkAdmin = 1,
  NetworkEngineer = 2,
  ItSupport = 3
}

export interface AuthUser {
  user_id: number;
  username: string;
  role_id: number;
  site_id: number;
}

export interface LoginResponse {
  token: string;
  user: AuthUser;
  expires_at: string;
}

export interface CurrentUser {
  user_id: number;
  username: string;
  role_id: number;
  role_name: string;
  site_id: number;
  is_active: boolean;
}

export interface ApiErrorEnvelope {
  error: {
    code: string;
    message: string;
    details?: string[] | null;
    timestamp: string;
  };
}
