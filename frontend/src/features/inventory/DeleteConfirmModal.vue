<script setup lang="ts">
import { ref } from 'vue';
import ModalWrapper from '../../components/common/ModalWrapper.vue';
import { useDevicesStore } from '../../stores/devices';
import { AlertTriangle } from 'lucide-vue-next';
import type { Device } from '../../types/device';

const props = defineProps<{
  isOpen: boolean;
  device?: Device | null;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'deleted'): void;
}>();

const devicesStore = useDevicesStore();
const isDeleting = ref(false);
const error = ref<string | null>(null);

async function confirmDelete() {
  if (!props.device) return;
  isDeleting.value = true;
  error.value = null;
  try {
    await devicesStore.deleteDevice(props.device.device_id);
    emit('deleted');
    emit('close');
  } catch (err: any) {
    error.value = err.response?.data?.error || err.message || 'Failed to delete device';
  } finally {
    isDeleting.value = false;
  }
}
</script>

<template>
  <ModalWrapper
    :is-open="isOpen"
    title="Orphan-Safe Deletion Protocol"
    subtitle="PostgreSQL 18 Relational Integrity Assurance"
    maxWidthClass="max-w-md"
    @close="emit('close')"
  >
    <div class="space-y-4">
      <div class="flex items-start space-x-3 p-3.5 rounded-lg bg-rose-950/40 border border-rose-500/40 text-rose-300">
        <AlertTriangle class="w-5 h-5 flex-shrink-0 mt-0.5 text-rose-400" />
        <div class="text-xs space-y-1">
          <p class="font-bold text-slate-100">
            Confirm permanent deletion of {{ device?.display_name }} ({{ device?.primary_ip }})?
          </p>
          <p class="text-[11px] text-slate-300">
            Orphan-Safe Guarantee: Any downstream child devices or lab workstations linked to this switch will automatically be decoupled (<code class="bg-black/50 px-1 py-0.5 rounded text-tactical-cyan">parent_device_id = NULL</code>). Child records, MAC addresses, and historical telemetry samples will NOT be deleted.
          </p>
        </div>
      </div>

      <div v-if="error" class="p-2.5 rounded bg-rose-950/60 border border-rose-500/50 text-rose-300 text-xs font-mono">
        {{ error }}
      </div>

      <div class="p-3 rounded bg-tactical-panel border border-tactical-border text-xs font-mono text-slate-400 space-y-1">
        <div><strong class="text-slate-300">Target ID:</strong> #{{ device?.device_id }}</div>
        <div><strong class="text-slate-300">MAC Address:</strong> {{ device?.mac_address }}</div>
        <div><strong class="text-slate-300">Type:</strong> {{ device?.type_code }}</div>
        <div><strong class="text-slate-300">Zone:</strong> {{ device?.zone || 'N/A' }}</div>
      </div>
    </div>

    <template #footer>
      <button
        @click="emit('close')"
        type="button"
        class="px-4 py-2 rounded text-xs font-mono bg-tactical-panel border border-tactical-border text-slate-300 hover:text-white"
      >
        Cancel
      </button>

      <button
        @click="confirmDelete"
        :disabled="isDeleting"
        type="button"
        class="px-5 py-2 rounded text-xs font-mono font-bold bg-rose-600 text-white hover:bg-rose-500 transition shadow-lg shadow-rose-600/30 disabled:opacity-50"
      >
        {{ isDeleting ? 'Deleting...' : 'Delete Permanently' }}
      </button>
    </template>
  </ModalWrapper>
</template>
