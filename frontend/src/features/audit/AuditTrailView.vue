<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAuditStore } from '../../stores/audit';
import { useAuthStore } from '../../stores/auth';
import PaginationFooter from '../../components/common/PaginationFooter.vue';
import ModalWrapper from '../../components/common/ModalWrapper.vue';
import { 
  ShieldCheck, 
  RotateCcw, 
  Lock 
} from 'lucide-vue-next';
import type { AuditLogEntry } from '../../types/audit';

const auditStore = useAuditStore();
const authStore = useAuthStore();

const selectedLogForDetails = ref<AuditLogEntry | null>(null);

onMounted(async () => {
  if (authStore.isAdmin) {
    await auditStore.fetchLogs();
  }
});

function openDetails(log: AuditLogEntry) {
  selectedLogForDetails.value = log;
}

function closeDetails() {
  selectedLogForDetails.value = null;
}

function formatDate(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleString();
  } catch {
    return iso;
  }
}
</script>

<template>
  <div class="h-full flex flex-col bg-tactical-surface rounded-xl border border-tactical-border overflow-hidden select-none font-mono">
    
    <!-- Header -->
    <div class="p-4 border-b border-tactical-border bg-tactical-surface flex flex-wrap items-center justify-between gap-4">
      <div>
        <h2 class="text-sm font-bold uppercase tracking-wider text-slate-100 flex items-center gap-2">
          <ShieldCheck class="w-4 h-4 text-tactical-cyan" />
          <span>Security Audit Trail & Non-Repudiation Log</span>
        </h2>
        <p class="text-[10px] text-slate-500 font-sans">
          PostgreSQL 18 append-only immutable security ledger. Direct updates and deletes are permanently aborted by trigger.
        </p>
      </div>

      <!-- Filters (Admin Only) -->
      <div v-if="authStore.isAdmin" class="flex flex-wrap items-center gap-2.5 text-xs">
        <!-- Action Code Filter -->
        <select
          v-model="auditStore.selectedActionCode"
          @change="auditStore.fetchLogs(1)"
          class="bg-tactical-panel border border-tactical-border rounded px-2.5 py-1.5 text-slate-300 focus:outline-none focus:border-tactical-cyan text-xs"
        >
          <option value="">All Action Codes</option>
          <option value="AUTH_LOGIN">AUTH_LOGIN</option>
          <option value="AUTH_LOGOUT">AUTH_LOGOUT</option>
          <option value="AUTH_FAILURE">AUTH_FAILURE</option>
          <option value="DEVICE_CREATE">DEVICE_CREATE</option>
          <option value="DEVICE_UPDATE">DEVICE_UPDATE</option>
          <option value="DEVICE_DELETE">DEVICE_DELETE</option>
          <option value="MAINTENANCE_TOGGLE">MAINTENANCE_TOGGLE</option>
          <option value="GROUP_CREATE">GROUP_CREATE</option>
          <option value="GROUP_UPDATE">GROUP_UPDATE</option>
          <option value="GROUP_DELETE">GROUP_DELETE</option>
          <option value="BATCH_UPLOAD">BATCH_UPLOAD</option>
          <option value="BATCH_COMMIT">BATCH_COMMIT</option>
          <option value="BATCH_DISCARD">BATCH_DISCARD</option>
        </select>

        <button
          @click="auditStore.resetFilters"
          class="p-1.5 rounded bg-tactical-panel border border-tactical-border hover:border-tactical-cyan text-slate-400 hover:text-tactical-cyan transition"
          title="Reset Filters"
        >
          <RotateCcw class="w-3.5 h-3.5" />
        </button>
      </div>
    </div>

    <!-- Non-Admin RBAC Gate -->
    <div v-if="!authStore.isAdmin" class="flex-1 flex items-center justify-center p-8">
      <div class="max-w-md text-center space-y-3 p-6 rounded-xl bg-tactical-panel border border-tactical-border">
        <Lock class="w-10 h-10 text-rose-500 mx-auto" />
        <h3 class="text-sm font-bold text-slate-100 uppercase tracking-wider">Access Restricted — Admin Authority Required</h3>
        <p class="text-xs text-slate-400 font-sans leading-relaxed">
          The Security Audit Trail is strictly guarded under Role-Based Access Control (<code class="text-rose-400">NETWORK_ADMIN</code> exclusively). Non-administrative operators cannot query non-repudiation ledgers.
        </p>
      </div>
    </div>

    <!-- Admin Audit Logs Table -->
    <div v-else class="flex-1 overflow-auto">
      <table class="w-full text-left text-xs border-collapse">
        <thead class="sticky top-0 bg-tactical-panel border-b border-tactical-border text-slate-400 text-[10px] uppercase tracking-wider font-bold z-10">
          <tr>
            <th class="py-3 px-4 w-44">Timestamp</th>
            <th class="py-3 px-4">Operator</th>
            <th class="py-3 px-4">Action Code</th>
            <th class="py-3 px-4">Target Entity</th>
            <th class="py-3 px-4">Target ID</th>
            <th class="py-3 px-4">IP Address</th>
            <th class="py-3 px-4">Audit Details</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-tactical-border/50 text-slate-300">
          <tr 
            v-for="log in auditStore.logs"
            :key="log.log_id"
            class="hover:bg-slate-900/60 transition cursor-pointer"
            @click="openDetails(log)"
          >
            <!-- Timestamp -->
            <td class="py-2.5 px-4 whitespace-nowrap text-slate-400 text-[11px]">
              {{ formatDate(log.logged_at) }}
            </td>

            <!-- Operator -->
            <td class="py-2.5 px-4 font-bold text-tactical-cyan whitespace-nowrap">
              {{ log.username || (log.user_id ? `User #${log.user_id}` : 'SYSTEM') }}
            </td>

            <!-- Action Code -->
            <td class="py-2.5 px-4 whitespace-nowrap">
              <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-tactical-panel border border-tactical-border text-slate-200">
                {{ log.action_code }}
              </span>
            </td>

            <!-- Target Entity -->
            <td class="py-2.5 px-4 text-slate-300">
              {{ log.target_entity }}
            </td>

            <!-- Target ID -->
            <td class="py-2.5 px-4 text-slate-400">
              {{ log.target_id || 'N/A' }}
            </td>

            <!-- IP Address -->
            <td class="py-2.5 px-4 font-mono text-slate-400">
              {{ log.ip_address }}
            </td>

            <!-- Details Preview -->
            <td class="py-2.5 px-4 text-slate-300 truncate max-w-xs">
              {{ log.details }}
            </td>
          </tr>

          <!-- Empty State -->
          <tr v-if="auditStore.logs.length === 0">
            <td colspan="7" class="py-12 text-center text-slate-500">
              <ShieldCheck class="w-8 h-8 mx-auto mb-2 text-slate-600" />
              <p>No audit trail records found for selected query.</p>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Standardized Pagination Footer (Admin Only) -->
    <PaginationFooter
      v-if="authStore.isAdmin"
      :current-page="auditStore.pagination.page"
      :page-size="auditStore.pagination.limit"
      :total-records="auditStore.pagination.total_records"
      @update:page="(p) => auditStore.setPage(p)"
      @update:page-size="(s) => auditStore.setLimit(s)"
    />

    <!-- JSON / Details Modal -->
    <ModalWrapper
      :is-open="!!selectedLogForDetails"
      :title="`Audit Log Record #${selectedLogForDetails?.log_id}`"
      subtitle="Immutable Audit Trail Metadata"
      maxWidthClass="max-w-md"
      @close="closeDetails"
    >
      <div v-if="selectedLogForDetails" class="space-y-3 font-mono text-xs">
        <div class="grid grid-cols-2 gap-2 p-3 rounded bg-tactical-panel border border-tactical-border text-[11px]">
          <div><strong class="text-slate-400">Action:</strong> {{ selectedLogForDetails.action_code }}</div>
          <div><strong class="text-slate-400">Entity:</strong> {{ selectedLogForDetails.target_entity }}</div>
          <div><strong class="text-slate-400">Operator:</strong> {{ selectedLogForDetails.username || 'SYSTEM' }}</div>
          <div><strong class="text-slate-400">IP:</strong> {{ selectedLogForDetails.ip_address }}</div>
          <div class="col-span-2"><strong class="text-slate-400">Timestamp:</strong> {{ formatDate(selectedLogForDetails.logged_at) }}</div>
        </div>

        <div>
          <label class="block text-slate-400 uppercase text-[10px] font-bold mb-1">Details Payload</label>
          <pre class="p-3 rounded bg-tactical-panel border border-tactical-border text-slate-200 text-[11px] whitespace-pre-wrap overflow-x-auto">{{ selectedLogForDetails.details }}</pre>
        </div>
      </div>
    </ModalWrapper>

  </div>
</template>
