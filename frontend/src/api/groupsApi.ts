import { apiClient } from './client';
import type { GroupNode, CreateGroupRequest, UpdateGroupRequest } from '../types/group';

export const groupsApi = {
  async getTree(): Promise<GroupNode[]> {
    const res = await apiClient.get<any>('/groups');
    if (Array.isArray(res.data)) {
      return res.data;
    }
    if (res.data && Array.isArray(res.data.groups)) {
      return res.data.groups;
    }
    return [];
  },

  async create(payload: CreateGroupRequest): Promise<GroupNode> {
    const res = await apiClient.post<GroupNode>('/groups', payload);
    return res.data;
  },

  async update(id: number, payload: UpdateGroupRequest): Promise<GroupNode> {
    const res = await apiClient.put<GroupNode>(`/groups/${id}`, payload);
    return res.data;
  },

  async delete(id: number): Promise<void> {
    await apiClient.delete(`/groups/${id}`);
  }
};
