<script setup lang="ts">
import { ref } from 'vue';
import ModalWrapper from '../../components/common/ModalWrapper.vue';
import { useIngestStore } from '../../stores/ingest';

defineProps<{
  isOpen: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'committed'): void;
}>();

const ingestStore = useIngestStore();
const commitValidOnly = ref(true);
const isCommitting = ref(false);
const error = ref<string | null>(null);

async function confirmCommit() {
  isCommitting.value = true;
  error.value = null;
  try {
    await ingestStore.commitBatch({ commit_valid_only: commitValidOnly.value });
    emit('committed');
    emit('close');
  } catch (err: any) {
    error.value = err.response?.data?.error || err.message || 'Commit failed.';
  } finally {
    isCommitting.value = false;
  }
}
</script>

<template>
  <ModalWrapper
    :is-open="isOpen"
    title="Commit Staged Batch to Production 3NF Inventory"
    subtitle="Atomic NpgsqlRetryingExecutionStrategy Transaction Wrapper"
    maxWidthClass="max-w-md"
    @close="emit('close')"
  >
    <div class="space-y-4">
      <div class="p-3.5 rounded bg-tactical-panel border border-tactical-border text-xs font-mono space-y-2">
        <div class="flex justify-between">
          <span class="text-slate-400">Total Staged Records:</span>
          <span class="text-slate-100 font-bold">{{ ingestStore.currentBatch?.total_records }}</span>
        </div>
        <div class="flex justify-between">
          <span class="text-emerald-400">Valid Records:</span>
          <span class="text-emerald-300 font-bold">{{ ingestStore.currentBatch?.valid_records }}</span>
        </div>
        <div class="flex justify-between">
          <span class="text-amber-400">Incomplete Records:</span>
          <span class="text-amber-300 font-bold">{{ ingestStore.currentBatch?.incomplete_records }}</span>
        </div>
        <div class="flex justify-between">
          <span class="text-rose-400">Conflict Records:</span>
          <span class="text-rose-300 font-bold">{{ ingestStore.currentBatch?.conflict_records }}</span>
        </div>
      </div>

      <!-- Commit Valid Only Checkbox -->
      <div class="p-3 rounded bg-tactical-surface border border-tactical-cyan/30 flex items-start space-x-3">
        <input
          v-model="commitValidOnly"
          type="checkbox"
          id="commit-valid-chk"
          class="mt-0.5 rounded bg-tactical-panel border-tactical-border text-tactical-cyan focus:ring-0"
        />
        <label for="commit-valid-chk" class="text-xs text-slate-200 select-none">
          <strong class="text-tactical-cyan block">Commit Valid Only (Recommended)</strong>
          <span class="text-[11px] text-slate-400">
            Atomically inserts the {{ ingestStore.currentBatch?.valid_records }} valid records into <code class="text-slate-300">devices</code>. Any unresolved or conflicting rows remain in staging for review.
          </span>
        </label>
      </div>

      <div v-if="error" class="p-2.5 rounded bg-rose-950/60 border border-rose-500/50 text-rose-300 text-xs font-mono">
        {{ error }}
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
        @click="confirmCommit"
        :disabled="isCommitting || !ingestStore.currentBatch || ingestStore.currentBatch.valid_records === 0"
        type="button"
        class="px-5 py-2 rounded text-xs font-mono font-bold bg-tactical-online text-slate-950 hover:bg-emerald-400 transition shadow-[0_0_12px_rgba(0,255,102,0.3)] disabled:opacity-50"
      >
        {{ isCommitting ? 'Committing...' : 'Confirm Atomic Commit' }}
      </button>
    </template>
  </ModalWrapper>
</template>
