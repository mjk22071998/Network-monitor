<script setup lang="ts">
import { useDevicesStore } from '../../stores/devices';
import { 
  CheckCircle2, 
  AlertOctagon, 
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
  <section class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3.5 select-none font-mono">
    
    <!-- Card 1: Total Up -->
    <div 
      @click="selectFilter('REACHABLE')"
      class="p-3.5 rounded-lg bg-tactical-surface border border-tactical-border hover:border-tactical-online/50 cursor-pointer transition relative overflow-hidden group shadow-lg"
    >
      <div class="flex items-center justify-between text-slate-400 text-[10px] uppercase font-bold tracking-wider">
        <span>LAN Devices Online</span>
        <CheckCircle2 class="w-4 h-4 text-tactical-online group-hover:scale-110 transition" />
      </div>
      <div class="mt-2 flex items-baseline space-x-2">
        <span class="text-2xl font-bold text-tactical-online">{{ devicesStore.upCount }}</span>
        <span class="text-[10px] text-slate-500">/ {{ devicesStore.totalCount }} total</span>
      </div>
      <div class="mt-1 text-[9px] text-slate-500 flex items-center gap-1">
        <span class="w-1.5 h-1.5 rounded-full bg-tactical-online animate-pulse"></span>
        <span>Reachable via 5s ICMP</span>
      </div>
    </div>

    <!-- Card 2: Outage / Down -->
    <div 
      @click="selectFilter('UNREACHABLE')"
      class="p-3.5 rounded-lg bg-tactical-surface border border-tactical-border hover:border-tactical-alert/50 cursor-pointer transition relative overflow-hidden group shadow-lg"
    >
      <div class="flex items-center justify-between text-slate-400 text-[10px] uppercase font-bold tracking-wider">
        <span>Active Outages</span>
        <AlertOctagon class="w-4 h-4 text-tactical-alert group-hover:scale-110 transition" />
      </div>
      <div class="mt-2 flex items-baseline space-x-2">
        <span 
          class="text-2xl font-bold"
          :class="devicesStore.downCount > 0 ? 'text-tactical-alert animate-pulse' : 'text-slate-300'"
        >
          {{ devicesStore.downCount }}
        </span>
        <span class="text-[10px] text-slate-500">devices</span>
      </div>
      <div class="mt-1 text-[9px] text-slate-500">
        {{ devicesStore.downCount > 0 ? 'Urgent packet drop detected' : 'Zero packet loss detected' }}
      </div>
    </div>

    <!-- Card 3: Cascade Suppressed -->
    <div 
      @click="selectFilter('SUPPRESSED')"
      class="p-3.5 rounded-lg bg-tactical-surface border border-tactical-border hover:border-tactical-amber/50 cursor-pointer transition relative overflow-hidden group shadow-lg"
    >
      <div class="flex items-center justify-between text-slate-400 text-[10px] uppercase font-bold tracking-wider">
        <span>Cascade Suppressed</span>
        <ShieldAlert class="w-4 h-4 text-tactical-amber group-hover:scale-110 transition" />
      </div>
      <div class="mt-2 flex items-baseline space-x-2">
        <span class="text-2xl font-bold text-tactical-amber">{{ devicesStore.suppressedCount }}</span>
        <span class="text-[10px] text-slate-500">nodes</span>
      </div>
      <div class="mt-1 text-[9px] text-slate-500">
        Parent switch failure / Maint
      </div>
    </div>

    <!-- Card 4: High CPU >80% -->
    <div 
      class="p-3.5 rounded-lg bg-tactical-surface border border-tactical-border hover:border-tactical-amber/50 cursor-pointer transition relative overflow-hidden group shadow-lg"
    >
      <div class="flex items-center justify-between text-slate-400 text-[10px] uppercase font-bold tracking-wider">
        <span>High CPU (>80%)</span>
        <Cpu class="w-4 h-4 text-tactical-amber group-hover:scale-110 transition" />
      </div>
      <div class="mt-2 flex items-baseline space-x-2">
        <span 
          class="text-2xl font-bold"
          :class="devicesStore.highCpuCount > 0 ? 'text-tactical-amber' : 'text-slate-300'"
        >
          {{ devicesStore.highCpuCount }}
        </span>
        <span class="text-[10px] text-slate-500">hosts</span>
      </div>
      <div class="mt-1 text-[9px] text-slate-500">
        Heavy computational load
      </div>
    </div>

    <!-- Card 5: High RAM >80% -->
    <div 
      class="p-3.5 rounded-lg bg-tactical-surface border border-tactical-border hover:border-tactical-amber/50 cursor-pointer transition relative overflow-hidden group shadow-lg"
    >
      <div class="flex items-center justify-between text-slate-400 text-[10px] uppercase font-bold tracking-wider">
        <span>High RAM (>80%)</span>
        <HardDrive class="w-4 h-4 text-tactical-amber group-hover:scale-110 transition" />
      </div>
      <div class="mt-2 flex items-baseline space-x-2">
        <span 
          class="text-2xl font-bold"
          :class="devicesStore.highRamCount > 0 ? 'text-tactical-amber' : 'text-slate-300'"
        >
          {{ devicesStore.highRamCount }}
        </span>
        <span class="text-[10px] text-slate-500">hosts</span>
      </div>
      <div class="mt-1 text-[9px] text-slate-500">
        Memory pressure threshold
      </div>
    </div>

  </section>
</template>
