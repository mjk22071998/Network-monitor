<script setup lang="ts">
import { ref } from 'vue';
import { useIngestStore } from '../../stores/ingest';
import StatusBadge from '../../components/common/StatusBadge.vue';
import PaginationFooter from '../../components/common/PaginationFooter.vue';
import { 
  UploadCloud, 
  FileSpreadsheet, 
  CheckCircle2, 
  AlertTriangle, 
  Trash2, 
  Save, 
  Check, 
  X, 
  AlertOctagon 
} from 'lucide-vue-next';
import type { StagingRecord } from '../../types/ingest';

const ingestStore = useIngestStore();

const emit = defineEmits<{
  (e: 'commit'): void;
}>();

const fileInput = ref<HTMLInputElement | null>(null);
const editingRecordId = ref<number | null>(null);
const editForm = ref<{
  display_name: string;
  primary_ip: string;
  mac_address: string;
  device_type_str: string;
  group_path_str: string;
  parent_device_str: string;
}>({
  display_name: '',
  primary_ip: '',
  mac_address: '',
  device_type_str: '',
  group_path_str: '',
  parent_device_str: ''
});

function triggerFileInput() {
  fileInput.value?.click();
}

async function handleFileChange(e: Event) {
  const target = e.target as HTMLInputElement;
  if (!target.files || target.files.length === 0) return;
  const file = target.files[0];
  await ingestStore.uploadCsv(file);
  target.value = '';
}

function startEdit(record: StagingRecord) {
  editingRecordId.value = record.record_id;
  editForm.value = {
    display_name: record.display_name || '',
    primary_ip: record.primary_ip || '',
    mac_address: record.mac_address || '',
    device_type_str: record.device_type_str || '',
    group_path_str: record.group_path_str || '',
    parent_device_str: record.parent_device_str || ''
  };
}

function cancelEdit() {
  editingRecordId.value = null;
}

async function saveEdit(recordId: number) {
  await ingestStore.updateRecord(recordId, {
    display_name: editForm.value.display_name.trim() || undefined,
    primary_ip: editForm.value.primary_ip.trim() || undefined,
    mac_address: editForm.value.mac_address.trim() || undefined,
    device_type_str: editForm.value.device_type_str.trim() || undefined,
    group_path_str: editForm.value.group_path_str.trim() || undefined,
    parent_device_str: editForm.value.parent_device_str.trim() || undefined
  });
  editingRecordId.value = null;
}

async function handleDiscard() {
  if (!confirm('Discard all staged records in this import batch?')) return;
  await ingestStore.discardBatch();
}
</script>

<template>
  <div class="h-full flex flex-col bg-tactical-surface rounded-xl border border-tactical-border overflow-hidden select-none font-mono">
    
    <!-- Header & Batch Controls -->
    <div class="p-4 border-b border-tactical-border bg-tactical-surface flex flex-wrap items-center justify-between gap-4">
      <div>
        <h2 class="text-sm font-bold uppercase tracking-wider text-slate-100 flex items-center gap-2">
          <FileSpreadsheet class="w-4 h-4 text-tactical-cyan" />
          <span>Low-Memory Streaming Bulk Ingestion Pipeline</span>
        </h2>
        <p class="text-[10px] text-slate-500 font-sans">
          64KB ring-buffer streaming parser. 500-record EF Core chunks maintaining &lt;15MB RSS memory footprint.
        </p>
      </div>

      <!-- Action Buttons -->
      <div class="flex items-center space-x-3 text-xs">
        <input
          ref="fileInput"
          type="file"
          accept=".csv"
          class="hidden"
          @change="handleFileChange"
        />

        <button
          @click="triggerFileInput"
          :disabled="ingestStore.isLoading"
          class="flex items-center space-x-1.5 px-3.5 py-1.5 rounded-lg bg-tactical-panel border border-tactical-border hover:border-tactical-cyan text-slate-200 hover:text-tactical-cyan font-bold transition"
        >
          <UploadCloud class="w-4 h-4" />
          <span>Upload CSV File</span>
        </button>

        <button
          v-if="ingestStore.currentBatch"
          @click="emit('commit')"
          :disabled="ingestStore.isLoading || ingestStore.currentBatch.valid_records === 0"
          class="flex items-center space-x-1.5 px-3.5 py-1.5 rounded-lg bg-tactical-online hover:bg-emerald-400 text-slate-950 font-bold transition shadow-[0_0_12px_rgba(0,255,102,0.3)] disabled:opacity-50"
        >
          <CheckCircle2 class="w-4 h-4" />
          <span>Commit Batch ({{ ingestStore.currentBatch.valid_records }} Valid)</span>
        </button>

        <button
          v-if="ingestStore.currentBatch"
          @click="handleDiscard"
          :disabled="ingestStore.isLoading"
          class="p-1.5 rounded-lg bg-tactical-panel border border-tactical-border text-slate-400 hover:text-rose-400 hover:border-rose-400 transition"
          title="Discard Staged Batch"
        >
          <Trash2 class="w-4 h-4" />
        </button>
      </div>
    </div>

    <!-- Batch Summary Banner (if batch loaded) -->
    <div 
      v-if="ingestStore.currentBatch"
      class="bg-tactical-panel px-4 py-2.5 border-b border-tactical-border flex items-center justify-between text-xs"
    >
      <div class="flex items-center space-x-4">
        <span class="text-slate-400">Batch ID: <strong class="text-tactical-cyan">#{{ ingestStore.currentBatch.batch_id }}</strong></span>
        <span class="text-slate-400">Total: <strong class="text-slate-100">{{ ingestStore.currentBatch.total_records }}</strong></span>
        <span class="text-emerald-400">Valid: <strong>{{ ingestStore.currentBatch.valid_records }}</strong></span>
        <span class="text-amber-400">Incomplete: <strong>{{ ingestStore.currentBatch.incomplete_records }}</strong></span>
        <span class="text-rose-400">Conflicts: <strong>{{ ingestStore.currentBatch.conflict_records }}</strong></span>
      </div>

      <div class="text-[10px] text-slate-500">
        Status: <span class="text-tactical-cyan font-bold">{{ ingestStore.currentBatch.status }}</span>
      </div>
    </div>

    <!-- Error Banner -->
    <div v-if="ingestStore.error" class="m-4 p-3 rounded bg-rose-950/40 border border-rose-500/50 text-rose-300 text-xs flex items-center space-x-2">
      <AlertOctagon class="w-4 h-4 flex-shrink-0" />
      <span>{{ ingestStore.error }}</span>
    </div>

    <!-- Interactive Spreadsheet Staging Grid -->
    <div class="flex-1 overflow-auto">
      <table class="w-full text-left text-xs border-collapse">
        <thead class="sticky top-0 bg-tactical-panel border-b border-tactical-border text-slate-400 text-[10px] uppercase tracking-wider font-bold z-10">
          <tr>
            <th class="py-3 px-3 w-16">Row #</th>
            <th class="py-3 px-3 w-28">Status</th>
            <th class="py-3 px-3">Display Name</th>
            <th class="py-3 px-3">IPv4 Address</th>
            <th class="py-3 px-3">MAC Address</th>
            <th class="py-3 px-3">Type</th>
            <th class="py-3 px-3">Group Path</th>
            <th class="py-3 px-3">Parent Switch</th>
            <th class="py-3 px-3">Diagnostics / Error</th>
            <th class="py-3 px-3 text-right">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-tactical-border/50 text-slate-300">
          <tr 
            v-for="rec in ingestStore.stagingRecords" 
            :key="rec.record_id"
            class="hover:bg-slate-900/60 transition"
          >
            <!-- Row # -->
            <td class="py-2.5 px-3 font-mono text-slate-500">
              #{{ rec.row_number }}
            </td>

            <!-- Status -->
            <td class="py-2.5 px-3">
              <StatusBadge :status="rec.validation_status" />
            </td>

            <!-- Inline Editable Display Name -->
            <td class="py-2.5 px-3 font-semibold">
              <input
                v-if="editingRecordId === rec.record_id"
                v-model="editForm.display_name"
                class="bg-tactical-surface border border-tactical-cyan rounded px-2 py-1 text-slate-100 text-xs w-full focus:outline-none"
              />
              <span v-else class="text-slate-200">{{ rec.display_name || 'N/A' }}</span>
            </td>

            <!-- Inline Editable IPv4 -->
            <td class="py-2.5 px-3 font-mono">
              <input
                v-if="editingRecordId === rec.record_id"
                v-model="editForm.primary_ip"
                class="bg-tactical-surface border border-tactical-cyan rounded px-2 py-1 text-tactical-cyan text-xs w-full focus:outline-none"
              />
              <span v-else class="text-tactical-cyan">{{ rec.primary_ip || 'N/A' }}</span>
            </td>

            <!-- Inline Editable MAC -->
            <td class="py-2.5 px-3 font-mono text-slate-400">
              <input
                v-if="editingRecordId === rec.record_id"
                v-model="editForm.mac_address"
                class="bg-tactical-surface border border-tactical-cyan rounded px-2 py-1 text-slate-100 text-xs w-full focus:outline-none"
              />
              <span v-else>{{ rec.mac_address || 'N/A' }}</span>
            </td>

            <!-- Type -->
            <td class="py-2.5 px-3">
              <input
                v-if="editingRecordId === rec.record_id"
                v-model="editForm.device_type_str"
                class="bg-tactical-surface border border-tactical-cyan rounded px-2 py-1 text-slate-100 text-xs w-full focus:outline-none"
              />
              <span v-else class="text-slate-300">{{ rec.device_type_str || 'N/A' }}</span>
            </td>

            <!-- Group Path -->
            <td class="py-2.5 px-3 text-slate-400">
              <input
                v-if="editingRecordId === rec.record_id"
                v-model="editForm.group_path_str"
                class="bg-tactical-surface border border-tactical-cyan rounded px-2 py-1 text-slate-100 text-xs w-full focus:outline-none"
              />
              <span v-else>{{ rec.group_path_str || 'N/A' }}</span>
            </td>

            <!-- Parent Switch -->
            <td class="py-2.5 px-3 text-slate-400">
              <input
                v-if="editingRecordId === rec.record_id"
                v-model="editForm.parent_device_str"
                class="bg-tactical-surface border border-tactical-cyan rounded px-2 py-1 text-slate-100 text-xs w-full focus:outline-none"
              />
              <span v-else>{{ rec.parent_device_str || 'N/A' }}</span>
            </td>

            <!-- Validation Error / Reason -->
            <td class="py-2.5 px-3 text-[11px]">
              <span v-if="rec.validation_error" class="text-rose-400 flex items-center gap-1">
                <AlertTriangle class="w-3.5 h-3.5 flex-shrink-0" />
                <span>{{ rec.validation_error }}</span>
              </span>
              <span v-else class="text-emerald-400 flex items-center gap-1">
                <Check class="w-3.5 h-3.5 flex-shrink-0" />
                <span>Ready to Commit</span>
              </span>
            </td>

            <!-- Actions -->
            <td class="py-2.5 px-3 text-right whitespace-nowrap">
              <div v-if="editingRecordId === rec.record_id" class="inline-flex items-center space-x-1">
                <button
                  @click="saveEdit(rec.record_id)"
                  class="p-1 rounded bg-tactical-online text-slate-950 hover:bg-emerald-400 transition"
                  title="Save and Revalidate"
                >
                  <Save class="w-3.5 h-3.5" />
                </button>
                <button
                  @click="cancelEdit"
                  class="p-1 rounded bg-slate-800 text-slate-400 hover:text-white transition"
                  title="Cancel Edit"
                >
                  <X class="w-3.5 h-3.5" />
                </button>
              </div>

              <div v-else class="inline-flex items-center">
                <button
                  @click="startEdit(rec)"
                  class="px-2 py-0.5 rounded text-[10px] bg-tactical-panel border border-tactical-border hover:border-tactical-cyan text-slate-300 hover:text-tactical-cyan transition"
                >
                  Edit Cell
                </button>
              </div>
            </td>
          </tr>

          <!-- Empty State -->
          <tr v-if="!ingestStore.currentBatch || ingestStore.stagingRecords.length === 0">
            <td colspan="10" class="py-16 text-center text-slate-500">
              <UploadCloud class="w-10 h-10 mx-auto mb-2 text-slate-600" />
              <p class="font-bold text-slate-400">No active staging batch</p>
              <p class="text-[11px] text-slate-600 mt-1">Upload a CSV inventory export to initiate streaming pre-validation</p>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Standardized Pagination Footer -->
    <PaginationFooter
      v-if="ingestStore.currentBatch"
      :current-page="ingestStore.currentPage"
      :page-size="ingestStore.pageSize"
      :total-records="ingestStore.totalRecords"
      @update:page="(p) => ingestStore.fetchRecords(ingestStore.currentBatch!.batch_id, p, ingestStore.pageSize)"
      @update:page-size="(s) => ingestStore.fetchRecords(ingestStore.currentBatch!.batch_id, 1, s)"
    />

  </div>
</template>
