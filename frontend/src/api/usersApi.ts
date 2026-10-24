import { apiClient } from './client';
import type { UserListItem, CreateUserPayload, UpdateUserPayload, UserDetail } from '../types/user';

export const usersApi = {
  async getUsers(): Promise<UserListItem[]> {
    const res = await apiClient.get<{ users: UserListItem[] }>('/users');
    return res.data?.users || [];
  },

  async getUserById(id: number): Promise<UserDetail> {
    const res = await apiClient.get<UserDetail>(`/users/${id}`);
    return res.data;
  },

  async createUser(payload: CreateUserPayload): Promise<any> {
    const res = await apiClient.post('/users', payload);
    return res.data;
  },

  async updateUser(id: number, payload: UpdateUserPayload): Promise<any> {
    const res = await apiClient.put(`/users/${id}`, payload);
    return res.data;
  },

  async deleteUser(id: number): Promise<void> {
    await apiClient.delete(`/users/${id}`);
  }
};
