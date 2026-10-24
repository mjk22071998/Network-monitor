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
  if (discoveryStore.currentScan?.cidr) {
    cidrInput.value = discoveryStore.currentScan.cidr;
  }
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
  <div class="h-full flex flex-col bg-zinc-950 rounded-xl border border-zinc-800 overflow-hidden select-none font-sans shadow-sm">
    
    <!-- Top Configuration Header -->
    <div class="p-4 border-b border-zinc-800 bg-zinc-900/60 flex flex-wrap items-center justify-between gap-4">
      <div>
        <div class="flex items-center gap-2">
          <Radar class="w-4 h-4 text-emerald-400" :class="{ 'animate-spin': discoveryStore.isScanning }" />
          <h2 class="text-sm font-semibold text-zinc-100">Subnet Discovery</h2>
          <span 
            v-if="discoveryStore.currentScan?.hosts?.length" 
            class="text-[10px] font-mono px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/30"
          >
            {{ discoveryStore.currentScan.hosts.length }} hosts discovered
          </span>
        </div>
        <p class="text-xs text-zinc-400 mt-0.5">
          Asynchronous ICMP echo sweep and ARP table resolution across local computer lab subnets.
        </p>
      </div>

      <!-- Controls -->
      <div class="flex items-center space-x-3 text-xs">
        <!-- CIDR Input -->
        <div class="flex items-center space-x-1.5">
          <span class="text-zinc-400 text-xs font-medium">Subnet:</span>
          <input
            v-model="cidrInput"
            type="text"
            placeholder="192.168.1.0/24"
            :disabled="discoveryStore.isScanning"
            class="w-36 bg-zinc-900 border border-zinc-700 rounded-md px-2.5 py-1.5 text-zinc-200 placeholder-zinc-500 focus:outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 text-xs disabled:opacity-50 font-mono"
          />
        </div>

        <!-- Timeout Slider -->
        <div class="flex items-center space-x-2">
          <span class="text-zinc-400 text-xs font-medium">Timeout:</span>
          <input
            v-model.number="timeoutMs"
            type="range"
            min="200"
            max="3000"
            step="100"
            :disabled="discoveryStore.isScanning"
            class="w-20 accent-emerald-500 cursor-pointer disabled:opacity-50"
          />
          <span class="text-emerald-400 font-mono text-xs w-12">{{ timeoutMs }}ms</span>
        </div>

        <!-- Scan Button -->
        <button
          v-if="!discoveryStore.isScanning"
          @click="handleStartScan"
          class="flex items-center space-x-1.5 px-3 py-1.5 rounded-md bg-emerald-600 hover:bg-emerald-500 text-white font-medium text-xs transition shadow-sm"
        >
          <Play class="w-3.5 h-3.5 fill-current" />
          <span>Launch Sweep</span>
        </button>

        <button
          v-else
          @click="handleCancelScan"
          class="flex items-center space-x-1.5 px-3 py-1.5 rounded-md bg-rose-600 hover:bg-rose-500 text-white font-medium text-xs transition shadow-sm"
        >
          <Square class="w-3.5 h-3.5 fill-current" />
          <span>Abort Sweep</span>
        </button>
      </div>
    </div>

    <!-- Live Progress Bar (if active) -->
    <div 
      v-if="discoveryStore.currentScan && (discoveryStore.currentScan.status === 'IN_PROGRESS' || discoveryStore.currentScan.status === 'RUNNING')"
      class="bg-zinc-900/80 p-3 border-b border-zinc-800 text-xs space-y-1.5"
    >
      <div class="flex justify-between text-zinc-300">
        <span class="flex items-center gap-2 text-emerald-400 font-medium">
          <span class="w-2 h-2 rounded-full bg-emerald-400 animate-ping"></span>
          <span>Sweeping {{ discoveryStore.currentScan.cidr }}...</span>
        </span>
        <span class="text-zinc-400 font-mono">
          {{ discoveryStore.currentScan.scanned_hosts }} / {{ discoveryStore.currentScan.total_targets }} Targets ({{ discoveryStore.currentScan.progress_pct || Math.round((discoveryStore.currentScan.scanned_hosts / (discoveryStore.currentScan.total_targets || 1)) * 100) }}%)
        </span>
      </div>
      <div class="h-2 w-full bg-zinc-800 rounded-full overflow-hidden">
        <div 
          class="h-full bg-emerald-500 transition-all duration-300"
          :style="{ width: `${discoveryStore.currentScan.progress_pct || ((discoveryStore.currentScan.scanned_hosts / (discoveryStore.currentScan.total_targets || 1)) * 100)}%` }"
        ></div>
      </div>
    </div>

    <!-- Error Banner -->
    <div v-if="discoveryStore.error" class="m-4 p-3 rounded-md bg-rose-950/40 border border-rose-500/40 text-rose-300 text-xs flex items-center space-x-2">
      <AlertCircle class="w-4 h-4 flex-shrink-0" />
      <span>{{ discoveryStore.error }}</span>
    </div>

    <!-- Discovered Hosts Table -->
    <div class="flex-1 overflow-auto">
      <table class="w-full text-left text-xs border-collapse font-sans">
        <thead class="sticky top-0 bg-zinc-900/90 border-b border-zinc-800 text-zinc-400 text-[11px] font-medium z-10">
          <tr>
            <th class="py-3 px-4">IPv4 Address</th>
            <th class="py-3 px-4">MAC Address</th>
            <th class="py-3 px-4">Vendor</th>
            <th class="py-3 px-4">Type</th>
            <th class="py-3 px-4">Latency</th>
            <th class="py-3 px-4">Inventory Status</th>
            <th class="py-3 px-4 text-right">Action</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-zinc-800/60 text-zinc-300">
          <tr 
            v-for="host in discoveryStore.currentScan?.hosts || []"
            :key="host.ip_address"
            class="hover:bg-zinc-900/50 transition"
          >
            <!-- IP -->
            <td class="py-2.5 px-4 font-mono font-medium text-emerald-400">
              {{ host.ip_address }}
            </td>

            <!-- MAC -->
            <td class="py-2.5 px-4 font-mono text-zinc-400">
              {{ host.mac_address || 'Unresolved' }}
            </td>

            <!-- Vendor -->
            <td class="py-2.5 px-4 text-zinc-300">
              {{ host.vendor || 'Generic' }}
            </td>

            <!-- Inferred Type -->
            <td class="py-2.5 px-4">
              <span class="px-2 py-0.5 rounded text-[10px] bg-zinc-800 text-zinc-300 font-mono border border-zinc-700">
                {{ host.inferred_type || 'PC' }}
              </span>
            </td>

            <!-- Latency -->
            <td class="py-2.5 px-4 font-mono">
              <span v-if="host.response_time_ms !== null && host.response_time_ms !== undefined" class="text-emerald-400 font-medium">
                {{ host.response_time_ms.toFixed(1) }} ms
              </span>
              <span v-else class="text-zinc-500 italic">
                N/A
              </span>
            </td>

            <!-- Status -->
            <td class="py-2.5 px-4">
              <span 
                v-if="host.is_already_inventoried"
                class="px-2 py-0.5 rounded text-[10px] bg-zinc-800 text-zinc-400 border border-zinc-700 font-medium"
              >
                ALREADY INVENTORIED
              </span>
              <span 
                v-else
                class="px-2 py-0.5 rounded text-[10px] bg-emerald-950/60 text-emerald-300 border border-emerald-500/40 font-medium"
              >
                NEW CANDIDATE
              </span>
            </td>

            <!-- Actions -->
            <td class="py-2.5 px-4 text-right">
              <button
                v-if="!host.is_already_inventoried"
                @click="emit('importHost', host)"
                class="inline-flex items-center space-x-1 px-2.5 py-1 rounded-md bg-zinc-800 border border-zinc-700 hover:border-emerald-500 text-zinc-200 hover:text-emerald-400 text-xs transition"
              >
                <Plus class="w-3.5 h-3.5" />
                <span>Import</span>
              </button>
              <span v-else class="text-[11px] text-zinc-500">Synced</span>
            </td>
          </tr>

          <!-- Empty State: No Scan Performed Yet -->
          <tr v-if="!discoveryStore.currentScan">
            <td colspan="7" class="py-16 text-center text-zinc-400">
              <Radar class="w-8 h-8 mx-auto mb-2 text-zinc-400" />
              <p class="text-xs font-medium text-zinc-300">No active discovery sweep</p>
              <p class="text-[11px] text-zinc-400 mt-1">Enter a subnet CIDR above and click "Launch Sweep" to begin live device discovery.</p>
            </td>
          </tr>

          <!-- Empty State: Scan Completed with 0 Hosts -->
          <tr v-else-if="discoveryStore.currentScan.hosts.length === 0">
            <td colspan="7" class="py-16 text-center text-zinc-400">
              <AlertCircle class="w-8 h-8 mx-auto mb-2 text-amber-400" />
              <p class="text-xs font-medium text-zinc-300">Sweep Completed Across {{ discoveryStore.currentScan.cidr }}</p>
              <p class="text-[11px] text-zinc-400 mt-1">No responding hosts discovered on this subnet segment.</p>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

  </div>
</template>
