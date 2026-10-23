<script setup lang="ts">
import { ref } from 'vue';
import { useGroupsStore } from '../../stores/groups';
import type { GroupNode } from '../../types/group';
import { 
  Folder, 
  FolderPlus, 
  ChevronRight, 
  ChevronDown, 
  Edit3, 
  Trash2, 
  Plus, 
  Layers, 
  Monitor,
  AlertCircle
} from 'lucide-vue-next';

const groupsStore = useGroupsStore();

const emit = defineEmits<{
  (e: 'createTop'): void;
  (e: 'createSub', parentGroup: GroupNode): void;
  (e: 'edit', group: GroupNode): void;
}>();

const expandedGroups = ref<Set<number>>(new Set([1])); // Unassigned expanded by default
const isDeleting = ref(false);
const deleteError = ref<string | null>(null);

function toggleExpand(id: number) {
  if (expandedGroups.value.has(id)) {
    expandedGroups.value.delete(id);
  } else {
    expandedGroups.value.add(id);
  }
}

async function handleDelete(group: GroupNode) {
  if (group.group_id === 1) {
    alert('Root Group 1 (Unassigned Group) cannot be deleted.');
    return;
  }

  const ok = confirm(`Delete group '${group.group_name}'? Any assigned devices will safely fall back to Group 1 (Unassigned).`);
  if (!ok) return;

  deleteError.value = null;
  isDeleting.value = true;
  try {
    await groupsStore.deleteGroup(group.group_id);
  } catch (err: any) {
    deleteError.value = err.response?.data?.error || err.message || 'Failed to delete group';
  } finally {
    isDeleting.value = false;
  }
}
</script>

<template>
  <div class="h-full flex flex-col bg-tactical-surface rounded-xl border border-tactical-border overflow-hidden select-none font-mono">
    
    <!-- Header -->
    <div class="p-4 border-b border-tactical-border bg-tactical-surface flex items-center justify-between">
      <div>
        <h2 class="text-sm font-bold uppercase tracking-wider text-slate-100 flex items-center gap-2">
          <Layers class="w-4 h-4 text-tactical-cyan" />
          <span>Computer Lab Hierarchy & Department Groupings</span>
        </h2>
        <p class="text-[10px] text-slate-500 font-sans">
          Recursive 3NF self-referencing tree structure. Deleting a group reassigns member devices to Group 1.
        </p>
      </div>

      <button
        @click="emit('createTop')"
        class="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg bg-tactical-cyan hover:bg-cyan-400 text-slate-950 font-bold text-xs transition shadow-[0_0_12px_rgba(0,240,255,0.25)]"
      >
        <Plus class="w-4 h-4" />
        <span>New Top Lab Group</span>
      </button>
    </div>

    <!-- Error Banner -->
    <div v-if="deleteError" class="m-4 p-3 rounded bg-rose-950/40 border border-rose-500/50 text-rose-300 text-xs flex items-center space-x-2">
      <AlertCircle class="w-4 h-4 flex-shrink-0" />
      <span>{{ deleteError }}</span>
    </div>

    <!-- Tree Content Area -->
    <div class="flex-1 overflow-auto p-6 space-y-3">
      <div 
        v-for="rootNode in groupsStore.groupsTree" 
        :key="rootNode.group_id"
        class="border border-tactical-border rounded-lg bg-tactical-panel/60 overflow-hidden"
      >
        <!-- Root Row -->
        <div class="p-3.5 flex items-center justify-between hover:bg-slate-900/60 transition">
          <div class="flex items-center space-x-2.5">
            <button 
              v-if="rootNode.children && rootNode.children.length > 0"
              @click="toggleExpand(rootNode.group_id)"
              class="p-1 rounded text-slate-400 hover:text-white"
            >
              <ChevronDown v-if="expandedGroups.has(rootNode.group_id)" class="w-4 h-4" />
              <ChevronRight v-else class="w-4 h-4" />
            </button>
            <div v-else class="w-6"></div>

            <Folder class="w-4 h-4 text-tactical-cyan" />
            
            <div>
              <div class="flex items-center space-x-2">
                <span class="font-bold text-slate-100 text-xs">{{ rootNode.group_name }}</span>
                <span class="text-[9px] font-mono px-1.5 py-0.2 rounded bg-tactical-surface border border-tactical-border text-slate-400">
                  ID: #{{ rootNode.group_id }}
                </span>
                <span v-if="rootNode.group_id === 1" class="text-[9px] font-mono px-1.5 py-0.2 rounded bg-tactical-cyan/10 border border-tactical-cyan/40 text-tactical-cyan">
                  DEFAULT FALLBACK
                </span>
              </div>
              <p v-if="rootNode.description" class="text-[10px] text-slate-500 font-sans mt-0.5">
                {{ rootNode.description }}
              </p>
            </div>
          </div>

          <!-- Right: Device Count & Actions -->
          <div class="flex items-center space-x-3 text-xs">
            <div class="flex items-center space-x-1 text-slate-400">
              <Monitor class="w-3.5 h-3.5 text-slate-500" />
              <span>{{ rootNode.device_count }} devices</span>
            </div>

            <div class="flex items-center space-x-1">
              <button
                @click="emit('createSub', rootNode)"
                class="p-1 rounded bg-tactical-surface border border-tactical-border text-slate-400 hover:text-tactical-cyan"
                title="Add Subgroup under this lab"
              >
                <FolderPlus class="w-3.5 h-3.5" />
              </button>

              <button
                @click="emit('edit', rootNode)"
                class="p-1 rounded bg-tactical-surface border border-tactical-border text-slate-400 hover:text-tactical-amber"
                title="Edit Group"
              >
                <Edit3 class="w-3.5 h-3.5" />
              </button>

              <button
                v-if="rootNode.group_id !== 1"
                @click="handleDelete(rootNode)"
                class="p-1 rounded bg-tactical-surface border border-tactical-border text-slate-400 hover:text-rose-400"
                title="Delete Group"
              >
                <Trash2 class="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        </div>

        <!-- Child Subgroups (Recursive / Nested) -->
        <div 
          v-if="expandedGroups.has(rootNode.group_id) && rootNode.children && rootNode.children.length > 0"
          class="border-t border-tactical-border/60 bg-tactical-bg/40 divide-y divide-tactical-border/40 pl-8"
        >
          <div 
            v-for="subNode in rootNode.children"
            :key="subNode.group_id"
            class="p-3 flex items-center justify-between hover:bg-slate-900/60 transition"
          >
            <div class="flex items-center space-x-2.5">
              <Folder class="w-4 h-4 text-tactical-amber" />
              <div>
                <div class="flex items-center space-x-2">
                  <span class="font-bold text-slate-200 text-xs">{{ subNode.group_name }}</span>
                  <span class="text-[9px] font-mono px-1.5 py-0.2 rounded bg-tactical-surface border border-tactical-border text-slate-400">
                    ID: #{{ subNode.group_id }}
                  </span>
                </div>
                <p v-if="subNode.description" class="text-[10px] text-slate-500 font-sans mt-0.5">
                  {{ subNode.description }}
                </p>
              </div>
            </div>

            <div class="flex items-center space-x-3 text-xs">
              <div class="flex items-center space-x-1 text-slate-400">
                <Monitor class="w-3.5 h-3.5 text-slate-500" />
                <span>{{ subNode.device_count }} devices</span>
              </div>

              <div class="flex items-center space-x-1">
                <button
                  @click="emit('edit', subNode)"
                  class="p-1 rounded bg-tactical-surface border border-tactical-border text-slate-400 hover:text-tactical-amber"
                  title="Edit Group"
                >
                  <Edit3 class="w-3.5 h-3.5" />
                </button>

                <button
                  @click="handleDelete(subNode)"
                  class="p-1 rounded bg-tactical-surface border border-tactical-border text-slate-400 hover:text-rose-400"
                  title="Delete Group"
                >
                  <Trash2 class="w-3.5 h-3.5" />
                </button>
              </div>
            </div>
          </div>
        </div>

      </div>

      <!-- Empty State -->
      <div v-if="groupsStore.groupsTree.length === 0" class="py-12 text-center text-slate-500">
        <Layers class="w-8 h-8 mx-auto mb-2 text-slate-600" />
        <p>No lab groups available.</p>
      </div>
    </div>

  </div>
</template>
