<script setup lang="ts">
import { ref, computed } from 'vue';
import { useDevicesStore } from '../../stores/devices';
import type { Device } from '../../types/device';
import { 
  ZoomIn, 
  ZoomOut, 
  RotateCcw, 
  Layers, 
  Server, 
  Radio, 
  Monitor, 
  Network 
} from 'lucide-vue-next';

const devicesStore = useDevicesStore();

const emit = defineEmits<{
  (e: 'inspect', deviceId: number): void;
}>();

// Pan and Zoom state
const scale = ref<number>(1);
const panX = ref<number>(50);
const panY = ref<number>(50);
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
  panY.value = 50;
}

// Group devices into hierarchy layers:
// Layer 0: Core Routers (type_id 1)
// Layer 1: Aggregate / Distribution Switches (type_id 2)
// Layer 2: Access Switches (type_id 3)
// Layer 3: Endpoints (Servers type_id 4, PCs type_id 5) organized into 2 parallel vertical branches under each access switch

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

// Find children for a given switch
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

// Or unparented endpoints
const unparentedEndpoints = computed(() => {
  const allDevices = Array.isArray(devicesStore.devices) ? devicesStore.devices : [];
  return endpoints.value.filter(d => !d.parent_device_id || !allDevices.some(p => p.device_id === d.parent_device_id));
});

function getNodeColor(status?: string, isMaint?: boolean): { border: string; bg: string; dot: string; text: string } {
  if (isMaint) {
    return {
      border: 'border-purple-500',
      bg: 'bg-purple-950/70',
      dot: 'bg-purple-400',
      text: 'text-purple-300'
    };
  }
  switch (status) {
    case 'REACHABLE':
      return {
        border: 'border-emerald-500',
        bg: 'bg-emerald-950/70',
        dot: 'bg-emerald-400 animate-pulse',
        text: 'text-emerald-300'
      };
    case 'UNREACHABLE':
      return {
        border: 'border-rose-500',
        bg: 'bg-rose-950/80',
        dot: 'bg-rose-500 animate-ping',
        text: 'text-rose-300'
      };
    case 'SUPPRESSED':
      return {
        border: 'border-amber-500',
        bg: 'bg-amber-950/70',
        dot: 'bg-amber-400',
        text: 'text-amber-300'
      };
    default:
      return {
        border: 'border-slate-700',
        bg: 'bg-slate-900/80',
        dot: 'bg-slate-500',
        text: 'text-slate-400'
      };
  }
}
</script>

<template>
  <div class="h-full flex flex-col relative select-none overflow-hidden bg-[#050811] bg-tactical-grid rounded-xl border border-tactical-border">
    
    <!-- Top Canvas HUD Controls -->
    <div class="absolute top-4 left-4 z-20 flex items-center space-x-3 bg-tactical-surface/90 border border-tactical-border backdrop-blur-md px-3.5 py-2 rounded-lg text-xs font-mono">
      <div class="flex items-center space-x-2 border-r border-tactical-border/70 pr-3">
        <Network class="w-4 h-4 text-tactical-cyan" />
        <span class="text-slate-200 font-bold uppercase tracking-wider text-[11px]">Campus Lab Topology</span>
      </div>

      <div class="flex items-center space-x-2 text-[10px] text-slate-400">
        <span class="flex items-center gap-1">
          <span class="w-2 h-2 rounded-full bg-emerald-400"></span> Up
        </span>
        <span class="flex items-center gap-1">
          <span class="w-2 h-2 rounded-full bg-rose-500"></span> Down
        </span>
        <span class="flex items-center gap-1">
          <span class="w-2 h-2 rounded-full bg-amber-400"></span> Suppressed
        </span>
        <span class="flex items-center gap-1">
          <span class="w-2 h-2 rounded-full bg-purple-400"></span> Maint
        </span>
      </div>
    </div>

    <!-- Zoom & Pan Toolbar -->
    <div class="absolute bottom-4 right-4 z-20 flex items-center space-x-1.5 bg-tactical-surface/90 border border-tactical-border backdrop-blur-md p-1.5 rounded-lg text-xs font-mono shadow-xl">
      <button 
        @click="zoomIn"
        class="p-2 rounded bg-tactical-panel hover:bg-slate-800 text-slate-300 hover:text-tactical-cyan transition"
        title="Zoom In"
      >
        <ZoomIn class="w-4 h-4" />
      </button>

      <button 
        @click="zoomOut"
        class="p-2 rounded bg-tactical-panel hover:bg-slate-800 text-slate-300 hover:text-tactical-cyan transition"
        title="Zoom Out"
      >
        <ZoomOut class="w-4 h-4" />
      </button>

      <div class="px-2 text-[11px] text-slate-400 font-mono">
        {{ Math.round(scale * 100) }}%
      </div>

      <button 
        @click="resetView"
        class="p-2 rounded bg-tactical-panel hover:bg-slate-800 text-slate-300 hover:text-tactical-cyan transition"
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
        <div class="p-10 inline-flex flex-col space-y-12">
          
          <!-- Layer 0: Core Backbone Gateway / Routers -->
          <div class="flex flex-col items-center">
            <div class="text-[10px] font-mono tracking-widest text-tactical-cyan uppercase mb-3 flex items-center gap-2">
              <span class="w-2 h-0.5 bg-tactical-cyan"></span>
              <span>Campus Core Gateway & Backbone</span>
              <span class="w-2 h-0.5 bg-tactical-cyan"></span>
            </div>

            <div class="flex items-center space-x-8">
              <div 
                v-for="router in coreRouters"
                :key="router.device_id"
                @click.stop="emit('inspect', router.device_id)"
                class="w-64 p-3 rounded-lg border-2 cursor-pointer shadow-xl transition-all duration-200 hover:scale-105"
                :class="[
                  getNodeColor(router.reachability_status, router.is_in_maintenance).border,
                  getNodeColor(router.reachability_status, router.is_in_maintenance).bg
                ]"
              >
                <div class="flex items-center justify-between mb-1.5">
                  <div class="flex items-center space-x-1.5">
                    <Radio class="w-4 h-4 text-tactical-cyan" />
                    <span class="font-bold text-slate-100 truncate text-xs">{{ router.display_name }}</span>
                  </div>
                  <span class="w-2 h-2 rounded-full" :class="getNodeColor(router.reachability_status, router.is_in_maintenance).dot"></span>
                </div>
                <div class="text-[10px] font-mono text-slate-300 flex justify-between">
                  <span>{{ router.primary_ip }}</span>
                  <span class="text-slate-400">{{ router.zone || 'Core NOC' }}</span>
                </div>
              </div>

              <!-- Fallback if no core router created yet -->
              <div 
                v-if="coreRouters.length === 0"
                class="w-64 p-3 rounded-lg border border-dashed border-tactical-border/80 bg-tactical-panel/40 text-center"
              >
                <span class="text-[10px] font-mono text-slate-500">No Core Router provisioned</span>
              </div>
            </div>
          </div>

          <!-- Vertical Cable Connector Line -->
          <div class="w-0.5 h-8 bg-tactical-cyan/40 mx-auto"></div>

          <!-- Layer 1: Distribution / Aggregate Switches -->
          <div class="flex flex-col items-center">
            <div class="text-[10px] font-mono tracking-widest text-slate-400 uppercase mb-3 flex items-center gap-2">
              <span class="w-2 h-0.5 bg-slate-500"></span>
              <span>Distribution / Building Aggregate Layer</span>
              <span class="w-2 h-0.5 bg-slate-500"></span>
            </div>

            <div class="flex items-center space-x-8">
              <div 
                v-for="sw in aggSwitches"
                :key="sw.device_id"
                @click.stop="emit('inspect', sw.device_id)"
                class="w-60 p-3 rounded-lg border-2 cursor-pointer shadow-xl transition-all duration-200 hover:scale-105"
                :class="[
                  getNodeColor(sw.reachability_status, sw.is_in_maintenance).border,
                  getNodeColor(sw.reachability_status, sw.is_in_maintenance).bg
                ]"
              >
                <div class="flex items-center justify-between mb-1">
                  <div class="flex items-center space-x-1.5">
                    <Layers class="w-4 h-4 text-tactical-amber" />
                    <span class="font-bold text-slate-100 truncate text-xs">{{ sw.display_name }}</span>
                  </div>
                  <span class="w-2 h-2 rounded-full" :class="getNodeColor(sw.reachability_status, sw.is_in_maintenance).dot"></span>
                </div>
                <div class="text-[10px] font-mono text-slate-300 flex justify-between">
                  <span>{{ sw.primary_ip }}</span>
                  <span class="text-slate-400">{{ sw.zone || 'Distribution Rack' }}</span>
                </div>
              </div>

              <div 
                v-if="aggSwitches.length === 0"
                class="w-60 p-3 rounded-lg border border-dashed border-tactical-border/80 bg-tactical-panel/40 text-center"
              >
                <span class="text-[10px] font-mono text-slate-500">No Distribution Switch provisioned</span>
              </div>
            </div>
          </div>

          <!-- Vertical Cable Connector Line -->
          <div class="w-0.5 h-8 bg-tactical-border mx-auto"></div>

          <!-- Layer 2 & 3: Lab Access Switches + 2-Branch Parallel Endpoint Columns -->
          <div class="flex flex-col items-center">
            <div class="text-[10px] font-mono tracking-widest text-slate-400 uppercase mb-4 flex items-center gap-2">
              <span class="w-2 h-0.5 bg-slate-500"></span>
              <span>Lab Access Switches & 2-Branch Parallel Workstation Pods</span>
              <span class="w-2 h-0.5 bg-slate-500"></span>
            </div>

            <!-- Horizontal Container for all Access Switches -->
            <div class="flex items-start space-x-12">
              
              <div 
                v-for="accSw in accessSwitches"
                :key="accSw.device_id"
                class="flex flex-col items-center"
              >
                <!-- Access Switch Node Card -->
                <div 
                  @click.stop="emit('inspect', accSw.device_id)"
                  class="w-56 p-3 rounded-lg border-2 cursor-pointer shadow-xl transition-all duration-200 hover:scale-105 z-10"
                  :class="[
                    getNodeColor(accSw.reachability_status, accSw.is_in_maintenance).border,
                    getNodeColor(accSw.reachability_status, accSw.is_in_maintenance).bg
                  ]"
                >
                  <div class="flex items-center justify-between mb-1">
                    <div class="flex items-center space-x-1.5">
                      <Server class="w-3.5 h-3.5 text-tactical-cyan" />
                      <span class="font-bold text-slate-100 truncate text-xs">{{ accSw.display_name }}</span>
                    </div>
                    <span class="w-2 h-2 rounded-full" :class="getNodeColor(accSw.reachability_status, accSw.is_in_maintenance).dot"></span>
                  </div>
                  <div class="text-[10px] font-mono text-slate-300 flex justify-between">
                    <span>{{ accSw.primary_ip }}</span>
                    <span class="text-slate-400">{{ accSw.zone || 'Lab Switch' }}</span>
                  </div>
                </div>

                <!-- Vertical Downlink Cable -->
                <div class="w-0.5 h-6 bg-tactical-border"></div>

                <!-- 2-Branch Parallel Splitter Cable -->
                <div class="w-48 h-3 border-t-2 border-l-2 border-r-2 border-tactical-border/70 rounded-t"></div>

                <!-- 2 Parallel Columns of Endpoints (Computer Row A / Computer Row B) -->
                <div class="flex items-start space-x-6">
                  
                  <!-- Left Column (Row 1) -->
                  <div class="flex flex-col space-y-2.5 w-44">
                    <div 
                      v-for="ep in getChildrenForSwitch(accSw.device_id).leftBranch"
                      :key="ep.device_id"
                      @click.stop="emit('inspect', ep.device_id)"
                      class="p-2 rounded border cursor-pointer transition hover:scale-102 shadow"
                      :class="[
                        getNodeColor(ep.reachability_status, ep.is_in_maintenance).border,
                        getNodeColor(ep.reachability_status, ep.is_in_maintenance).bg
                      ]"
                    >
                      <div class="flex items-center justify-between">
                        <div class="flex items-center space-x-1.5 truncate">
                          <Monitor v-if="ep.type_id === 5" class="w-3 h-3 text-slate-300 flex-shrink-0" />
                          <Server v-else class="w-3 h-3 text-tactical-amber flex-shrink-0" />
                          <span class="text-[10px] font-bold text-slate-200 truncate">{{ ep.display_name }}</span>
                        </div>
                        <span class="w-1.5 h-1.5 rounded-full" :class="getNodeColor(ep.reachability_status, ep.is_in_maintenance).dot"></span>
                      </div>
                      <div class="text-[9px] text-slate-400 flex justify-between mt-0.5">
                        <span>{{ ep.primary_ip }}</span>
                        <span v-if="ep.latest_latency_us !== null && ep.latest_latency_us !== undefined" class="text-tactical-cyan">
                          {{ ep.latest_latency_us < 1000 ? `${ep.latest_latency_us}µs` : `${(ep.latest_latency_us/1000).toFixed(1)}ms` }}
                        </span>
                      </div>
                    </div>

                    <div 
                      v-if="getChildrenForSwitch(accSw.device_id).leftBranch.length === 0"
                      class="p-2 rounded border border-dashed border-tactical-border/50 text-center"
                    >
                      <span class="text-[9px] text-slate-600">No endpoints</span>
                    </div>
                  </div>

                  <!-- Right Column (Row 2) -->
                  <div class="flex flex-col space-y-2.5 w-44">
                    <div 
                      v-for="ep in getChildrenForSwitch(accSw.device_id).rightBranch"
                      :key="ep.device_id"
                      @click.stop="emit('inspect', ep.device_id)"
                      class="p-2 rounded border cursor-pointer transition hover:scale-102 shadow"
                      :class="[
                        getNodeColor(ep.reachability_status, ep.is_in_maintenance).border,
                        getNodeColor(ep.reachability_status, ep.is_in_maintenance).bg
                      ]"
                    >
                      <div class="flex items-center justify-between">
                        <div class="flex items-center space-x-1.5 truncate">
                          <Monitor v-if="ep.type_id === 5" class="w-3 h-3 text-slate-300 flex-shrink-0" />
                          <Server v-else class="w-3 h-3 text-tactical-amber flex-shrink-0" />
                          <span class="text-[10px] font-bold text-slate-200 truncate">{{ ep.display_name }}</span>
                        </div>
                        <span class="w-1.5 h-1.5 rounded-full" :class="getNodeColor(ep.reachability_status, ep.is_in_maintenance).dot"></span>
                      </div>
                      <div class="text-[9px] text-slate-400 flex justify-between mt-0.5">
                        <span>{{ ep.primary_ip }}</span>
                        <span v-if="ep.latest_latency_us !== null && ep.latest_latency_us !== undefined" class="text-tactical-cyan">
                          {{ ep.latest_latency_us < 1000 ? `${ep.latest_latency_us}µs` : `${(ep.latest_latency_us/1000).toFixed(1)}ms` }}
                        </span>
                      </div>
                    </div>

                    <div 
                      v-if="getChildrenForSwitch(accSw.device_id).rightBranch.length === 0"
                      class="p-2 rounded border border-dashed border-tactical-border/50 text-center"
                    >
                      <span class="text-[9px] text-slate-600">No endpoints</span>
                    </div>
                  </div>

                </div>
              </div>

              <!-- Unassigned Endpoints Box (if any) -->
              <div v-if="unparentedEndpoints.length > 0" class="flex flex-col items-center">
                <div class="w-56 p-3 rounded-lg border border-dashed border-tactical-border bg-tactical-panel/40">
                  <div class="text-[10px] font-bold text-slate-400 uppercase tracking-wider mb-2 text-center">
                    Direct / Root Endpoints
                  </div>
                  <div class="space-y-1.5">
                    <div 
                      v-for="ep in unparentedEndpoints"
                      :key="ep.device_id"
                      @click.stop="emit('inspect', ep.device_id)"
                      class="p-2 rounded border cursor-pointer hover:border-tactical-cyan"
                      :class="[
                        getNodeColor(ep.reachability_status, ep.is_in_maintenance).border,
                        getNodeColor(ep.reachability_status, ep.is_in_maintenance).bg
                      ]"
                    >
                      <div class="flex items-center justify-between text-[10px]">
                        <span class="font-bold text-slate-200 truncate">{{ ep.display_name }}</span>
                        <span class="text-slate-400">{{ ep.primary_ip }}</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Fallback if no devices yet -->
              <div 
                v-if="accessSwitches.length === 0 && unparentedEndpoints.length === 0"
                class="p-8 rounded-lg border border-dashed border-tactical-border/80 bg-tactical-panel/40 text-center"
              >
                <Network class="w-8 h-8 text-tactical-cyan/40 mx-auto mb-2" />
                <p class="text-xs font-mono text-slate-400">No Access Switches or Lab Endpoints configured yet</p>
                <p class="text-[10px] font-mono text-slate-600 mt-1">Use the Device Inventory or Bulk Ingestion tab to add lab switches & workstations</p>
              </div>

            </div>
          </div>

        </div>
      </div>
    </div>
  </div>
</template>
