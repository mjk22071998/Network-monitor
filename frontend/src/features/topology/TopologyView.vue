<script setup lang="ts">
import { ref, computed } from 'vue';
import { useDevicesStore } from '../../stores/devices';
import type { Device } from '../../types/device';
import { 
  ZoomIn, 
  ZoomOut, 
  RotateCcw, 
  Router,
  ArrowLeftRight,
  Monitor,
  Server,
  Network
} from 'lucide-vue-next';

const devicesStore = useDevicesStore();

const emit = defineEmits<{
  (e: 'inspect', deviceId: number): void;
}>();

// Pan and Zoom state
const scale = ref<number>(1);
const panX = ref<number>(50);
const panY = ref<number>(40);
const isDragging = ref<boolean>(false);
const startMouseX = ref<number>(0);
const startMouseY = ref<number>(0);

function handleMouseDown(e: MouseEvent) {
  if (e.button !== 0) return; // Only left click drag
  isDragging.value = true;
  startMouseX.value = e.clientX - panX.value;
  startMouseY.value = e.clientY - panY.value;
}

function handleMouseMove(e: MouseEvent) {
  if (!isDragging.value) return;
  panX.value = e.clientX - startMouseX.value;
  panY.value = e.clientY - startMouseY.value;
}

function handleMouseUp() {
  isDragging.value = false;
}

function handleWheel(e: WheelEvent) {
  e.preventDefault();
  const delta = e.deltaY < 0 ? 0.1 : -0.1;
  const newScale = Math.min(2.5, Math.max(0.4, scale.value + delta));
  scale.value = Number(newScale.toFixed(2));
}

function zoomIn() {
  scale.value = Math.min(2.5, Number((scale.value + 0.15).toFixed(2)));
}

function zoomOut() {
  scale.value = Math.max(0.4, Number((scale.value - 0.15).toFixed(2)));
}

function resetView() {
  scale.value = 1;
  panX.value = 50;
  panY.value = 40;
}

// Device grouping by hierarchy layer
const coreRouters = computed(() => {
  const list = Array.isArray(devicesStore.devices) ? devicesStore.devices : [];
  return list.filter(d => d && d.type_id === 1);
});
const aggSwitches = computed(() => {
  const list = Array.isArray(devicesStore.devices) ? devicesStore.devices : [];
  return list.filter(d => d && d.type_id === 2);
});
const accessSwitches = computed(() => {
  const list = Array.isArray(devicesStore.devices) ? devicesStore.devices : [];
  return list.filter(d => d && d.type_id === 3);
});
const endpoints = computed(() => {
  const list = Array.isArray(devicesStore.devices) ? devicesStore.devices : [];
  return list.filter(d => d && (d.type_id === 4 || d.type_id === 5));
});

function getChildrenForSwitch(switchId: number): { leftBranch: Device[]; rightBranch: Device[] } {
  const children = endpoints.value.filter(d => d.parent_device_id === switchId);
  const leftBranch: Device[] = [];
  const rightBranch: Device[] = [];
  
  children.forEach((dev, idx) => {
    if (idx % 2 === 0) leftBranch.push(dev);
    else rightBranch.push(dev);
  });

  return { leftBranch, rightBranch };
}

const unparentedEndpoints = computed(() => {
  const allDevices = Array.isArray(devicesStore.devices) ? devicesStore.devices : [];
  return endpoints.value.filter(d => !d.parent_device_id || !allDevices.some(p => p.device_id === d.parent_device_id));
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
  <div class="h-full flex flex-col relative select-none overflow-hidden bg-zinc-950 bg-tactical-grid rounded-xl border border-zinc-800 shadow-sm">
    
    <!-- Top Canvas Status Legend -->
    <div class="absolute top-4 left-4 z-20 flex items-center gap-3 bg-zinc-900/90 border border-zinc-800 backdrop-blur-md px-3 py-1.5 rounded-lg text-xs font-sans shadow-md">
      <div class="flex items-center gap-2 border-r border-zinc-800 pr-3">
        <Network class="w-3.5 h-3.5 text-zinc-300" />
        <span class="text-zinc-200 font-medium text-xs">Topology</span>
      </div>

      <div class="flex items-center gap-3 text-xs text-zinc-400">
        <span class="flex items-center gap-1.5">
          <span class="w-2 h-2 rounded-full bg-emerald-400"></span> Reachable
        </span>
        <span class="flex items-center gap-1.5">
          <span class="w-2 h-2 rounded-full bg-rose-500"></span> Unreachable
        </span>
        <span class="flex items-center gap-1.5">
          <span class="w-2 h-2 rounded-full bg-purple-400"></span> Maint
        </span>
      </div>
    </div>

    <!-- Zoom & Pan Toolbar -->
    <div class="absolute bottom-4 right-4 z-20 flex items-center gap-1 bg-zinc-900/90 border border-zinc-800 backdrop-blur-md p-1 rounded-lg text-xs shadow-lg">
      <button 
        @click="zoomIn"
        class="p-1.5 rounded hover:bg-zinc-800 text-zinc-300 hover:text-zinc-100 transition"
        title="Zoom In"
      >
        <ZoomIn class="w-4 h-4" />
      </button>

      <button 
        @click="zoomOut"
        class="p-1.5 rounded hover:bg-zinc-800 text-zinc-300 hover:text-zinc-100 transition"
        title="Zoom Out"
      >
        <ZoomOut class="w-4 h-4" />
      </button>

      <div class="px-2 text-[11px] text-zinc-400 font-mono">
        {{ Math.round(scale * 100) }}%
      </div>

      <button 
        @click="resetView"
        class="p-1.5 rounded hover:bg-zinc-800 text-zinc-300 hover:text-zinc-100 transition"
        title="Reset Fit"
      >
        <RotateCcw class="w-4 h-4" />
      </button>
    </div>

    <!-- Pan/Zoom Infinite Canvas Container -->
    <div 
      class="flex-1 w-full h-full cursor-grab active:cursor-grabbing overflow-hidden"
      @mousedown="handleMouseDown"
      @mousemove="handleMouseMove"
      @mouseup="handleMouseUp"
      @mouseleave="handleMouseUp"
      @wheel="handleWheel"
    >
      <div 
        class="min-w-full min-h-full transition-transform duration-75 origin-top-left"
        :style="{
          transform: `translate(${panX}px, ${panY}px) scale(${scale})`
        }"
      >
        <div class="p-10 inline-flex flex-col space-y-10 items-center min-w-full">
          
          <!-- Layer 0: Core Backbone Gateway / Routers -->
          <div class="flex flex-col items-center">
            <div class="text-[11px] font-medium text-zinc-400 uppercase tracking-wider mb-4 flex items-center gap-2">
              <span class="w-3 h-px bg-zinc-700"></span>
              <span>Core Gateway</span>
              <span class="w-3 h-px bg-zinc-700"></span>
            </div>

            <div class="flex items-center space-x-12">
              <div 
                v-for="router in coreRouters"
                :key="router.device_id"
                class="relative group cursor-pointer flex flex-col items-center"
                @click.stop="emit('inspect', router.device_id)"
              >
                <!-- Icon Circle -->
                <div 
                  class="w-13 h-13 p-3 rounded-2xl flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow-lg relative backdrop-blur-sm"
                  :class="[
                    getNodeStatusColor(router.reachability_status, router.is_in_maintenance).border,
                    getNodeStatusColor(router.reachability_status, router.is_in_maintenance).bg,
                    getNodeStatusColor(router.reachability_status, router.is_in_maintenance).glow
                  ]"
                >
                  <component 
                    :is="getDeviceIcon(router.type_id)" 
                    class="w-7 h-7 transition-colors"
                    :class="getNodeStatusColor(router.reachability_status, router.is_in_maintenance).icon"
                  />
                  <span 
                    class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full border border-zinc-950"
                    :class="getNodeStatusColor(router.reachability_status, router.is_in_maintenance).dot"
                  ></span>
                </div>

                <!-- Only Device Name on Canvas -->
                <span class="text-xs font-medium text-zinc-200 group-hover:text-zinc-100 truncate max-w-[130px] text-center mt-2 transition tracking-tight">
                  {{ router.display_name }}
                </span>

                <!-- Hover Card (shows IP & Status Badge) -->
                <div 
                  class="opacity-0 pointer-events-none group-hover:opacity-100 transition-opacity duration-150 absolute bottom-full mb-2 z-50 w-48 p-2.5 rounded-lg bg-zinc-900 border border-zinc-700 shadow-2xl backdrop-blur-md text-left font-sans"
                >
                  <div class="flex items-center justify-between gap-1 pb-1.5 border-b border-zinc-800">
                    <span class="font-semibold text-xs text-zinc-100 truncate">{{ router.display_name }}</span>
                    <span 
                      class="px-1.5 py-0.5 rounded text-[10px] font-semibold border font-mono"
                      :class="getNodeStatusColor(router.reachability_status, router.is_in_maintenance).badgeClass"
                    >
                      {{ getNodeStatusColor(router.reachability_status, router.is_in_maintenance).badgeText }}
                    </span>
                  </div>

                  <div class="pt-1.5 space-y-1 text-[11px]">
                    <div class="flex items-center justify-between">
                      <span class="text-zinc-400">IP</span>
                      <span class="font-mono text-zinc-200 font-medium">{{ router.primary_ip }}</span>
                    </div>
                    <div class="flex items-center justify-between">
                      <span class="text-zinc-400">Type</span>
                      <span class="text-zinc-300">Core Router</span>
                    </div>
                    <div v-if="router.zone" class="flex items-center justify-between">
                      <span class="text-zinc-400">Zone</span>
                      <span class="text-zinc-300 truncate max-w-[90px]">{{ router.zone }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Fallback if no core router -->
              <div 
                v-if="coreRouters.length === 0"
                class="px-4 py-2 rounded-lg border border-dashed border-zinc-800 text-center"
              >
                <span class="text-xs text-zinc-400">No Core Router provisioned</span>
              </div>
            </div>
          </div>

          <!-- Vertical Connector Line -->
          <div class="w-px h-8 bg-zinc-700"></div>

          <!-- Layer 1: Distribution / Aggregate Switches -->
          <div class="flex flex-col items-center">
            <div class="text-[11px] font-medium text-zinc-400 uppercase tracking-wider mb-4 flex items-center gap-2">
              <span class="w-3 h-px bg-zinc-700"></span>
              <span>Distribution Switches</span>
              <span class="w-3 h-px bg-zinc-700"></span>
            </div>

            <div class="flex items-center space-x-12">
              <div 
                v-for="sw in aggSwitches"
                :key="sw.device_id"
                class="relative group cursor-pointer flex flex-col items-center"
                @click.stop="emit('inspect', sw.device_id)"
              >
                <!-- Switch Icon (ArrowLeftRight) -->
                <div 
                  class="w-12 h-12 p-2.5 rounded-xl flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow-lg relative backdrop-blur-sm"
                  :class="[
                    getNodeStatusColor(sw.reachability_status, sw.is_in_maintenance).border,
                    getNodeStatusColor(sw.reachability_status, sw.is_in_maintenance).bg,
                    getNodeStatusColor(sw.reachability_status, sw.is_in_maintenance).glow
                  ]"
                >
                  <ArrowLeftRight 
                    class="w-6 h-6 transition-colors"
                    :class="getNodeStatusColor(sw.reachability_status, sw.is_in_maintenance).icon"
                  />
                  <span 
                    class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full border border-zinc-950"
                    :class="getNodeStatusColor(sw.reachability_status, sw.is_in_maintenance).dot"
                  ></span>
                </div>

                <!-- Only Device Name on Canvas -->
                <span class="text-xs font-medium text-zinc-200 group-hover:text-zinc-100 truncate max-w-[120px] text-center mt-2 transition tracking-tight">
                  {{ sw.display_name }}
                </span>

                <!-- Hover Card (shows IP & Status Badge) -->
                <div 
                  class="opacity-0 pointer-events-none group-hover:opacity-100 transition-opacity duration-150 absolute bottom-full mb-2 z-50 w-48 p-2.5 rounded-lg bg-zinc-900 border border-zinc-700 shadow-2xl backdrop-blur-md text-left font-sans"
                >
                  <div class="flex items-center justify-between gap-1 pb-1.5 border-b border-zinc-800">
                    <span class="font-semibold text-xs text-zinc-100 truncate">{{ sw.display_name }}</span>
                    <span 
                      class="px-1.5 py-0.5 rounded text-[10px] font-semibold border font-mono"
                      :class="getNodeStatusColor(sw.reachability_status, sw.is_in_maintenance).badgeClass"
                    >
                      {{ getNodeStatusColor(sw.reachability_status, sw.is_in_maintenance).badgeText }}
                    </span>
                  </div>

                  <div class="pt-1.5 space-y-1 text-[11px]">
                    <div class="flex items-center justify-between">
                      <span class="text-zinc-400">IP</span>
                      <span class="font-mono text-zinc-200 font-medium">{{ sw.primary_ip }}</span>
                    </div>
                    <div class="flex items-center justify-between">
                      <span class="text-zinc-400">Type</span>
                      <span class="text-zinc-300">Distribution Switch</span>
                    </div>
                    <div v-if="sw.zone" class="flex items-center justify-between">
                      <span class="text-zinc-400">Rack</span>
                      <span class="text-zinc-300 truncate max-w-[90px]">{{ sw.zone }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <div 
                v-if="aggSwitches.length === 0"
                class="px-4 py-2 rounded-lg border border-dashed border-zinc-800 text-center"
              >
                <span class="text-xs text-zinc-400">No Distribution Switch provisioned</span>
              </div>
            </div>
          </div>

          <!-- Vertical Connector Line -->
          <div class="w-px h-8 bg-zinc-700"></div>

          <!-- Layer 2 & 3: Lab Access Switches + Connected Workstations -->
          <div class="flex flex-col items-center w-full">
            <div class="text-[11px] font-medium text-zinc-400 uppercase tracking-wider mb-6 flex items-center gap-2">
              <span class="w-3 h-px bg-zinc-700"></span>
              <span>Lab Switches & Workstations</span>
              <span class="w-3 h-px bg-zinc-700"></span>
            </div>

            <!-- Horizontal Container for Access Switches -->
            <div class="flex items-start justify-center flex-wrap gap-12 w-full">
              
              <div 
                v-for="accSw in accessSwitches"
                :key="accSw.device_id"
                class="flex flex-col items-center"
              >
                <!-- Access Switch Node (ArrowLeftRight) -->
                <div 
                  class="relative group cursor-pointer flex flex-col items-center"
                  @click.stop="emit('inspect', accSw.device_id)"
                >
                  <div 
                    class="w-12 h-12 p-2.5 rounded-xl flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow-lg relative backdrop-blur-sm"
                    :class="[
                      getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).border,
                      getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).bg,
                      getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).glow
                    ]"
                  >
                    <ArrowLeftRight 
                      class="w-6 h-6 transition-colors"
                      :class="getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).icon"
                    />
                    <span 
                      class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full border border-zinc-950"
                      :class="getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).dot"
                    ></span>
                  </div>

                  <!-- Only Name below icon -->
                  <span class="text-xs font-medium text-zinc-200 group-hover:text-zinc-100 truncate max-w-[120px] text-center mt-2 transition tracking-tight">
                    {{ accSw.display_name }}
                  </span>

                  <!-- Hover Card (shows IP & Status Badge) -->
                  <div 
                    class="opacity-0 pointer-events-none group-hover:opacity-100 transition-opacity duration-150 absolute bottom-full mb-2 z-50 w-48 p-2.5 rounded-lg bg-zinc-900 border border-zinc-700 shadow-2xl backdrop-blur-md text-left font-sans"
                  >
                    <div class="flex items-center justify-between gap-1 pb-1.5 border-b border-zinc-800">
                      <span class="font-semibold text-xs text-zinc-100 truncate">{{ accSw.display_name }}</span>
                      <span 
                        class="px-1.5 py-0.5 rounded text-[10px] font-semibold border font-mono"
                        :class="getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).badgeClass"
                      >
                        {{ getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).badgeText }}
                      </span>
                    </div>

                    <div class="pt-1.5 space-y-1 text-[11px]">
                      <div class="flex items-center justify-between">
                        <span class="text-zinc-400">IP</span>
                        <span class="font-mono text-zinc-200 font-medium">{{ accSw.primary_ip }}</span>
                      </div>
                      <div class="flex items-center justify-between">
                        <span class="text-zinc-400">Type</span>
                        <span class="text-zinc-300">Access Switch</span>
                      </div>
                      <div v-if="accSw.zone" class="flex items-center justify-between">
                        <span class="text-zinc-400">Lab</span>
                        <span class="text-zinc-300 truncate max-w-[90px]">{{ accSw.zone }}</span>
                      </div>
                    </div>
                  </div>
                </div>

                <!-- Downlink Cable Connector -->
                <div class="w-px h-6 bg-zinc-700 mt-2"></div>
                <div class="w-36 h-2 border-t border-l border-r border-zinc-700 rounded-t"></div>

                <!-- Parallel Columns of Workstations & Servers -->
                <div class="flex items-start space-x-6">
                  
                  <!-- Left Workstation Branch -->
                  <div class="flex flex-col space-y-3 items-center w-20">
                    <div 
                      v-for="ep in getChildrenForSwitch(accSw.device_id).leftBranch"
                      :key="ep.device_id"
                      class="relative group cursor-pointer flex flex-col items-center"
                      @click.stop="emit('inspect', ep.device_id)"
                    >
                      <div 
                        class="w-10 h-10 rounded-lg flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow relative"
                        :class="[
                          getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).border,
                          getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).bg,
                          getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).glow
                        ]"
                      >
                        <component 
                          :is="getDeviceIcon(ep.type_id)" 
                          class="w-5 h-5 transition-colors"
                          :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).icon"
                        />
                        <span 
                          class="absolute -top-0.5 -right-0.5 w-2 h-2 rounded-full border border-zinc-950"
                          :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).dot"
                        ></span>
                      </div>

                      <!-- Only Name below icon -->
                      <span class="text-[10px] font-medium text-zinc-300 group-hover:text-zinc-100 truncate max-w-[80px] text-center mt-1 transition">
                        {{ ep.display_name }}
                      </span>

                      <!-- Hover Card (shows IP & Status Badge) -->
                      <div 
                        class="opacity-0 pointer-events-none group-hover:opacity-100 transition-opacity duration-150 absolute bottom-full mb-2 z-50 w-44 p-2 rounded-lg bg-zinc-900 border border-zinc-700 shadow-2xl backdrop-blur-md text-left font-sans"
                      >
                        <div class="flex items-center justify-between gap-1 pb-1 border-b border-zinc-800">
                          <span class="font-semibold text-[11px] text-zinc-100 truncate">{{ ep.display_name }}</span>
                          <span 
                            class="px-1.5 py-0.2 rounded text-[9px] font-semibold border font-mono"
                            :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).badgeClass"
                          >
                            {{ getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).badgeText }}
                          </span>
                        </div>

                        <div class="pt-1 space-y-0.5 text-[10px]">
                          <div class="flex items-center justify-between">
                            <span class="text-zinc-400">IP</span>
                            <span class="font-mono text-zinc-200">{{ ep.primary_ip }}</span>
                          </div>
                          <div class="flex items-center justify-between">
                            <span class="text-zinc-400">Type</span>
                            <span class="text-zinc-300">{{ getDeviceTypeName(ep.type_id) }}</span>
                          </div>
                          <div v-if="ep.latest_latency_us !== null && ep.latest_latency_us !== undefined" class="flex items-center justify-between">
                            <span class="text-zinc-400">RTT</span>
                            <span class="font-mono text-emerald-400">
                              {{ ep.latest_latency_us < 1000 ? `${ep.latest_latency_us}µs` : `${(ep.latest_latency_us/1000).toFixed(1)}ms` }}
                            </span>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>

                  <!-- Right Workstation Branch -->
                  <div class="flex flex-col space-y-3 items-center w-20">
                    <div 
                      v-for="ep in getChildrenForSwitch(accSw.device_id).rightBranch"
                      :key="ep.device_id"
                      class="relative group cursor-pointer flex flex-col items-center"
                      @click.stop="emit('inspect', ep.device_id)"
                    >
                      <div 
                        class="w-10 h-10 rounded-lg flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow relative"
                        :class="[
                          getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).border,
                          getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).bg,
                          getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).glow
                        ]"
                      >
                        <component 
                          :is="getDeviceIcon(ep.type_id)" 
                          class="w-5 h-5 transition-colors"
                          :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).icon"
                        />
                        <span 
                          class="absolute -top-0.5 -right-0.5 w-2 h-2 rounded-full border border-zinc-950"
                          :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).dot"
                        ></span>
                      </div>

                      <!-- Only Name below icon -->
                      <span class="text-[10px] font-medium text-zinc-300 group-hover:text-zinc-100 truncate max-w-[80px] text-center mt-1 transition">
                        {{ ep.display_name }}
                      </span>

                      <!-- Hover Card (shows IP & Status Badge) -->
                      <div 
                        class="opacity-0 pointer-events-none group-hover:opacity-100 transition-opacity duration-150 absolute bottom-full mb-2 z-50 w-44 p-2 rounded-lg bg-zinc-900 border border-zinc-700 shadow-2xl backdrop-blur-md text-left font-sans"
                      >
                        <div class="flex items-center justify-between gap-1 pb-1 border-b border-zinc-800">
                          <span class="font-semibold text-[11px] text-zinc-100 truncate">{{ ep.display_name }}</span>
                          <span 
                            class="px-1.5 py-0.2 rounded text-[9px] font-semibold border font-mono"
                            :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).badgeClass"
                          >
                            {{ getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).badgeText }}
                          </span>
                        </div>

                        <div class="pt-1 space-y-0.5 text-[10px]">
                          <div class="flex items-center justify-between">
                            <span class="text-zinc-400">IP</span>
                            <span class="font-mono text-zinc-200">{{ ep.primary_ip }}</span>
                          </div>
                          <div class="flex items-center justify-between">
                            <span class="text-zinc-400">Type</span>
                            <span class="text-zinc-300">{{ getDeviceTypeName(ep.type_id) }}</span>
                          </div>
                          <div v-if="ep.latest_latency_us !== null && ep.latest_latency_us !== undefined" class="flex items-center justify-between">
                            <span class="text-zinc-400">RTT</span>
                            <span class="font-mono text-emerald-400">
                              {{ ep.latest_latency_us < 1000 ? `${ep.latest_latency_us}µs` : `${(ep.latest_latency_us/1000).toFixed(1)}ms` }}
                            </span>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>

                </div>
              </div>

              <!-- Unassigned Endpoints -->
              <div v-if="unparentedEndpoints.length > 0" class="flex flex-col items-center">
                <div class="p-3 rounded-lg border border-dashed border-zinc-800 text-center">
                  <div class="text-[10px] font-medium text-zinc-400 uppercase tracking-wider mb-2">
                    Standalone Devices
                  </div>
                  <div class="flex flex-wrap gap-3 justify-center max-w-xs">
                    <div 
                      v-for="ep in unparentedEndpoints"
                      :key="ep.device_id"
                      class="relative group cursor-pointer flex flex-col items-center"
                      @click.stop="emit('inspect', ep.device_id)"
                    >
                      <div 
                        class="w-10 h-10 rounded-lg flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow"
                        :class="[
                          getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).border,
                          getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).bg,
                          getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).glow
                        ]"
                      >
                        <component 
                          :is="getDeviceIcon(ep.type_id)" 
                          class="w-5 h-5 transition-colors"
                          :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).icon"
                        />
                      </div>
                      <span class="text-[10px] font-medium text-zinc-300 group-hover:text-zinc-100 truncate max-w-[80px] text-center mt-1">
                        {{ ep.display_name }}
                      </span>

                      <!-- Hover Card -->
                      <div 
                        class="opacity-0 pointer-events-none group-hover:opacity-100 transition-opacity duration-150 absolute bottom-full mb-2 z-50 w-44 p-2 rounded-lg bg-zinc-900 border border-zinc-700 shadow-2xl backdrop-blur-md text-left font-sans"
                      >
                        <div class="flex items-center justify-between gap-1 pb-1 border-b border-zinc-800">
                          <span class="font-semibold text-[11px] text-zinc-100 truncate">{{ ep.display_name }}</span>
                          <span 
                            class="px-1.5 py-0.2 rounded text-[9px] font-semibold border font-mono"
                            :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).badgeClass"
                          >
                            {{ getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).badgeText }}
                          </span>
                        </div>
                        <div class="pt-1 space-y-0.5 text-[10px]">
                          <div class="flex items-center justify-between">
                            <span class="text-zinc-400">IP</span>
                            <span class="font-mono text-zinc-200">{{ ep.primary_ip }}</span>
                          </div>
                          <div class="flex items-center justify-between">
                            <span class="text-zinc-400">Type</span>
                            <span class="text-zinc-300">{{ getDeviceTypeName(ep.type_id) }}</span>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Fallback if no devices yet -->
              <div 
                v-if="accessSwitches.length === 0 && unparentedEndpoints.length === 0"
                class="p-6 rounded-lg border border-dashed border-zinc-800 text-center max-w-md"
              >
                <Network class="w-6 h-6 text-zinc-400 mx-auto mb-2" />
                <p class="text-xs text-zinc-300 font-medium">No Access Switches or Workstations Configured</p>
                <p class="text-[11px] text-zinc-400 mt-0.5">Use the Device Inventory or Subnet Discovery tab to populate lab equipment.</p>
              </div>

            </div>
          </div>

        </div>
      </div>
    </div>
  </div>
</template>
