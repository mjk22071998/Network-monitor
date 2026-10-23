<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useDiscoveryStore } from '../../stores/discovery';
import { 
  Radar, 
  Play, 
  Square, 
  Plus, 
  AlertCircle 
} from 'lucide-vue-next';
import type { DiscoveredHost } from '../../types/discovery';

const discoveryStore = useDiscoveryStore();

const emit = defineEmits<{
  (e: 'importHost', host: DiscoveredHost): void;
}>();

const cidrInput = ref('192.168.1.0/24');
const timeoutMs = ref(1000);

onMounted(async () => {
  await discoveryStore.checkActiveScan();
});

async function handleStartScan() {
  if (!cidrInput.value.trim()) return;
  await discoveryStore.startScan(cidrInput.value.trim(), timeoutMs.value);
}

async function handleCancelScan() {
  await discoveryStore.cancelScan();
}
</script>

<template>
  <div class="h-full flex flex-col bg-tactical-surface rounded-xl border border-tactical-border overflow-hidden select-none font-mono">
    
    <!-- Top Configuration Header -->
    <div class="p-4 border-b border-tactical-border bg-tactical-surface flex flex-wrap items-center justify-between gap-4">
      <div>
        <h2 class="text-sm font-bold uppercase tracking-wider text-slate-100 flex items-center gap-2">
          <Radar class="w-4 h-4 text-tactical-cyan" :class="{ 'animate-spin': discoveryStore.isScanning }" />
          <span>Autonomous Subnet Sweep & Host Discovery</span>
        </h2>
        <p class="text-[10px] text-slate-500 font-sans">
          Concurrent ICMP echo & ARP discovery across university lab IP subnets (/24).
        </p>
      </div>

      <!-- Controls -->
      <div class="flex items-center space-x-3 text-xs">
        <!-- CIDR Input -->
        <div class="flex items-center space-x-1.5">
          <span class="text-slate-400 uppercase text-[10px] font-bold">Subnet CIDR:</span>
          <input
            v-model="cidrInput"
            type="text"
            placeholder="192.168.1.0/24"
            :disabled="discoveryStore.isScanning"
            class="w-36 bg-tactical-panel border border-tactical-border rounded px-2.5 py-1.5 text-slate-200 focus:outline-none focus:border-tactical-cyan text-xs disabled:opacity-50"
          />
        </div>

        <!-- Timeout Slider -->
        <div class="flex items-center space-x-2">
          <span class="text-slate-400 uppercase text-[10px] font-bold">Timeout:</span>
          <input
            v-model.number="timeoutMs"
            type="range"
            min="200"
            max="3000"
            step="100"
            :disabled="discoveryStore.isScanning"
            class="w-24 accent-tactical-cyan cursor-pointer disabled:opacity-50"
          />
          <span class="text-tactical-cyan text-[11px] w-12">{{ timeoutMs }}ms</span>
        </div>

        <!-- Scan Button -->
        <button
          v-if="!discoveryStore.isScanning"
          @click="handleStartScan"
          class="flex items-center space-x-1.5 px-3.5 py-1.5 rounded-lg bg-tactical-cyan hover:bg-cyan-400 text-slate-950 font-bold transition shadow-[0_0_12px_rgba(0,240,255,0.25)]"
        >
          <Play class="w-3.5 h-3.5 fill-current" />
          <span>Launch Sweep</span>
        </button>

        <button
          v-else
          @click="handleCancelScan"
          class="flex items-center space-x-1.5 px-3.5 py-1.5 rounded-lg bg-rose-600 hover:bg-rose-500 text-white font-bold transition shadow-lg shadow-rose-600/30"
        >
          <Square class="w-3.5 h-3.5 fill-current" />
          <span>Abort Sweep</span>
        </button>
      </div>
    </div>

    <!-- Live Progress Bar (if active) -->
    <div 
      v-if="discoveryStore.currentScan && discoveryStore.currentScan.status === 'IN_PROGRESS'"
      class="bg-tactical-panel p-3 border-b border-tactical-border text-xs space-y-1.5"
    >
      <div class="flex justify-between text-slate-300">
        <span class="flex items-center gap-1.5 text-tactical-cyan font-bold">
          <span class="w-2 h-2 rounded-full bg-tactical-cyan animate-ping"></span>
          <span>Sweeping {{ discoveryStore.currentScan.cidr }}...</span>
        </span>
        <span class="text-slate-400">
          {{ discoveryStore.currentScan.scanned_hosts }} / {{ discoveryStore.currentScan.total_targets }} Targets ({{ Math.round((discoveryStore.currentScan.scanned_hosts / (discoveryStore.currentScan.total_targets || 1)) * 100) }}%)
        </span>
      </div>
      <div class="h-2 w-full bg-tactical-surface rounded overflow-hidden border border-tactical-border">
        <div 
          class="h-full bg-tactical-cyan transition-all duration-300"
          :style="{ width: `${(discoveryStore.currentScan.scanned_hosts / (discoveryStore.currentScan.total_targets || 1)) * 100}%` }"
        ></div>
      </div>
    </div>

    <!-- Error Banner -->
    <div v-if="discoveryStore.error" class="m-4 p-3 rounded bg-rose-950/40 border border-rose-500/50 text-rose-300 text-xs flex items-center space-x-2">
      <AlertCircle class="w-4 h-4 flex-shrink-0" />
      <span>{{ discoveryStore.error }}</span>
    </div>

    <!-- Discovered Hosts Table -->
    <div class="flex-1 overflow-auto">
      <table class="w-full text-left text-xs border-collapse">
        <thead class="sticky top-0 bg-tactical-panel border-b border-tactical-border text-slate-400 text-[10px] uppercase tracking-wider font-bold z-10">
          <tr>
            <th class="py-3 px-4">IPv4 Address</th>
            <th class="py-3 px-4">MAC Address</th>
            <th class="py-3 px-4">Vendor Resolution</th>
            <th class="py-3 px-4">Latency (RTT)</th>
            <th class="py-3 px-4">Status</th>
            <th class="py-3 px-4 text-right">Inventory Action</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-tactical-border/50 text-slate-300">
          <tr 
            v-for="host in discoveryStore.currentScan?.hosts || []"
            :key="host.ip_address"
            class="hover:bg-slate-900/60 transition"
          >
            <!-- IP -->
            <td class="py-2.5 px-4 font-bold text-tactical-cyan">
              {{ host.ip_address }}
            </td>

            <!-- MAC -->
            <td class="py-2.5 px-4 text-slate-400">
              {{ host.mac_address || 'Unresolved ARP' }}
            </td>

            <!-- Vendor -->
            <td class="py-2.5 px-4 text-slate-300">
              {{ host.vendor || 'Generic Network Card' }}
            </td>

            <!-- Latency -->
            <td class="py-2.5 px-4 text-tactical-online font-bold">
              {{ host.response_time_ms.toFixed(1) }} ms
            </td>

            <!-- Status -->
            <td class="py-2.5 px-4">
              <span 
                v-if="host.is_already_inventoried"
                class="px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-400 border border-slate-700 font-bold"
              >
                ALREADY INVENTORIED
              </span>
              <span 
                v-else
                class="px-2 py-0.5 rounded text-[10px] bg-emerald-950/60 text-emerald-300 border border-emerald-500/40 font-bold"
              >
                NEW CANDIDATE
              </span>
            </td>

            <!-- Actions -->
            <td class="py-2.5 px-4 text-right">
              <button
                v-if="!host.is_already_inventoried"
                @click="emit('importHost', host)"
                class="inline-flex items-center space-x-1 px-2.5 py-1 rounded bg-tactical-panel border border-tactical-border hover:border-tactical-cyan text-slate-300 hover:text-tactical-cyan text-[11px] transition"
              >
                <Plus class="w-3.5 h-3.5" />
                <span>Import</span>
              </button>
              <span v-else class="text-[10px] text-slate-600">Synced</span>
            </td>
          </tr>

          <!-- Empty State -->
          <tr v-if="!discoveryStore.currentScan || discoveryStore.currentScan.hosts.length === 0">
            <td colspan="6" class="py-12 text-center text-slate-500">
              <Radar class="w-8 h-8 mx-auto mb-2 text-slate-600" />
              <p>No active scan results. Enter a subnet CIDR above and click "Launch Sweep".</p>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

  </div>
</template>
