import { apiClient } from './client';
import type { LoginResponse, CurrentUser } from '../types/auth';

export const authApi = {
  async login(username: string, password: string): TaskPromise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>('/auth/login', {
      username,
      password
    });
    return response.data;
  },

  async logout(): Promise<void> {
    await apiClient.post('/auth/logout');
  },

  async getMe(): Promise<CurrentUser> {
    const response = await apiClient.get<CurrentUser>('/auth/me');
    return response.data;
  }
};

type TaskPromise<T> = Promise<T>;
