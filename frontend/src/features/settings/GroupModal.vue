<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import ModalWrapper from '../../components/common/ModalWrapper.vue';
import { useGroupsStore } from '../../stores/groups';
import type { GroupNode, CreateGroupRequest, UpdateGroupRequest } from '../../types/group';

const props = defineProps<{
  isOpen: boolean;
  groupToEdit?: GroupNode | null;
  parentGroupForNewSubgroup?: GroupNode | null;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'saved'): void;
}>();

const groupsStore = useGroupsStore();
const isSubmitting = ref(false);
const error = ref<string | null>(null);

const groupName = ref('');
const parentGroupId = ref<number | null>(null);
const description = ref('');

const isEditMode = computed(() => !!props.groupToEdit);
const flatGroups = computed(() => groupsStore.flattenGroups(groupsStore.groupsTree));

watch(() => props.isOpen, (newVal) => {
  if (newVal) {
    error.value = null;
    if (props.groupToEdit) {
      groupName.value = props.groupToEdit.group_name;
      parentGroupId.value = props.groupToEdit.parent_group_id;
      description.value = props.groupToEdit.description || '';
    } else {
      groupName.value = '';
      parentGroupId.value = props.parentGroupForNewSubgroup ? props.parentGroupForNewSubgroup.group_id : null;
      description.value = '';
    }
  }
});

async function handleSubmit() {
  if (!groupName.value.trim()) {
    error.value = 'Lab group name is required.';
    return;
  }

  isSubmitting.value = true;
  error.value = null;
  try {
    if (isEditMode.value && props.groupToEdit) {
      const payload: UpdateGroupRequest = {
        group_name: groupName.value.trim(),
        parent_group_id: parentGroupId.value,
        description: description.value.trim() || null
      };
      await groupsStore.updateGroup(props.groupToEdit.group_id, payload);
    } else {
      const payload: CreateGroupRequest = {
        group_name: groupName.value.trim(),
        site_id: 1,
        parent_group_id: parentGroupId.value,
        description: description.value.trim() || null
      };
      await groupsStore.createGroup(payload);
    }
    emit('saved');
    emit('close');
  } catch (err: any) {
    error.value = err.response?.data?.error || err.message || 'Failed to save group';
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <ModalWrapper
    :is-open="isOpen"
    :title="isEditMode ? `Edit Group — ${groupToEdit?.group_name}` : 'Create New Computer Lab / Department Group'"
    subtitle="3NF Hierarchical Nested Set & Subgroup Tree"
    maxWidthClass="max-w-md"
    @close="emit('close')"
  >
    <div v-if="error" class="mb-4 p-3 rounded bg-rose-950/40 border border-rose-500/50 text-rose-300 text-xs font-mono">
      {{ error }}
    </div>

    <form @submit.prevent="handleSubmit" class="space-y-4 text-xs font-mono">
      <div>
        <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Group / Lab Name *</label>
        <input
          v-model="groupName"
          type="text"
          required
          placeholder="e.g. Computer Science Lab 101"
          class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
        />
      </div>

      <div>
        <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Parent Group (Hierarchy)</label>
        <select
          v-model="parentGroupId"
          class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
        >
          <option :value="null">-- Top-Level Root Group --</option>
          <option 
            v-for="g in flatGroups" 
            :key="g.id" 
            :value="g.id"
            :disabled="Boolean(groupToEdit && g.id === groupToEdit.group_id)"
          >
            {{ g.name }}
          </option>
        </select>
      </div>

      <div>
        <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Description / Purpose</label>
        <textarea
          v-model="description"
          rows="3"
          placeholder="e.g. AI Research Cluster & Workstations"
          class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
        ></textarea>
      </div>
    </form>

    <template #footer>
      <button
        @click="emit('close')"
        type="button"
        class="px-4 py-2 rounded text-xs font-mono bg-tactical-panel border border-tactical-border text-slate-300 hover:text-white"
      >
        Cancel
      </button>

      <button
        @click="handleSubmit"
        :disabled="isSubmitting"
        type="button"
        class="px-5 py-2 rounded text-xs font-mono font-bold bg-tactical-cyan text-slate-950 hover:bg-cyan-400 transition shadow-[0_0_12px_rgba(0,240,255,0.3)] disabled:opacity-50"
      >
        {{ isSubmitting ? 'Saving...' : (isEditMode ? 'Update Group' : 'Create Group') }}
      </button>
    </template>
  </ModalWrapper>
</template>
