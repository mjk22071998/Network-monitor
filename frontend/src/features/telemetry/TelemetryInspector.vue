<script setup lang="ts">
import { computed, ref } from 'vue';
import { useDevicesStore } from '../../stores/devices';
import StatusBadge from '../../components/common/StatusBadge.vue';
import { 
  X, 
  Activity, 
  Server, 
  HardDrive, 
  Cpu, 
  Wrench, 
  Edit3, 
  Trash2
} from 'lucide-vue-next';

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'edit', device: any): void;
  (e: 'delete', device: any): void;
}>();

const devicesStore = useDevicesStore();

const isTogglingMaintenance = ref(false);

const device = computed(() => devicesStore.selectedDevice);

// Format latency: µs to ms with 2 decimals if > 1000 µs
function formatLatency(us: number | null | undefined): string {
  if (us === null || us === undefined) return 'N/A';
  if (us < 1000) return `${us} µs`;
  return `${(us / 1000).toFixed(2)} ms`;
}

// Format NTP offset
function formatNtp(us: number | null | undefined): string {
  if (us === null || us === undefined) return 'N/A';
  return `${us > 0 ? '+' : ''}${us} µs`;
}

// Format Packet Loss
function formatLoss(pct: number | null | undefined): string {
  if (pct === null || pct === undefined) return 'N/A';
  return `${pct.toFixed(1)}%`;
}

// Format Bytes to GB
function formatBytesToGb(bytes: number): string {
  return (bytes / (1024 * 1024 * 1024)).toFixed(1) + ' GB';
}

async function handleToggleMaintenance() {
  if (!device.value) return;
  isTogglingMaintenance.value = true;
  try {
    const nextState = !device.value.is_in_maintenance;
    await devicesStore.toggleMaintenance(device.value.device_id, {
      is_in_maintenance: nextState,
      reason: nextState ? 'Operator initiated diagnostic freeze' : undefined
    });
  } finally {
    isTogglingMaintenance.value = false;
  }
}
</script>

<template>
  <aside 
    v-if="device"
    class="fixed inset-y-0 right-0 z-40 w-96 bg-[#070b16] border-l border-tactical-border shadow-2xl flex flex-col font-mono text-xs select-none animate-in slide-in-from-right duration-200"
  >
    <!-- Header -->
    <div class="h-14 border-b border-tactical-border bg-tactical-surface px-5 flex items-center justify-between">
      <div class="flex items-center space-x-2">
        <Server class="w-4 h-4 text-tactical-cyan" />
        <div>
          <h2 class="text-sm font-bold tracking-wider text-slate-100 truncate max-w-[200px]">
            {{ device.display_name }}
          </h2>
          <p class="text-[10px] text-slate-500 font-sans">Telemetry & Host Diagnostics</p>
        </div>
      </div>

      <div class="flex items-center space-x-2">
        <button 
          @click="emit('close')"
          class="p-1 rounded text-slate-400 hover:text-white hover:bg-slate-800 transition"
          title="Close Inspector"
        >
          <X class="w-4 h-4" />
        </button>
      </div>
    </div>

    <!-- Scrollable Content -->
    <div class="flex-1 overflow-y-auto p-5 space-y-5">
      
      <!-- Maintenance Banner (if active) -->
      <div 
        v-if="device.is_in_maintenance"
        class="p-3 rounded-lg bg-purple-950/50 border border-purple-500/50 text-purple-300 flex items-start space-x-2"
      >
        <Wrench class="w-4 h-4 flex-shrink-0 mt-0.5 text-purple-400" />
        <div class="text-[11px] leading-relaxed">
          <strong class="block text-purple-200 font-bold">MAINTENANCE MODE ACTIVE</strong>
          Synthetic polling suppressed. {{ device.maintenance_reason || 'Under engineer diagnostics.' }}
        </div>
      </div>

      <!-- Quick Identity Card -->
      <div class="p-3.5 rounded-lg bg-tactical-panel border border-tactical-border space-y-2">
        <div class="flex items-center justify-between">
          <span class="text-slate-400 text-[10px] uppercase font-bold">Network Status:</span>
          <StatusBadge :status="device.reachability_status" :is-maintenance="device.is_in_maintenance" />
        </div>

        <div class="grid grid-cols-2 gap-2 pt-2 border-t border-tactical-border/60 text-[11px]">
          <div>
            <span class="text-slate-500 block text-[9px] uppercase">IPv4 Address</span>
            <span class="text-tactical-cyan font-bold">{{ device.primary_ip }}</span>
          </div>
          <div>
            <span class="text-slate-500 block text-[9px] uppercase">MAC Address</span>
            <span class="text-slate-200">{{ device.mac_address }}</span>
          </div>
          <div>
            <span class="text-slate-500 block text-[9px] uppercase">Device Type</span>
            <span class="text-slate-200">{{ device.type_code }}</span>
          </div>
          <div>
            <span class="text-slate-500 block text-[9px] uppercase">Lab / Group</span>
            <span class="text-slate-200 truncate block">{{ device.group_name || 'Group #1' }}</span>
          </div>
          <div>
            <span class="text-slate-500 block text-[9px] uppercase">Vendor / Model</span>
            <span class="text-slate-300 truncate block">{{ device.vendor || 'Unknown' }} {{ device.model || '' }}</span>
          </div>
          <div>
            <span class="text-slate-500 block text-[9px] uppercase">Zone</span>
            <span class="text-slate-300 truncate block">{{ device.zone || 'N/A' }}</span>
          </div>
        </div>
      </div>

      <!-- Network Reachability Telemetry (ICMP / NTP) -->
      <div class="p-3.5 rounded-lg bg-tactical-panel border border-tactical-border space-y-3">
        <div class="flex items-center justify-between border-b border-tactical-border/60 pb-2">
          <span class="text-slate-300 font-bold flex items-center gap-1.5">
            <Activity class="w-3.5 h-3.5 text-tactical-cyan" />
            <span>LAN Network Telemetry</span>
          </span>
          <span class="text-[9px] text-slate-500">5s ICMP Echo</span>
        </div>

        <div class="grid grid-cols-3 gap-2 text-center">
          <div class="p-2 rounded bg-tactical-surface border border-tactical-border">
            <span class="text-[9px] text-slate-500 block uppercase">Round-Trip RTT</span>
            <span 
              class="text-xs font-bold"
              :class="device.latest_latency_us !== null && device.latest_latency_us !== undefined ? 'text-tactical-cyan' : 'text-slate-500'"
            >
              {{ formatLatency(device.latest_latency_us) }}
            </span>
          </div>

          <div class="p-2 rounded bg-tactical-surface border border-tactical-border">
            <span class="text-[9px] text-slate-500 block uppercase">Packet Loss</span>
            <span 
              class="text-xs font-bold"
              :class="(device.latest_loss_pct ?? 0) > 0 ? 'text-tactical-alert' : 'text-tactical-online'"
            >
              {{ formatLoss(device.latest_loss_pct) }}
            </span>
          </div>

          <div class="p-2 rounded bg-tactical-surface border border-tactical-border">
            <span class="text-[9px] text-slate-500 block uppercase">NTP Drift</span>
            <span 
              class="text-xs font-bold"
              :class="device.latest_ntp_offset_us !== null && device.latest_ntp_offset_us !== undefined ? 'text-tactical-amber' : 'text-slate-500'"
            >
              {{ formatNtp(device.latest_ntp_offset_us) }}
            </span>
          </div>
        </div>

        <!-- Latency Trend Sparkline (History) -->
        <div v-if="device.reachability_history && device.reachability_history.length > 1" class="pt-2">
          <div class="flex justify-between text-[9px] text-slate-500 mb-1">
            <span>Latency Trend (Last 10 Samples)</span>
            <span>Microseconds</span>
          </div>
          <div class="h-10 w-full flex items-end gap-1 bg-tactical-surface rounded p-1 border border-tactical-border">
            <div 
              v-for="(sample, idx) in device.reachability_history.slice(-10)"
              :key="idx"
              class="flex-1 bg-tactical-cyan/40 hover:bg-tactical-cyan transition rounded-t"
              :style="{ height: `${Math.min(100, Math.max(15, (sample.latency_us ?? 0) / 50))}%` }"
              :title="`Time: ${sample.recorded_at} | RTT: ${formatLatency(sample.latency_us)}`"
            ></div>
          </div>
        </div>
      </div>

      <!-- Host Health Diagnostics (CPU, RAM, Disks) -->
      <div class="p-3.5 rounded-lg bg-tactical-panel border border-tactical-border space-y-3">
        <div class="flex items-center justify-between border-b border-tactical-border/60 pb-2">
          <span class="text-slate-300 font-bold flex items-center gap-1.5">
            <Cpu class="w-3.5 h-3.5 text-tactical-amber" />
            <span>Host Resource Telemetry</span>
          </span>
          <span class="text-[9px] text-slate-500">30s Interval</span>
        </div>

        <!-- CPU Bar -->
        <div>
          <div class="flex justify-between text-[10px] mb-1">
            <span class="text-slate-400">CPU Core Utilization</span>
            <span 
              class="font-bold"
              :class="(device.latest_cpu_pct ?? 0) >= 80 ? 'text-tactical-alert' : 'text-tactical-cyan'"
            >
              {{ device.latest_cpu_pct !== null && device.latest_cpu_pct !== undefined ? `${device.latest_cpu_pct.toFixed(1)}%` : 'N/A' }}
            </span>
          </div>
          <div class="h-2 w-full bg-tactical-surface rounded overflow-hidden border border-tactical-border">
            <div 
              class="h-full transition-all duration-500"
              :class="(device.latest_cpu_pct ?? 0) >= 80 ? 'bg-tactical-alert' : 'bg-tactical-cyan'"
              :style="{ width: `${device.latest_cpu_pct ?? 0}%` }"
            ></div>
          </div>
        </div>

        <!-- RAM Bar -->
        <div>
          <div class="flex justify-between text-[10px] mb-1">
            <span class="text-slate-400">RAM System Memory</span>
            <span 
              class="font-bold"
              :class="(device.latest_ram_pct ?? 0) >= 80 ? 'text-tactical-alert' : 'text-tactical-online'"
            >
              {{ device.latest_ram_pct !== null && device.latest_ram_pct !== undefined ? `${device.latest_ram_pct.toFixed(1)}%` : 'N/A' }}
            </span>
          </div>
          <div class="h-2 w-full bg-tactical-surface rounded overflow-hidden border border-tactical-border">
            <div 
              class="h-full transition-all duration-500"
              :class="(device.latest_ram_pct ?? 0) >= 80 ? 'bg-tactical-alert' : 'bg-tactical-online'"
              :style="{ width: `${device.latest_ram_pct ?? 0}%` }"
            ></div>
          </div>
        </div>

        <!-- Multi-Volume Disk Partitions (C:, D:, /) -->
        <div v-if="device.disk_volumes && device.disk_volumes.length > 0" class="pt-2 border-t border-tactical-border/60 space-y-2">
          <div class="text-[9px] uppercase tracking-wider text-slate-500 flex items-center gap-1">
            <HardDrive class="w-3 h-3 text-slate-400" />
            <span>Partition Storage Volumes</span>
          </div>

          <div 
            v-for="vol in device.disk_volumes"
            :key="vol.partition_name"
            class="space-y-1"
          >
            <div class="flex justify-between text-[10px]">
              <span class="text-slate-300 font-bold">{{ vol.partition_name }}</span>
              <span class="text-slate-400">
                {{ vol.used_pct.toFixed(0) }}% ({{ formatBytesToGb(vol.free_bytes) }} free of {{ formatBytesToGb(vol.total_bytes) }})
              </span>
            </div>
            <div class="h-1.5 w-full bg-tactical-surface rounded overflow-hidden border border-tactical-border">
              <div 
                class="h-full transition-all"
                :class="vol.used_pct >= 90 ? 'bg-tactical-alert' : (vol.used_pct >= 75 ? 'bg-tactical-amber' : 'bg-tactical-cyan')"
                :style="{ width: `${vol.used_pct}%` }"
              ></div>
            </div>
          </div>
        </div>
      </div>

    </div>

    <!-- Footer Action Bar -->
    <div class="h-16 border-t border-tactical-border bg-tactical-surface px-5 flex items-center justify-between gap-2">
      <!-- Maintenance Mode Toggle -->
      <button
        @click="handleToggleMaintenance"
        :disabled="isTogglingMaintenance"
        class="flex-1 py-2 px-3 rounded text-[11px] font-bold border transition flex items-center justify-center space-x-1.5"
        :class="device.is_in_maintenance 
          ? 'bg-purple-950/60 border-purple-500 text-purple-300 hover:bg-purple-900/80' 
          : 'bg-tactical-panel border-tactical-border text-slate-300 hover:border-purple-500 hover:text-purple-300'"
      >
        <Wrench class="w-3.5 h-3.5" :class="{ 'animate-spin': isTogglingMaintenance }" />
        <span>{{ device.is_in_maintenance ? 'Exit Maintenance' : 'Set Maintenance' }}</span>
      </button>

      <!-- Edit Device -->
      <button
        @click="emit('edit', device)"
        class="p-2 rounded bg-tactical-panel border border-tactical-border text-slate-300 hover:border-tactical-cyan hover:text-tactical-cyan transition"
        title="Edit Device Configuration"
      >
        <Edit3 class="w-4 h-4" />
      </button>

      <!-- Delete Device -->
      <button
        @click="emit('delete', device)"
        class="p-2 rounded bg-tactical-panel border border-tactical-border text-slate-300 hover:border-rose-500 hover:text-rose-400 transition"
        title="Delete Device (Orphan-Safe)"
      >
        <Trash2 class="w-4 h-4" />
      </button>
    </div>
  </aside>
</template>
