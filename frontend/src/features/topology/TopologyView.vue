<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useDevicesStore } from '../../stores/devices';
import { useGroupsStore } from '../../stores/groups';
import type { Device } from '../../types/device';
import type { GroupNode } from '../../types/group';
import { 
  ZoomIn, 
  ZoomOut, 
  RotateCcw, 
  Router,
  ArrowLeftRight,
  Monitor,
  Server,
  Network,
  GitFork,
  Layers
} from 'lucide-vue-next';

const devicesStore = useDevicesStore();
const groupsStore = useGroupsStore();

const emit = defineEmits<{
  (e: 'inspect', deviceId: number): void;
}>();

// Two distinct tree hierarchy models requested by user:
// 1. ROUTER_GROUPS_PCS: Core Router -> Aggregate Router -> Computer Lab Groups -> PCs below them
// 2. DEVICE_TO_DEVICE: Core Router -> Aggregate Switch -> Access Switch -> Child Endpoints
export type TopologyLayoutMode = 'ROUTER_GROUPS_PCS' | 'DEVICE_TO_DEVICE';
const layoutMode = ref<TopologyLayoutMode>('ROUTER_GROUPS_PCS');

onMounted(async () => {
  if (!groupsStore.groupsTree || groupsStore.groupsTree.length === 0) {
    await groupsStore.fetchGroups();
  }
});

// Pan and Zoom state
const scale = ref<number>(1);
const panX = ref<number>(50);
const panY = ref<number>(30);
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
  panY.value = 30;
}

// ---------------------------------------------------------------------------
// Device Filters & Helpers
// ---------------------------------------------------------------------------
const allDevices = computed<Device[]>(() => {
  return Array.isArray(devicesStore.devices) ? devicesStore.devices : [];
});

const coreRouters = computed(() => {
  return allDevices.value.filter(d => d && d.type_id === 1);
});

const aggSwitches = computed(() => {
  return allDevices.value.filter(d => d && d.type_id === 2);
});

const accessSwitches = computed(() => {
  return allDevices.value.filter(d => d && d.type_id === 3);
});

const endpoints = computed(() => {
  return allDevices.value.filter(d => d && (d.type_id === 4 || d.type_id === 5));
});

// Direct endpoints connected to an access switch
function getEndpointsForSwitch(switchId: number): Device[] {
  return endpoints.value.filter(d => d.parent_device_id === switchId);
}

// Direct access switches connected to an aggregate switch
function getAccessSwitchesForAgg(aggId: number): Device[] {
  return accessSwitches.value.filter(d => d.parent_device_id === aggId);
}

// Access switches without an aggregate switch parent
const unparentedAccessSwitches = computed(() => {
  const aggIds = new Set(aggSwitches.value.map(s => s.device_id));
  return accessSwitches.value.filter(s => !s.parent_device_id || !aggIds.has(s.parent_device_id));
});

// Direct servers/endpoints attached to core routers
const coreEndpoints = computed(() => {
  const coreIds = new Set(coreRouters.value.map(r => r.device_id));
  return endpoints.value.filter(d => d.parent_device_id && coreIds.has(d.parent_device_id));
});

// Standalone endpoints not attached to any known parent
const unparentedEndpoints = computed(() => {
  const knownDeviceIds = new Set(allDevices.value.map(d => d.device_id));
  return endpoints.value.filter(d => !d.parent_device_id || !knownDeviceIds.has(d.parent_device_id));
});

// ---------------------------------------------------------------------------
// Groups Helper for Router -> Groups -> PCs Mode
// ---------------------------------------------------------------------------
interface FlattenedGroup {
  group_id: number;
  group_name: string;
  parent_group_id: number | null;
  description: string | null;
  depth: number;
}

function flattenGroupsList(nodes: GroupNode[], depth: number = 0): FlattenedGroup[] {
  const result: FlattenedGroup[] = [];
  if (!Array.isArray(nodes)) return result;

  for (const node of nodes) {
    if (!node) continue;
    result.push({
      group_id: node.group_id,
      group_name: node.group_name,
      parent_group_id: node.parent_group_id,
      description: node.description,
      depth
    });
    if (node.children && Array.isArray(node.children) && node.children.length > 0) {
      result.push(...flattenGroupsList(node.children, depth + 1));
    }
  }
  return result;
}

const flatGroups = computed(() => {
  const list = flattenGroupsList(groupsStore.groupsTree || []);
  if (list.length === 0) {
    return [{
      group_id: 1,
      group_name: 'Unassigned Group',
      parent_group_id: null,
      description: 'System pool for unclassified devices',
      depth: 0
    }];
  }
  return list;
});

// PCs and non-core equipment belonging to this group
function getDevicesForGroup(groupId: number): Device[] {
  const knownGroupIds = new Set(flatGroups.value.map(g => g.group_id));

  // Exclude Core Routers (1) and Aggregate Switches (2) as they sit at the top levels
  if (groupId === 1) {
    return allDevices.value.filter(d => 
      (d.group_id === 1 || !knownGroupIds.has(d.group_id)) && 
      d.type_id !== 1 && d.type_id !== 2
    );
  }
  return allDevices.value.filter(d => 
    d.group_id === groupId && 
    d.type_id !== 1 && d.type_id !== 2
  );
}

function getGroupStats(groupId: number) {
  const devs = getDevicesForGroup(groupId);
  const total = devs.length;
  let online = 0;
  let offline = 0;
  for (const d of devs) {
    if (isReachable(d.reachability_status)) online++;
    else offline++;
  }
  return { total, online, offline };
}

// ---------------------------------------------------------------------------
// Lucide Icon & Status Helpers
// ---------------------------------------------------------------------------
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
    
    <!-- Top Canvas Header: Layout Mode Selector & Reachability Legend -->
    <div class="absolute top-4 left-4 z-20 flex flex-wrap items-center gap-3 bg-zinc-900/90 border border-zinc-800 backdrop-blur-md px-3 py-1.5 rounded-lg text-xs font-sans shadow-md">
      
      <!-- Layout Switcher Segmented Control -->
      <div class="flex items-center p-0.5 bg-zinc-950/80 border border-zinc-800 rounded-md">
        <button 
          @click="layoutMode = 'ROUTER_GROUPS_PCS'"
          class="flex items-center gap-1.5 px-3 py-1 rounded text-xs font-medium transition"
          :class="layoutMode === 'ROUTER_GROUPS_PCS' 
            ? 'bg-zinc-800 text-zinc-100 shadow-sm border border-zinc-700/60' 
            : 'text-zinc-400 hover:text-zinc-200'"
          title="Tree by Core Router -> Aggregate Router -> Computer Lab Groups -> PCs"
        >
          <Layers class="w-3.5 h-3.5 text-amber-400" />
          <span>Core → Agg → Groups → PCs</span>
        </button>

        <button 
          @click="layoutMode = 'DEVICE_TO_DEVICE'"
          class="flex items-center gap-1.5 px-3 py-1 rounded text-xs font-medium transition"
          :class="layoutMode === 'DEVICE_TO_DEVICE' 
            ? 'bg-zinc-800 text-zinc-100 shadow-sm border border-zinc-700/60' 
            : 'text-zinc-400 hover:text-zinc-200'"
          title="Tree by Device-to-Device Interconnects"
        >
          <GitFork class="w-3.5 h-3.5 text-emerald-400" />
          <span>Device to Device</span>
        </button>
      </div>

      <!-- Divider -->
      <div class="h-4 w-px bg-zinc-800"></div>

      <!-- Reachability Legend -->
      <div class="flex items-center gap-3 text-xs text-zinc-400 font-sans">
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
        
        <!-- ================================================================= -->
        <!-- MODE 1: CORE ROUTER -> AGGREGATE ROUTER -> GROUPS -> PCS BELOW    -->
        <!-- ================================================================= -->
        <div v-if="layoutMode === 'ROUTER_GROUPS_PCS'" class="p-10 inline-flex flex-col items-center min-w-full">
          
          <!-- Layer 0: Core Router(s) -->
          <div class="flex flex-col items-center">
            <div class="text-[11px] font-medium text-zinc-400 uppercase tracking-wider mb-3 flex items-center gap-2 font-mono">
              <span class="w-3 h-px bg-zinc-700"></span>
              <span>1. Core Gateway</span>
              <span class="w-3 h-px bg-zinc-700"></span>
            </div>

            <div class="flex items-center space-x-12">
              <div 
                v-for="router in coreRouters"
                :key="router.device_id"
                class="relative group cursor-pointer flex flex-col items-center"
                @click.stop="emit('inspect', router.device_id)"
              >
                <!-- Icon Box -->
                <div 
                  class="w-14 h-14 p-3 rounded-2xl flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow-lg relative backdrop-blur-sm"
                  :class="[
                    getNodeStatusColor(router.reachability_status, router.is_in_maintenance).border,
                    getNodeStatusColor(router.reachability_status, router.is_in_maintenance).bg,
                    getNodeStatusColor(router.reachability_status, router.is_in_maintenance).glow
                  ]"
                >
                  <Router 
                    class="w-7 h-7 transition-colors"
                    :class="getNodeStatusColor(router.reachability_status, router.is_in_maintenance).icon"
                  />
                  <span 
                    class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full border border-zinc-950"
                    :class="getNodeStatusColor(router.reachability_status, router.is_in_maintenance).dot"
                  ></span>
                </div>

                <!-- ONLY Device Name on Canvas -->
                <span class="text-xs font-medium text-zinc-200 group-hover:text-zinc-100 truncate max-w-[140px] text-center mt-2 transition tracking-tight">
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

          <!-- Trunk Cable: Core Router to Aggregate Router -->
          <div class="flex flex-col items-center my-3">
            <div class="w-0.5 h-6 bg-zinc-700"></div>
            <div class="w-1.5 h-1.5 rounded-full bg-emerald-500 shadow-[0_0_6px_rgba(16,185,129,0.4)]"></div>
            <div class="w-0.5 h-6 bg-zinc-700"></div>
          </div>

          <!-- Layer 1: Aggregate / Distribution Router(s) -->
          <div class="flex flex-col items-center">
            <div class="text-[11px] font-medium text-zinc-400 uppercase tracking-wider mb-3 flex items-center gap-2 font-mono">
              <span class="w-3 h-px bg-zinc-700"></span>
              <span>2. Aggregate / Distribution Switch</span>
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
                  class="w-13 h-13 p-3 rounded-xl flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow-lg relative backdrop-blur-sm"
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

                <!-- ONLY Device Name on Canvas -->
                <span class="text-xs font-medium text-zinc-200 group-hover:text-zinc-100 truncate max-w-[130px] text-center mt-2 transition tracking-tight">
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

              <!-- Fallback if no agg switch -->
              <div 
                v-if="aggSwitches.length === 0"
                class="px-4 py-2 rounded-lg border border-dashed border-zinc-800 text-center"
              >
                <span class="text-xs text-zinc-400">Direct Gateway Trunk (No Agg switch provisioned)</span>
              </div>
            </div>
          </div>

          <!-- Trunk Cable: Aggregate Switch down to Groups Bus Rail -->
          <div class="flex flex-col items-center w-full my-3">
            <div class="w-0.5 h-6 bg-zinc-700"></div>
            <!-- Horizontal Bus Line Connecting All Groups -->
            <div class="w-5/6 h-0.5 bg-zinc-700 rounded-full"></div>
          </div>

          <!-- Layer 2 & 3: Groups and their PCs directly below -->
          <div class="flex flex-col items-center w-full">
            <div class="text-[11px] font-medium text-zinc-400 uppercase tracking-wider mb-4 flex items-center gap-2 font-mono">
              <span class="w-3 h-px bg-zinc-700"></span>
              <span>3. Computer Lab Groups &amp; Connected PCs</span>
              <span class="w-3 h-px bg-zinc-700"></span>
            </div>

            <!-- Groups Row Container -->
            <div class="flex items-start justify-center flex-wrap gap-12 w-full pt-1">
              
              <div 
                v-for="group in flatGroups"
                :key="group.group_id"
                class="flex flex-col items-center"
              >
                <!-- Vertical Drop from Trunk Bus into Group Card -->
                <div class="w-0.5 h-5 bg-zinc-700 -mt-5 mb-2"></div>

                <!-- Group Station Card (Shadcn style) -->
                <div class="px-3.5 py-2 rounded-xl bg-zinc-900 border border-zinc-700/80 shadow-md backdrop-blur flex items-center gap-2.5 hover:border-amber-500/50 transition">
                  <div class="w-7 h-7 rounded-lg bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400 flex-shrink-0">
                    <Layers class="w-3.5 h-3.5" />
                  </div>
                  <div>
                    <div class="text-xs font-semibold text-zinc-100 tracking-tight">{{ group.group_name }}</div>
                    <div class="flex items-center gap-2 text-[10px] text-zinc-400 mt-0.5 font-mono">
                      <span>{{ getGroupStats(group.group_id).total }} PCs</span>
                      <span v-if="getGroupStats(group.group_id).online > 0" class="text-emerald-400 flex items-center gap-1 font-medium">
                        <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>{{ getGroupStats(group.group_id).online }} Online
                      </span>
                      <span v-if="getGroupStats(group.group_id).offline > 0" class="text-rose-400 flex items-center gap-1 font-medium">
                        <span class="w-1.5 h-1.5 rounded-full bg-rose-500"></span>{{ getGroupStats(group.group_id).offline }} Offline
                      </span>
                    </div>
                  </div>
                </div>

                <!-- Downlink Cable from Group down to its PCs -->
                <div class="w-0.5 h-6 bg-zinc-700 mt-2"></div>

                <!-- Horizontal branch rail if multiple PCs -->
                <div 
                  v-if="getDevicesForGroup(group.group_id).length > 1"
                  class="h-0.5 bg-zinc-700 rounded-full mb-3"
                  :style="{ width: `${Math.min(320, Math.max(80, getDevicesForGroup(group.group_id).length * 60))}px` }"
                ></div>

                <!-- PCs and Endpoints belonging to this Lab Group -->
                <div class="flex flex-wrap items-start justify-center gap-5 max-w-md pt-1">
                  
                  <div 
                    v-for="pc in getDevicesForGroup(group.group_id)"
                    :key="pc.device_id"
                    class="relative group cursor-pointer flex flex-col items-center"
                    @click.stop="emit('inspect', pc.device_id)"
                  >
                    <!-- PC Drop Cable -->
                    <div class="w-0.5 h-2 bg-zinc-700 -mt-3 mb-1"></div>

                    <!-- PC Icon Box -->
                    <div 
                      class="w-10 h-10 rounded-xl flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow-md relative backdrop-blur-sm"
                      :class="[
                        getNodeStatusColor(pc.reachability_status, pc.is_in_maintenance).border,
                        getNodeStatusColor(pc.reachability_status, pc.is_in_maintenance).bg,
                        getNodeStatusColor(pc.reachability_status, pc.is_in_maintenance).glow
                      ]"
                    >
                      <component 
                        :is="getDeviceIcon(pc.type_id)" 
                        class="w-5 h-5 transition-colors"
                        :class="getNodeStatusColor(pc.reachability_status, pc.is_in_maintenance).icon"
                      />
                      <span 
                        class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full border border-zinc-950"
                        :class="getNodeStatusColor(pc.reachability_status, pc.is_in_maintenance).dot"
                      ></span>
                    </div>

                    <!-- ONLY Device Name on Canvas -->
                    <span class="text-[10px] font-medium text-zinc-300 group-hover:text-zinc-100 truncate max-w-[85px] text-center mt-1.5 transition tracking-tight">
                      {{ pc.display_name }}
                    </span>

                    <!-- Hover Card (shows IP & Status Badge) -->
                    <div 
                      class="opacity-0 pointer-events-none group-hover:opacity-100 transition-opacity duration-150 absolute bottom-full mb-2 z-50 w-48 p-2.5 rounded-lg bg-zinc-900 border border-zinc-700 shadow-2xl backdrop-blur-md text-left font-sans"
                    >
                      <div class="flex items-center justify-between gap-1 pb-1.5 border-b border-zinc-800">
                        <span class="font-semibold text-xs text-zinc-100 truncate">{{ pc.display_name }}</span>
                        <span 
                          class="px-1.5 py-0.5 rounded text-[10px] font-semibold border font-mono"
                          :class="getNodeStatusColor(pc.reachability_status, pc.is_in_maintenance).badgeClass"
                        >
                          {{ getNodeStatusColor(pc.reachability_status, pc.is_in_maintenance).badgeText }}
                        </span>
                      </div>

                      <div class="pt-1.5 space-y-1 text-[11px]">
                        <div class="flex items-center justify-between">
                          <span class="text-zinc-400">IP</span>
                          <span class="font-mono text-zinc-200 font-medium">{{ pc.primary_ip }}</span>
                        </div>
                        <div class="flex items-center justify-between">
                          <span class="text-zinc-400">Type</span>
                          <span class="text-zinc-300">{{ getDeviceTypeName(pc.type_id) }}</span>
                        </div>
                        <div v-if="pc.latest_latency_us !== null && pc.latest_latency_us !== undefined" class="flex items-center justify-between">
                          <span class="text-zinc-400">RTT</span>
                          <span class="font-mono text-emerald-400 font-medium">
                            {{ pc.latest_latency_us < 1000 ? `${pc.latest_latency_us}µs` : `${(pc.latest_latency_us/1000).toFixed(1)}ms` }}
                          </span>
                        </div>
                        <div v-else class="flex items-center justify-between">
                          <span class="text-zinc-400">RTT</span>
                          <span class="font-mono text-zinc-500 italic">N/A</span>
                        </div>
                        <div class="flex items-center justify-between">
                          <span class="text-zinc-400">Lab</span>
                          <span class="text-zinc-300 truncate max-w-[90px]">{{ group.group_name }}</span>
                        </div>
                      </div>
                    </div>
                  </div>

                  <!-- Empty state for groups without PCs -->
                  <div 
                    v-if="getDevicesForGroup(group.group_id).length === 0"
                    class="py-2 px-3 rounded border border-dashed border-zinc-800 text-center"
                  >
                    <span class="text-[10px] text-zinc-400 italic">No PCs configured in this lab</span>
                  </div>

                </div>

              </div>

            </div>
          </div>

        </div>

        <!-- ================================================================= -->
        <!-- MODE 2: PURE DEVICE-TO-DEVICE HIERARCHY (PARENT-CHILD INTERCONNECT)-->
        <!-- ================================================================= -->
        <div v-else class="p-10 inline-flex flex-col space-y-10 items-center min-w-full">
          
          <!-- Layer 0: Core Backbone Gateway / Routers -->
          <div class="flex flex-col items-center">
            <div class="text-[11px] font-medium text-zinc-400 uppercase tracking-wider mb-4 flex items-center gap-2 font-mono">
              <span class="w-3 h-px bg-zinc-700"></span>
              <span>1. Core Gateway</span>
              <span class="w-3 h-px bg-zinc-700"></span>
            </div>

            <div class="flex items-center space-x-12">
              <div 
                v-for="router in coreRouters"
                :key="router.device_id"
                class="relative group cursor-pointer flex flex-col items-center"
                @click.stop="emit('inspect', router.device_id)"
              >
                <!-- Icon Box -->
                <div 
                  class="w-13 h-13 p-3 rounded-2xl flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow-lg relative backdrop-blur-sm"
                  :class="[
                    getNodeStatusColor(router.reachability_status, router.is_in_maintenance).border,
                    getNodeStatusColor(router.reachability_status, router.is_in_maintenance).bg,
                    getNodeStatusColor(router.reachability_status, router.is_in_maintenance).glow
                  ]"
                >
                  <Router 
                    class="w-7 h-7 transition-colors"
                    :class="getNodeStatusColor(router.reachability_status, router.is_in_maintenance).icon"
                  />
                  <span 
                    class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full border border-zinc-950"
                    :class="getNodeStatusColor(router.reachability_status, router.is_in_maintenance).dot"
                  ></span>
                </div>

                <!-- ONLY Device Name on Canvas -->
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

          <!-- Layer 1: Distribution / Aggregate Switches & Direct Core Endpoints -->
          <div class="flex flex-col items-center w-full">
            <div class="text-[11px] font-medium text-zinc-400 uppercase tracking-wider mb-4 flex items-center gap-2 font-mono">
              <span class="w-3 h-px bg-zinc-700"></span>
              <span>2. Distribution Switches &amp; Core Infrastructure</span>
              <span class="w-3 h-px bg-zinc-700"></span>
            </div>

            <!-- Distribution Switches Row -->
            <div class="flex items-start justify-center flex-wrap gap-16 w-full">
              
              <div 
                v-for="sw in aggSwitches"
                :key="sw.device_id"
                class="flex flex-col items-center"
              >
                <!-- Agg Switch Node -->
                <div 
                  class="relative group cursor-pointer flex flex-col items-center"
                  @click.stop="emit('inspect', sw.device_id)"
                >
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

                  <!-- ONLY Device Name on Canvas -->
                  <span class="text-xs font-medium text-zinc-200 group-hover:text-zinc-100 truncate max-w-[120px] text-center mt-2 transition tracking-tight">
                    {{ sw.display_name }}
                  </span>

                  <!-- Hover Card -->
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

                <!-- Downlink from Agg Switch to its Connected Access Switches & Endpoints -->
                <div v-if="getAccessSwitchesForAgg(sw.device_id).length > 0 || getEndpointsForSwitch(sw.device_id).length > 0" class="flex flex-col items-center mt-3">
                  <div class="w-px h-6 bg-zinc-700"></div>

                  <!-- Access Switches linked to this Agg Switch -->
                  <div class="flex items-start justify-center gap-10 mt-2">
                    <div 
                      v-for="accSw in getAccessSwitchesForAgg(sw.device_id)"
                      :key="accSw.device_id"
                      class="flex flex-col items-center"
                    >
                      <!-- Access Switch -->
                      <div 
                        class="relative group cursor-pointer flex flex-col items-center"
                        @click.stop="emit('inspect', accSw.device_id)"
                      >
                        <div 
                          class="w-11 h-11 p-2 rounded-xl flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow-lg relative backdrop-blur-sm"
                          :class="[
                            getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).border,
                            getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).bg,
                            getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).glow
                          ]"
                        >
                          <ArrowLeftRight 
                            class="w-5 h-5 transition-colors"
                            :class="getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).icon"
                          />
                          <span 
                            class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full border border-zinc-950"
                            :class="getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).dot"
                          ></span>
                        </div>
                        <span class="text-[11px] font-medium text-zinc-200 group-hover:text-zinc-100 truncate max-w-[100px] text-center mt-1.5 transition tracking-tight">
                          {{ accSw.display_name }}
                        </span>

                        <!-- Hover Card -->
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

                      <!-- Endpoints under this Access Switch -->
                      <div v-if="getEndpointsForSwitch(accSw.device_id).length > 0" class="flex flex-col items-center mt-2">
                        <div class="w-px h-5 bg-zinc-700"></div>
                        <div class="flex items-start justify-center gap-4 mt-1">
                          <div 
                            v-for="ep in getEndpointsForSwitch(accSw.device_id)"
                            :key="ep.device_id"
                            class="relative group cursor-pointer flex flex-col items-center"
                            @click.stop="emit('inspect', ep.device_id)"
                          >
                            <div 
                              class="w-9 h-9 rounded-lg flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow relative"
                              :class="[
                                getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).border,
                                getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).bg,
                                getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).glow
                              ]"
                            >
                              <component 
                                :is="getDeviceIcon(ep.type_id)" 
                                class="w-4 h-4 transition-colors"
                                :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).icon"
                              />
                              <span 
                                class="absolute -top-0.5 -right-0.5 w-2 h-2 rounded-full border border-zinc-950"
                                :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).dot"
                              ></span>
                            </div>
                            <span class="text-[10px] font-medium text-zinc-300 group-hover:text-zinc-100 truncate max-w-[75px] text-center mt-1 transition">
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

                    <!-- Direct Endpoints connected to this Agg Switch -->
                    <div 
                      v-for="ep in getEndpointsForSwitch(sw.device_id)"
                      :key="ep.device_id"
                      class="flex flex-col items-center"
                    >
                      <div 
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
                        <span class="text-[10px] font-medium text-zinc-300 group-hover:text-zinc-100 truncate max-w-[80px] text-center mt-1 transition">
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

              </div>

              <!-- Direct Endpoints attached to Core Routers (e.g. Servers) -->
              <div 
                v-for="srv in coreEndpoints"
                :key="srv.device_id"
                class="flex flex-col items-center"
              >
                <div 
                  class="relative group cursor-pointer flex flex-col items-center"
                  @click.stop="emit('inspect', srv.device_id)"
                >
                  <div 
                    class="w-12 h-12 p-2.5 rounded-xl flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow-lg relative backdrop-blur-sm"
                    :class="[
                      getNodeStatusColor(srv.reachability_status, srv.is_in_maintenance).border,
                      getNodeStatusColor(srv.reachability_status, srv.is_in_maintenance).bg,
                      getNodeStatusColor(srv.reachability_status, srv.is_in_maintenance).glow
                    ]"
                  >
                    <Server 
                      class="w-6 h-6 transition-colors"
                      :class="getNodeStatusColor(srv.reachability_status, srv.is_in_maintenance).icon"
                    />
                    <span 
                      class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full border border-zinc-950"
                      :class="getNodeStatusColor(srv.reachability_status, srv.is_in_maintenance).dot"
                    ></span>
                  </div>
                  <span class="text-xs font-medium text-zinc-200 group-hover:text-zinc-100 truncate max-w-[120px] text-center mt-2 transition tracking-tight">
                    {{ srv.display_name }}
                  </span>

                  <!-- Hover Card -->
                  <div 
                    class="opacity-0 pointer-events-none group-hover:opacity-100 transition-opacity duration-150 absolute bottom-full mb-2 z-50 w-48 p-2.5 rounded-lg bg-zinc-900 border border-zinc-700 shadow-2xl backdrop-blur-md text-left font-sans"
                  >
                    <div class="flex items-center justify-between gap-1 pb-1.5 border-b border-zinc-800">
                      <span class="font-semibold text-xs text-zinc-100 truncate">{{ srv.display_name }}</span>
                      <span 
                        class="px-1.5 py-0.5 rounded text-[10px] font-semibold border font-mono"
                        :class="getNodeStatusColor(srv.reachability_status, srv.is_in_maintenance).badgeClass"
                      >
                        {{ getNodeStatusColor(srv.reachability_status, srv.is_in_maintenance).badgeText }}
                      </span>
                    </div>
                    <div class="pt-1.5 space-y-1 text-[11px]">
                      <div class="flex items-center justify-between">
                        <span class="text-zinc-400">IP</span>
                        <span class="font-mono text-zinc-200 font-medium">{{ srv.primary_ip }}</span>
                      </div>
                      <div class="flex items-center justify-between">
                        <span class="text-zinc-400">Type</span>
                        <span class="text-zinc-300">Server</span>
                      </div>
                      <div v-if="srv.zone" class="flex items-center justify-between">
                        <span class="text-zinc-400">Zone</span>
                        <span class="text-zinc-300 truncate max-w-[90px]">{{ srv.zone }}</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

            </div>

            <!-- Unparented Access Switches (if any) -->
            <div v-if="unparentedAccessSwitches.length > 0" class="flex flex-col items-center mt-8">
              <div class="text-[10px] font-medium text-zinc-400 uppercase tracking-wider mb-3 font-mono">
                Direct Lab Access Switches
              </div>
              <div class="flex items-start justify-center gap-10">
                <div 
                  v-for="accSw in unparentedAccessSwitches"
                  :key="accSw.device_id"
                  class="flex flex-col items-center"
                >
                  <div 
                    class="relative group cursor-pointer flex flex-col items-center"
                    @click.stop="emit('inspect', accSw.device_id)"
                  >
                    <div 
                      class="w-11 h-11 p-2 rounded-xl flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow-lg relative backdrop-blur-sm"
                      :class="[
                        getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).border,
                        getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).bg,
                        getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).glow
                      ]"
                    >
                      <ArrowLeftRight 
                        class="w-5 h-5 transition-colors"
                        :class="getNodeStatusColor(accSw.reachability_status, accSw.is_in_maintenance).icon"
                      />
                    </div>
                    <span class="text-[11px] font-medium text-zinc-200 group-hover:text-zinc-100 truncate max-w-[100px] text-center mt-1.5 transition tracking-tight">
                      {{ accSw.display_name }}
                    </span>
                  </div>

                  <!-- Endpoints under this switch -->
                  <div v-if="getEndpointsForSwitch(accSw.device_id).length > 0" class="flex flex-col items-center mt-2">
                    <div class="w-px h-5 bg-zinc-700"></div>
                    <div class="flex items-start justify-center gap-4 mt-1">
                      <div 
                        v-for="ep in getEndpointsForSwitch(accSw.device_id)"
                        :key="ep.device_id"
                        class="relative group cursor-pointer flex flex-col items-center"
                        @click.stop="emit('inspect', ep.device_id)"
                      >
                        <div 
                          class="w-9 h-9 rounded-lg flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow relative"
                          :class="[
                            getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).border,
                            getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).bg,
                            getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).glow
                          ]"
                        >
                          <component 
                            :is="getDeviceIcon(ep.type_id)" 
                            class="w-4 h-4 transition-colors"
                            :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).icon"
                          />
                        </div>
                        <span class="text-[10px] font-medium text-zinc-300 group-hover:text-zinc-100 truncate max-w-[75px] text-center mt-1 transition">
                          {{ ep.display_name }}
                        </span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Standalone / Unparented Endpoints -->
            <div v-if="unparentedEndpoints.length > 0" class="flex flex-col items-center mt-8">
              <div class="p-3 rounded-lg border border-dashed border-zinc-800 text-center">
                <div class="text-[10px] font-medium text-zinc-400 uppercase tracking-wider mb-2 font-mono">
                  Standalone / Unparented Workstations
                </div>
                <div class="flex flex-wrap gap-4 justify-center max-w-sm">
                  <div 
                    v-for="ep in unparentedEndpoints"
                    :key="ep.device_id"
                    class="relative group cursor-pointer flex flex-col items-center"
                    @click.stop="emit('inspect', ep.device_id)"
                  >
                    <div 
                      class="w-9 h-9 rounded-lg flex items-center justify-center border-2 transition-all duration-200 group-hover:scale-110 shadow"
                      :class="[
                        getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).border,
                        getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).bg,
                        getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).glow
                      ]"
                    >
                      <component 
                        :is="getDeviceIcon(ep.type_id)" 
                        class="w-4 h-4 transition-colors"
                        :class="getNodeStatusColor(ep.reachability_status, ep.is_in_maintenance).icon"
                      />
                    </div>
                    <span class="text-[10px] font-medium text-zinc-300 group-hover:text-zinc-100 truncate max-w-[75px] text-center mt-1">
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

            <!-- Fallback if no equipment at all -->
            <div 
              v-if="allDevices.length === 0"
              class="p-6 rounded-lg border border-dashed border-zinc-800 text-center max-w-md"
            >
              <Network class="w-6 h-6 text-zinc-400 mx-auto mb-2" />
              <p class="text-xs text-zinc-300 font-medium">No Devices Configured</p>
              <p class="text-[11px] text-zinc-400 mt-0.5">Use the Device Inventory or Subnet Discovery tab to populate equipment.</p>
            </div>

          </div>

        </div>

      </div>
    </div>
  </div>
</template>
