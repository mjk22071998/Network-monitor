export interface GroupNode {
  group_id: number;
  site_id: number;
  group_name: string;
  parent_group_id: number | null;
  description: string | null;
  created_at: string;
  device_count: number;
  children: GroupNode[];
}

export interface CreateGroupRequest {
  group_name: string;
  site_id: number;
  parent_group_id?: number | null;
  description?: string | null;
}

export interface UpdateGroupRequest {
  group_name?: string;
  parent_group_id?: number | null;
  description?: string | null;
}
