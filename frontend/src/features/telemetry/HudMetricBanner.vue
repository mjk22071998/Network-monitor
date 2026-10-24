<script setup lang="ts">
import { useDevicesStore } from '../../stores/devices';
import { 
  CheckCircle2, 
  AlertCircle, 
  ShieldAlert, 
  Cpu, 
  HardDrive 
} from 'lucide-vue-next';

const devicesStore = useDevicesStore();

const emit = defineEmits<{
  (e: 'filterStatus', status: string): void;
}>();

function selectFilter(status: string) {
  emit('filterStatus', status);
}
</script>

<template>
  <section class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3 select-none">
    
    <!-- Card 1: Online -->
    <div 
      @click="selectFilter('REACHABLE')"
      class="p-3 rounded-lg bg-zinc-900/80 border border-zinc-800 hover:border-emerald-500/40 cursor-pointer transition shadow-sm group"
    >
      <div class="flex items-center justify-between text-zinc-400 text-xs font-medium">
        <span>LAN Online</span>
        <CheckCircle2 class="w-4 h-4 text-emerald-400 group-hover:scale-110 transition" />
      </div>
      <div class="mt-1.5 flex items-baseline gap-1.5">
        <span class="text-xl font-bold text-emerald-400 font-mono">{{ devicesStore.upCount }}</span>
        <span class="text-xs text-zinc-400 font-mono">/ {{ devicesStore.totalCount }}</span>
      </div>
      <div class="mt-1 text-[10px] text-zinc-400 flex items-center gap-1.5">
        <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
        <span>Reachable</span>
      </div>
    </div>

    <!-- Card 2: Outages -->
    <div 
      @click="selectFilter('UNREACHABLE')"
      class="p-3 rounded-lg bg-zinc-900/80 border border-zinc-800 hover:border-rose-500/40 cursor-pointer transition shadow-sm group"
    >
      <div class="flex items-center justify-between text-zinc-400 text-xs font-medium">
        <span>Outages</span>
        <AlertCircle class="w-4 h-4 text-rose-400 group-hover:scale-110 transition" />
      </div>
      <div class="mt-1.5 flex items-baseline gap-1.5">
        <span 
          class="text-xl font-bold font-mono"
          :class="devicesStore.downCount > 0 ? 'text-rose-400 animate-pulse' : 'text-zinc-200'"
        >
          {{ devicesStore.downCount }}
        </span>
        <span class="text-xs text-zinc-400">offline</span>
      </div>
      <div class="mt-1 text-[10px] text-zinc-400">
        {{ devicesStore.downCount > 0 ? 'Packet loss detected' : 'All responding' }}
      </div>
    </div>

    <!-- Card 3: Suppressed -->
    <div 
      @click="selectFilter('SUPPRESSED')"
      class="p-3 rounded-lg bg-zinc-900/80 border border-zinc-800 hover:border-amber-500/40 cursor-pointer transition shadow-sm group"
    >
      <div class="flex items-center justify-between text-zinc-400 text-xs font-medium">
        <span>Suppressed</span>
        <ShieldAlert class="w-4 h-4 text-amber-400 group-hover:scale-110 transition" />
      </div>
      <div class="mt-1.5 flex items-baseline gap-1.5">
        <span class="text-xl font-bold text-amber-400 font-mono">{{ devicesStore.suppressedCount }}</span>
        <span class="text-xs text-zinc-400">nodes</span>
      </div>
      <div class="mt-1 text-[10px] text-zinc-400">
        Cascade suppressed
      </div>
    </div>

    <!-- Card 4: High CPU -->
    <div 
      class="p-3 rounded-lg bg-zinc-900/80 border border-zinc-800 hover:border-zinc-700 transition shadow-sm group"
    >
      <div class="flex items-center justify-between text-zinc-400 text-xs font-medium">
        <span>High CPU</span>
        <Cpu class="w-4 h-4 text-zinc-400 group-hover:scale-110 transition" />
      </div>
      <div class="mt-1.5 flex items-baseline gap-1.5">
        <span 
          class="text-xl font-bold font-mono"
          :class="devicesStore.highCpuCount > 0 ? 'text-amber-400' : 'text-zinc-200'"
        >
          {{ devicesStore.highCpuCount }}
        </span>
        <span class="text-xs text-zinc-400">&gt;80% load</span>
      </div>
      <div class="mt-1 text-[10px] text-zinc-400">
        {{ devicesStore.highCpuCount > 0 ? 'Compute pressure' : 'Nominal load' }}
      </div>
    </div>

    <!-- Card 5: High RAM -->
    <div 
      class="p-3 rounded-lg bg-zinc-900/80 border border-zinc-800 hover:border-zinc-700 transition shadow-sm group"
    >
      <div class="flex items-center justify-between text-zinc-400 text-xs font-medium">
        <span>High Memory</span>
        <HardDrive class="w-4 h-4 text-zinc-400 group-hover:scale-110 transition" />
      </div>
      <div class="mt-1.5 flex items-baseline gap-1.5">
        <span 
          class="text-xl font-bold font-mono"
          :class="devicesStore.highRamCount > 0 ? 'text-amber-400' : 'text-zinc-200'"
        >
          {{ devicesStore.highRamCount }}
        </span>
        <span class="text-xs text-zinc-400">&gt;80% RAM</span>
      </div>
      <div class="mt-1 text-[10px] text-zinc-400">
        {{ devicesStore.highRamCount > 0 ? 'Memory pressure' : 'Nominal memory' }}
      </div>
    </div>

  </section>
</template>
