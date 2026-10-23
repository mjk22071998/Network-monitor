<script setup lang="ts">
import { useDevicesStore } from '../../stores/devices';
import StatusBadge from '../../components/common/StatusBadge.vue';
import PaginationFooter from '../../components/common/PaginationFooter.vue';
import { 
  Plus, 
  Search, 
  Activity, 
  Edit3, 
  Trash2, 
  Server, 
  Monitor, 
  Radio, 
  Layers 
} from 'lucide-vue-next';
import type { Device } from '../../types/device';

const devicesStore = useDevicesStore();

const emit = defineEmits<{
  (e: 'inspect', deviceId: number): void;
  (e: 'create'): void;
  (e: 'edit', device: Device): void;
  (e: 'delete', device: Device): void;
}>();

function formatCpu(cpu?: number | null): string {
  if (cpu === null || cpu === undefined) return 'N/A';
  return `${cpu.toFixed(1)}%`;
}

function formatRam(ram?: number | null): string {
  if (ram === null || ram === undefined) return 'N/A';
  return `${ram.toFixed(1)}%`;
}

function getTypeIcon(typeId: number) {
  switch (typeId) {
    case 1: return Radio;
    case 2: return Layers;
    case 3: return Server;
    case 4: return Server;
    case 5: default: return Monitor;
  }
}
</script>

<template>
  <div class="h-full flex flex-col bg-tactical-surface rounded-xl border border-tactical-border overflow-hidden select-none font-mono">
    
    <!-- Top Action & Filter Toolbar -->
    <div class="p-4 border-b border-tactical-border bg-tactical-surface flex flex-wrap items-center justify-between gap-3 text-xs">
      
      <!-- Search Input -->
      <div class="relative flex-1 min-w-[240px] max-w-md">
        <Search class="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
        <input
          v-model="devicesStore.searchQuery"
          type="text"
          placeholder="Filter by Name, IPv4, MAC, or Vendor..."
          class="w-full bg-tactical-panel border border-tactical-border rounded-lg pl-9 pr-3 py-2 text-slate-200 placeholder-slate-500 focus:outline-none focus:border-tactical-cyan"
        />
      </div>

      <!-- Filters & Add Device -->
      <div class="flex items-center space-x-3">
        <!-- Type Filter -->
        <div class="flex items-center space-x-1.5">
          <span class="text-slate-500 uppercase text-[10px]">Type:</span>
          <select
            v-model="devicesStore.selectedTypeFilter"
            class="bg-tactical-panel border border-tactical-border rounded px-2.5 py-1.5 text-slate-300 focus:outline-none focus:border-tactical-cyan text-xs"
          >
            <option value="ALL">All Types</option>
            <option value="CORE_ROUTER">Core Router</option>
            <option value="AGGREGATE_SWITCH">Distribution Switch</option>
            <option value="ACCESS_SWITCH">Access Switch</option>
            <option value="SERVER">Server</option>
            <option value="PC">Workstation / PC</option>
          </select>
        </div>

        <!-- Status Filter -->
        <div class="flex items-center space-x-1.5">
          <span class="text-slate-500 uppercase text-[10px]">Status:</span>
          <select
            v-model="devicesStore.selectedStatusFilter"
            class="bg-tactical-panel border border-tactical-border rounded px-2.5 py-1.5 text-slate-300 focus:outline-none focus:border-tactical-cyan text-xs"
          >
            <option value="ALL">All States</option>
            <option value="REACHABLE">Reachable (Online)</option>
            <option value="UNREACHABLE">Unreachable (Outage)</option>
            <option value="SUPPRESSED">Suppressed</option>
            <option value="MAINTENANCE">Maintenance</option>
          </select>
        </div>

        <!-- Add Device Button -->
        <button
          @click="emit('create')"
          class="flex items-center space-x-1.5 px-3.5 py-1.5 rounded-lg bg-tactical-cyan hover:bg-cyan-400 text-slate-950 font-bold transition shadow-[0_0_12px_rgba(0,240,255,0.25)]"
        >
          <Plus class="w-4 h-4" />
          <span>Add Device</span>
        </button>
      </div>

    </div>

    <!-- Tabular Grid -->
    <div class="flex-1 overflow-auto">
      <table class="w-full text-left text-xs border-collapse">
        <thead class="sticky top-0 bg-tactical-panel border-b border-tactical-border text-slate-400 text-[10px] uppercase tracking-wider font-bold z-10">
          <tr>
            <th class="py-3 px-4">Status</th>
            <th class="py-3 px-4">Device Identity</th>
            <th class="py-3 px-4">Primary IPv4</th>
            <th class="py-3 px-4">MAC Address</th>
            <th class="py-3 px-4">Type</th>
            <th class="py-3 px-4">Lab / Group</th>
            <th class="py-3 px-4">CPU %</th>
            <th class="py-3 px-4">RAM %</th>
            <th class="py-3 px-4 text-right">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-tactical-border/50 text-slate-300">
          <tr 
            v-for="d in devicesStore.paginatedDevices" 
            :key="d.device_id"
            class="hover:bg-slate-900/60 transition group cursor-pointer"
            @click="emit('inspect', d.device_id)"
          >
            <!-- Status Badge -->
            <td class="py-2.5 px-4 whitespace-nowrap">
              <StatusBadge :status="d.reachability_status" :is-maintenance="d.is_in_maintenance" />
            </td>

            <!-- Identity -->
            <td class="py-2.5 px-4">
              <div class="flex items-center space-x-2">
                <component :is="getTypeIcon(d.type_id)" class="w-4 h-4 text-slate-400 group-hover:text-tactical-cyan transition" />
                <div>
                  <span class="font-bold text-slate-100 group-hover:text-tactical-cyan transition block">
                    {{ d.display_name }}
                  </span>
                  <span class="text-[10px] text-slate-500 font-sans block">
                    {{ d.vendor || 'Unknown Vendor' }} {{ d.model || '' }}
                  </span>
                </div>
              </div>
            </td>

            <!-- IPv4 -->
            <td class="py-2.5 px-4 font-mono font-semibold text-tactical-cyan">
              {{ d.primary_ip }}
            </td>

            <!-- MAC -->
            <td class="py-2.5 px-4 font-mono text-slate-400 text-[11px]">
              {{ d.mac_address }}
            </td>

            <!-- Type -->
            <td class="py-2.5 px-4 text-[11px]">
              <span class="px-1.5 py-0.5 rounded bg-tactical-panel border border-tactical-border text-slate-300 text-[10px]">
                {{ d.type_code }}
              </span>
            </td>

            <!-- Lab Group -->
            <td class="py-2.5 px-4 text-[11px] text-slate-400 truncate max-w-[140px]">
              {{ d.group_name || 'Unassigned' }}
            </td>

            <!-- CPU % -->
            <td class="py-2.5 px-4 text-[11px]">
              <span 
                :class="(d.latest_cpu_pct ?? 0) >= 80 ? 'text-tactical-alert font-bold' : 'text-slate-400'"
              >
                {{ formatCpu(d.latest_cpu_pct) }}
              </span>
            </td>

            <!-- RAM % -->
            <td class="py-2.5 px-4 text-[11px]">
              <span 
                :class="(d.latest_ram_pct ?? 0) >= 80 ? 'text-tactical-alert font-bold' : 'text-slate-400'"
              >
                {{ formatRam(d.latest_ram_pct) }}
              </span>
            </td>

            <!-- Actions -->
            <td class="py-2.5 px-4 text-right whitespace-nowrap" @click.stop>
              <div class="inline-flex items-center space-x-1.5">
                <button
                  @click="emit('inspect', d.device_id)"
                  class="p-1 rounded bg-tactical-panel border border-tactical-border text-slate-400 hover:text-tactical-cyan hover:border-tactical-cyan transition"
                  title="Inspect Live Telemetry"
                >
                  <Activity class="w-3.5 h-3.5" />
                </button>

                <button
                  @click="emit('edit', d)"
                  class="p-1 rounded bg-tactical-panel border border-tactical-border text-slate-400 hover:text-tactical-amber hover:border-tactical-amber transition"
                  title="Edit Device"
                >
                  <Edit3 class="w-3.5 h-3.5" />
                </button>

                <button
                  @click="emit('delete', d)"
                  class="p-1 rounded bg-tactical-panel border border-tactical-border text-slate-400 hover:text-rose-400 hover:border-rose-400 transition"
                  title="Orphan-Safe Deletion"
                >
                  <Trash2 class="w-3.5 h-3.5" />
                </button>
              </div>
            </td>
          </tr>

          <!-- Empty State -->
          <tr v-if="devicesStore.paginatedDevices.length === 0">
            <td colspan="9" class="py-12 text-center text-slate-500">
              <Server class="w-8 h-8 mx-auto mb-2 text-slate-600" />
              <p>No devices matching criteria.</p>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Standardized Pagination Footer -->
    <PaginationFooter
      :current-page="devicesStore.currentPage"
      :page-size="devicesStore.pageSize"
      :total-records="devicesStore.filteredDevices.length"
      @update:page="(p) => devicesStore.currentPage = p"
      @update:page-size="(s) => devicesStore.pageSize = s"
    />

  </div>
</template>
