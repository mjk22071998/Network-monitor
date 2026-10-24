<script setup lang="ts">
import { computed } from 'vue';
import { useDevicesStore } from '../../stores/devices';
import type { Device } from '../../types/device';
import { 
  Router,
  ArrowLeftRight,
  Monitor,
  Server
} from 'lucide-vue-next';

defineOptions({
  name: 'TopologyDeviceTreeNode'
});

const props = defineProps<{
  device: Device;
}>();

const emit = defineEmits<{
  (e: 'inspect', deviceId: number): void;
}>();

const devicesStore = useDevicesStore();

// Children connected directly to this device via parent_device_id
const children = computed<Device[]>(() => {
  const all = Array.isArray(devicesStore.devices) ? devicesStore.devices : [];
  return all.filter(d => d && d.parent_device_id === props.device.device_id);
});

// Icon mapping: Router for core router, ArrowLeftRight for switches, Monitor for PC, Server for server
function getDeviceIcon(typeId?: number) {
  switch (typeId) {
    case 1:
      return Router;
    case 2:
    case 3:
      return ArrowLeftRight;
    case 4:
      return Server;
    case 5:
    default:
      return Monitor;
  }
}

function getDeviceTypeName(typeId?: number): string {
  switch (typeId) {
    case 1: return 'Core Router';
    case 2: return 'Distribution Switch';
    case 3: return 'Access Switch';
    case 4: return 'Server';
    case 5: default: return 'Workstation PC';
  }
}

function isReachable(status?: string | null): boolean {
  if (!status) return false;
  const s = status.toUpperCase();
  return s === 'REACHABLE' || s === 'ONLINE';
}

function getNodeStatusColor(status?: string | null, isMaint?: boolean) {
  if (isMaint) {
    return {
      icon: 'text-purple-400',
      bg: 'bg-purple-500/10 hover:bg-purple-500/20',
      border: 'border-purple-500/40 hover:border-purple-400',
      glow: 'shadow-[0_0_12px_rgba(168,85,247,0.2)]',
      dot: 'bg-purple-400',
      badgeText: 'MAINTENANCE',
      badgeClass: 'bg-purple-950/60 border-purple-500/40 text-purple-300'
    };
  }

  if (isReachable(status)) {
    return {
      icon: 'text-emerald-400',
      bg: 'bg-emerald-500/10 hover:bg-emerald-500/20',
      border: 'border-emerald-500/50 hover:border-emerald-400',
      glow: 'shadow-[0_0_14px_rgba(16,185,129,0.25)]',
      dot: 'bg-emerald-400',
      badgeText: 'ONLINE',
      badgeClass: 'bg-emerald-950/60 border-emerald-500/40 text-emerald-300'
    };
  } else {
    return {
      icon: 'text-rose-400',
      bg: 'bg-rose-500/10 hover:bg-rose-500/20',
      border: 'border-rose-500/50 hover:border-rose-400',
      glow: 'shadow-[0_0_14px_rgba(244,63,94,0.25)]',
      dot: 'bg-rose-500',
      badgeText: 'OFFLINE',
      badgeClass: 'bg-rose-950/60 border-rose-500/40 text-rose-300'
    };
  }
}
</script>

<template>
  <div class="flex flex-col items-center">
    
    <!-- The Device Node -->
    <div 
      class="relative group cursor-pointer flex flex-col items-center"
      @click.stop="device && device.device_id > 0 && emit('inspect', device.device_id)"
    >
      <!-- Icon Box -->
      <div 
        class="w-12 h-12 p-2.5 rounded-xl flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow-lg relative backdrop-blur-sm"
        :class="[
          getNodeStatusColor(device.reachability_status, device.is_in_maintenance).border,
          getNodeStatusColor(device.reachability_status, device.is_in_maintenance).bg,
          getNodeStatusColor(device.reachability_status, device.is_in_maintenance).glow
        ]"
      >
        <component 
          :is="getDeviceIcon(device.type_id)" 
          class="w-6 h-6 transition-colors"
          :class="getNodeStatusColor(device.reachability_status, device.is_in_maintenance).icon"
        />
        <span 
          class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full border border-zinc-950"
          :class="getNodeStatusColor(device.reachability_status, device.is_in_maintenance).dot"
        ></span>
      </div>

      <!-- ONLY Device Name on Canvas (No extra text clutter) -->
      <span class="text-xs font-medium text-zinc-200 group-hover:text-zinc-100 truncate max-w-[125px] text-center mt-1.5 transition tracking-tight">
        {{ device.display_name }}
      </span>

      <!-- Hover Card (shows IP, Status Badge, RTT, Type) -->
      <div 
        class="opacity-0 pointer-events-none group-hover:opacity-100 transition-opacity duration-150 absolute bottom-full mb-2 z-50 w-48 p-2.5 rounded-lg bg-zinc-900 border border-zinc-700 shadow-2xl backdrop-blur-md text-left font-sans"
      >
        <div class="flex items-center justify-between gap-1 pb-1.5 border-b border-zinc-800">
          <span class="font-semibold text-xs text-zinc-100 truncate">{{ device.display_name }}</span>
          <span 
            class="px-1.5 py-0.5 rounded text-[10px] font-semibold border font-mono"
            :class="getNodeStatusColor(device.reachability_status, device.is_in_maintenance).badgeClass"
          >
            {{ getNodeStatusColor(device.reachability_status, device.is_in_maintenance).badgeText }}
          </span>
        </div>

        <div class="pt-1.5 space-y-1 text-[11px]">
          <div class="flex items-center justify-between">
            <span class="text-zinc-400">IP</span>
            <span class="font-mono text-zinc-200 font-medium">{{ device.primary_ip }}</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-zinc-400">Type</span>
            <span class="text-zinc-300">{{ getDeviceTypeName(device.type_id) }}</span>
          </div>
          <div v-if="device.latest_latency_us !== null && device.latest_latency_us !== undefined" class="flex items-center justify-between">
            <span class="text-zinc-400">RTT</span>
            <span class="font-mono text-emerald-400 font-medium">
              {{ device.latest_latency_us < 1000 ? `${device.latest_latency_us}µs` : `${(device.latest_latency_us/1000).toFixed(1)}ms` }}
            </span>
          </div>
          <div v-else class="flex items-center justify-between">
            <span class="text-zinc-400">RTT</span>
            <span class="font-mono text-zinc-500 italic">N/A</span>
          </div>
          <div v-if="device.zone" class="flex items-center justify-between">
            <span class="text-zinc-400">Zone</span>
            <span class="text-zinc-300 truncate max-w-[90px]">{{ device.zone }}</span>
          </div>
          <div v-if="device.group_name" class="flex items-center justify-between">
            <span class="text-zinc-400">Group</span>
            <span class="text-zinc-300 truncate max-w-[90px]">{{ device.group_name }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Direct Physical Downlink to Child Devices (Device to Device Connection) -->
    <div v-if="children.length > 0" class="flex flex-col items-center w-full mt-2">
      <!-- Vertical Drop Cable from Parent -->
      <div class="w-0.5 h-6 bg-zinc-700"></div>

      <!-- Horizontal Distribution Rail if multiple children -->
      <div 
        v-if="children.length > 1"
        class="h-0.5 bg-zinc-700 rounded-full mb-3"
        :style="{ width: `${Math.min(700, Math.max(60, children.length * 100))}px` }"
      ></div>

      <!-- Row of Connected Child Devices -->
      <div class="flex items-start justify-center gap-10">
        <div 
          v-for="child in children" 
          :key="child.device_id"
          class="flex flex-col items-center"
        >
          <!-- Drop Stub from Rail -->
          <div v-if="children.length > 1" class="w-0.5 h-3 bg-zinc-700 -mt-3 mb-1"></div>

          <!-- Recursive Node -->
          <TopologyDeviceTreeNode 
            :device="child"
            @inspect="(id) => id && id > 0 && emit('inspect', id)"
          />
        </div>
      </div>
    </div>

  </div>
</template>
