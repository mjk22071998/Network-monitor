import { defineStore } from 'pinia';
import { ref, shallowRef } from 'vue';
import { groupsApi } from '../api/groupsApi';
import type { GroupNode, CreateGroupRequest, UpdateGroupRequest } from '../types/group';

export const useGroupsStore = defineStore('groups', () => {
  const groupsTree = shallowRef<GroupNode[]>([]);
  const isLoading = ref<boolean>(false);
  const error = ref<string | null>(null);

  // Helper to flatten groups for select dropdowns
  function flattenGroups(nodes: GroupNode[], depth: number = 0, result: Array<{ id: number; name: string; depth: number }> = []) {
    if (!Array.isArray(nodes)) return result;
    for (const node of nodes) {
      if (!node) continue;
      result.push({
        id: node.group_id,
        name: `${'—'.repeat(depth)} ${node.group_name}`.trim(),
        depth
      });
      if (node.children && Array.isArray(node.children) && node.children.length > 0) {
        flattenGroups(node.children, depth + 1, result);
      }
    }
    return result;
  }

  async function fetchGroups() {
    isLoading.value = true;
    error.value = null;
    try {
      const data = await groupsApi.getTree();
      groupsTree.value = Array.isArray(data) ? data : [];
    } catch (err: any) {
      groupsTree.value = [];
      error.value = err.response?.data?.error || err.message || 'Failed to load groups';
    } finally {
      isLoading.value = false;
    }
  }

  async function createGroup(payload: CreateGroupRequest) {
    isLoading.value = true;
    error.value = null;
    try {
      await groupsApi.create(payload);
      await fetchGroups();
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to create group';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  async function updateGroup(id: number, payload: UpdateGroupRequest) {
    if (!id) return;
    isLoading.value = true;
    error.value = null;
    try {
      await groupsApi.update(id, payload);
      await fetchGroups();
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to update group';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  async function deleteGroup(id: number) {
    if (!id) return;
    isLoading.value = true;
    error.value = null;
    try {
      await groupsApi.delete(id);
      await fetchGroups();
    } catch (err: any) {
      error.value = err.response?.data?.error || err.message || 'Failed to delete group';
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  return {
    groupsTree,
    isLoading,
    error,
    flattenGroups,
    fetchGroups,
    createGroup,
    updateGroup,
    deleteGroup
  };
});
