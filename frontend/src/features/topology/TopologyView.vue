<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch, shallowRef } from 'vue';
import cytoscape, { type Core, type ElementsDefinition, type NodeSingular } from 'cytoscape';
import dagre from 'cytoscape-dagre';
import { useDevicesStore } from '../../stores/devices';
import { useGroupsStore } from '../../stores/groups';
import type { Device } from '../../types/device';
import type { GroupNode } from '../../types/group';
import { 
  ZoomIn, 
  ZoomOut, 
  RotateCcw, 
  ArrowLeftRight,
  Network,
  Layers
} from 'lucide-vue-next';

// Register dagre layout plugin with Cytoscape once
if (!cytoscape('core', 'dagre')) {
  cytoscape.use(dagre);
}

const devicesStore = useDevicesStore();
const groupsStore = useGroupsStore();

const emit = defineEmits<{
  (e: 'inspect', deviceId: number): void;
}>();

// Two distinct topology models requested by user:
// 1. ROUTER_GROUPS_PCS: Core Router -> Aggregate Switch -> Computer Lab Groups -> PCs below them
// 2. DEVICE_TO_DEVICE: Core Router -> Aggregate Switch -> Access Switch -> Child Endpoints (Direct links, no titles/groups)
export type TopologyLayoutMode = 'ROUTER_GROUPS_PCS' | 'DEVICE_TO_DEVICE';
const layoutMode = ref<TopologyLayoutMode>('ROUTER_GROUPS_PCS');

// DOM container reference
const cyContainerRef = ref<HTMLDivElement | null>(null);

// Cytoscape Core instance (shallowRef to adhere to strict 500MB memory allowance)
const cy = shallowRef<Core | null>(null);

// Hover Card / Tooltip State
interface HoveredNodeData {
  deviceId?: number;
  displayName: string;
  ip: string;
  mac?: string;
  typeName: string;
  groupName?: string;
  status: 'REACHABLE' | 'UNREACHABLE' | 'UNKNOWN';
  isMaintenance: boolean;
  latencyUs?: number | null;
  lossPct?: number | null;
  x: number;
  y: number;
}
const hoveredNode = ref<HoveredNodeData | null>(null);

// ---------------------------------------------------------------------------
// Helpers & Group Flattener
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

const allDevices = computed<Device[]>(() => {
  return Array.isArray(devicesStore.devices) ? devicesStore.devices : [];
});

function getDeviceTypeName(typeId: number): string {
  switch (typeId) {
    case 1: return 'Core Router';
    case 2: return 'Aggregate Switch';
    case 3: return 'Access Switch';
    case 4: return 'Server / Storage';
    case 5: return 'Workstation PC';
    default: return 'Network Device';
  }
}

function getDevicesForGroup(groupId: number): Device[] {
  const knownGroupIds = new Set(flatGroups.value.map(g => g.group_id));
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

// ---------------------------------------------------------------------------
// Build Cytoscape Elements for Mode 1 & Mode 2
// ---------------------------------------------------------------------------
function buildElements(): ElementsDefinition {
  const nodes: any[] = [];
  const edges: any[] = [];

  const coreRouters = allDevices.value.filter(d => d.type_id === 1);
  const aggSwitches = allDevices.value.filter(d => d.type_id === 2);

  if (layoutMode.value === 'ROUTER_GROUPS_PCS') {
    // =========================================================================
    // MODE 1: Core Router -> Aggregate Switch -> Computer Lab Groups -> PCs
    // =========================================================================

    // 1. Core Router(s)
    if (coreRouters.length > 0) {
      for (const router of coreRouters) {
        nodes.push({
          data: {
            id: `d_${router.device_id}`,
            deviceId: router.device_id,
            label: router.display_name,
            nodeType: 'core-router',
            ip: router.primary_ip,
            mac: router.mac_address,
            typeName: 'Core Router',
            groupName: 'Campus Gateway',
            status: router.reachability_status,
            isMaintenance: router.is_in_maintenance,
            latencyUs: router.latest_latency_us,
            lossPct: router.latest_loss_pct
          }
        });
      }
    } else {
      // Fallback synthetic gateway node if none provisioned yet
      nodes.push({
        data: {
          id: 'gateway_fallback',
          label: 'Default Gateway (Unassigned)',
          nodeType: 'core-router',
          ip: '10.0.0.1',
          typeName: 'Core Gateway',
          status: 'UNKNOWN',
          isMaintenance: false
        }
      });
    }

    // 2. Aggregate Switch(es)
    const primaryRouterId = coreRouters.length > 0 ? `d_${coreRouters[0].device_id}` : 'gateway_fallback';
    if (aggSwitches.length > 0) {
      for (const sw of aggSwitches) {
        nodes.push({
          data: {
            id: `d_${sw.device_id}`,
            deviceId: sw.device_id,
            label: sw.display_name,
            nodeType: 'agg-switch',
            ip: sw.primary_ip,
            mac: sw.mac_address,
            typeName: 'Aggregate Switch',
            groupName: 'Distribution Rack',
            status: sw.reachability_status,
            isMaintenance: sw.is_in_maintenance,
            latencyUs: sw.latest_latency_us,
            lossPct: sw.latest_loss_pct
          }
        });

        // Edge: Core Router -> Agg Switch
        edges.push({
          data: {
            id: `e_${primaryRouterId}_d_${sw.device_id}`,
            source: primaryRouterId,
            target: `d_${sw.device_id}`,
            status: sw.reachability_status
          }
        });
      }
    }

    // Determine upstream feed for groups: first agg switch or fallback to core router
    const groupUplinkId = aggSwitches.length > 0 ? `d_${aggSwitches[0].device_id}` : primaryRouterId;

    // 3. Computer Lab Groups
    for (const group of flatGroups.value) {
      const pcs = getDevicesForGroup(group.group_id);
      const onlineCount = pcs.filter(p => p.reachability_status === 'REACHABLE').length;
      const offlineCount = pcs.filter(p => p.reachability_status === 'UNREACHABLE').length;

      let groupStatus: 'REACHABLE' | 'UNREACHABLE' | 'UNKNOWN' = 'UNKNOWN';
      if (pcs.length > 0) {
        groupStatus = offlineCount === 0 ? 'REACHABLE' : 'UNREACHABLE';
      }

      const groupId = `g_${group.group_id}`;
      nodes.push({
        data: {
          id: groupId,
          label: `${group.group_name} (${pcs.length} PCs)`,
          nodeType: 'group-node',
          groupName: group.group_name,
          pcCount: pcs.length,
          onlineCount,
          offlineCount,
          status: groupStatus,
          isMaintenance: false
        }
      });

      // Edge: Agg Switch -> Group Card
      edges.push({
        data: {
          id: `e_${groupUplinkId}_${groupId}`,
          source: groupUplinkId,
          target: groupId,
          edgeType: 'group-link',
          status: groupStatus
        }
      });

      // 4. PCs belonging to this Lab Group
      for (const pc of pcs) {
        nodes.push({
          data: {
            id: `d_${pc.device_id}`,
            deviceId: pc.device_id,
            label: pc.display_name,
            nodeType: pc.type_id === 4 ? 'server' : 'workstation',
            ip: pc.primary_ip,
            mac: pc.mac_address,
            typeName: getDeviceTypeName(pc.type_id),
            groupName: group.group_name,
            status: pc.reachability_status,
            isMaintenance: pc.is_in_maintenance,
            latencyUs: pc.latest_latency_us,
            lossPct: pc.latest_loss_pct
          }
        });

        // Edge: Group Card -> PC
        edges.push({
          data: {
            id: `e_${groupId}_d_${pc.device_id}`,
            source: groupId,
            target: `d_${pc.device_id}`,
            status: pc.reachability_status
          }
        });
      }
    }

  } else {
    // =========================================================================
    // MODE 2: Pure Device-to-Device Physical Hierarchy (No Titles, Direct Links)
    // =========================================================================
    const deviceIdSet = new Set(allDevices.value.map(d => d.device_id));

    for (const dev of allDevices.value) {
      let nodeType = 'workstation';
      if (dev.type_id === 1) nodeType = 'core-router';
      else if (dev.type_id === 2) nodeType = 'agg-switch';
      else if (dev.type_id === 3) nodeType = 'access-switch';
      else if (dev.type_id === 4) nodeType = 'server';

      nodes.push({
        data: {
          id: `d_${dev.device_id}`,
          deviceId: dev.device_id,
          label: dev.display_name,
          nodeType,
          ip: dev.primary_ip,
          mac: dev.mac_address,
          typeName: getDeviceTypeName(dev.type_id),
          groupName: dev.zone || undefined,
          status: dev.reachability_status,
          isMaintenance: dev.is_in_maintenance,
          latencyUs: dev.latest_latency_us,
          lossPct: dev.latest_loss_pct
        }
      });

      // Direct parent_device_id interconnect
      if (dev.parent_device_id && deviceIdSet.has(dev.parent_device_id)) {
        edges.push({
          data: {
            id: `e_d_${dev.parent_device_id}_d_${dev.device_id}`,
            source: `d_${dev.parent_device_id}`,
            target: `d_${dev.device_id}`,
            status: dev.reachability_status
          }
        });
      }
    }
  }

  return { nodes, edges };
}

// ---------------------------------------------------------------------------
// Cytoscape Cyber-Tactical Stylesheet
// ---------------------------------------------------------------------------
const cytoscapeStylesheet: any[] = [
  // Global Node Base Styling
  {
    selector: 'node',
    style: {
      'label': 'data(label)',
      'color': '#cbd5e1', // slate-300
      'font-size': '10px',
      'font-family': 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace',
      'font-weight': 600,
      'text-valign': 'bottom',
      'text-halign': 'center',
      'text-margin-y': 6,
      'text-outline-color': '#090d16',
      'text-outline-width': 3,
      'text-outline-opacity': 1,
      'background-color': '#0f172a',
      'border-width': 2,
      'border-color': '#334155',
      'transition-property': 'background-color, border-color, width, height',
      'transition-duration': '0.18s'
    }
  },

  // Node Shape Variations by Device Role
  {
    selector: 'node[nodeType = "core-router"]',
    style: {
      'shape': 'diamond',
      'width': 50,
      'height': 50
    }
  },
  {
    selector: 'node[nodeType = "agg-switch"]',
    style: {
      'shape': 'round-rectangle',
      'corner-radius': 8,
      'width': 46,
      'height': 46
    }
  },
  {
    selector: 'node[nodeType = "access-switch"]',
    style: {
      'shape': 'round-rectangle',
      'corner-radius': 6,
      'width': 40,
      'height': 40
    }
  },
  {
    selector: 'node[nodeType = "server"]',
    style: {
      'shape': 'barrel',
      'width': 38,
      'height': 38
    }
  },
  {
    selector: 'node[nodeType = "workstation"]',
    style: {
      'shape': 'ellipse',
      'width': 34,
      'height': 34
    }
  },

  // Computer Lab Group Node (Mode 1)
  {
    selector: 'node[nodeType = "group-node"]',
    style: {
      'shape': 'round-rectangle',
      'corner-radius': 10,
      'width': 140,
      'height': 34,
      'background-color': '#18181b', // zinc-900
      'border-color': '#d97706', // amber-600
      'border-width': 2,
      'text-valign': 'center',
      'text-halign': 'center',
      'text-margin-y': 0,
      'color': '#fde68a', // amber-200
      'font-size': '11px',
      'font-weight': 700
    }
  },

  // Reachability: REACHABLE (Online / Emerald Glow)
  {
    selector: 'node[status = "REACHABLE"]',
    style: {
      'background-color': '#064e3b', // emerald-950
      'border-color': '#10b981', // emerald-500
      'border-width': 2.5,
      'underlay-color': '#10b981',
      'underlay-padding': 4,
      'underlay-opacity': 0.3
    }
  },

  // Reachability: UNREACHABLE (Offline / Rose Red Glow)
  {
    selector: 'node[status = "UNREACHABLE"]',
    style: {
      'background-color': '#4c0519', // rose-950
      'border-color': '#f43f5e', // rose-500
      'border-width': 2.5,
      'underlay-color': '#f43f5e',
      'underlay-padding': 4,
      'underlay-opacity': 0.35
    }
  },

  // Maintenance Override: Amber Glow
  {
    selector: 'node[isMaintenance = true]',
    style: {
      'background-color': '#451a03', // amber-950
      'border-color': '#f59e0b', // amber-500
      'border-width': 2.5,
      'underlay-color': '#f59e0b',
      'underlay-padding': 4,
      'underlay-opacity': 0.35
    }
  },

  // Status: UNKNOWN (Slate)
  {
    selector: 'node[status = "UNKNOWN"]',
    style: {
      'background-color': '#1e293b',
      'border-color': '#64748b',
      'border-width': 2
    }
  },

  // Active / Selected Node (Cyan Ring)
  {
    selector: 'node:selected',
    style: {
      'border-color': '#38bdf8', // sky-400
      'border-width': 4,
      'underlay-color': '#38bdf8',
      'underlay-padding': 6,
      'underlay-opacity': 0.4,
      'color': '#38bdf8'
    }
  },

  // Global Edge (Cable / Trunk)
  {
    selector: 'edge',
    style: {
      'width': 2,
      'line-color': '#334155', // slate-700
      'curve-style': 'taxi',
      'taxi-direction': 'vertical',
      'taxi-turn': 25,
      'target-arrow-shape': 'triangle',
      'target-arrow-color': '#475569',
      'arrow-scale': 0.8,
      'transition-property': 'line-color, target-arrow-color, width',
      'transition-duration': '0.15s'
    }
  },

  // Edge to Reachable Device
  {
    selector: 'edge[status = "REACHABLE"]',
    style: {
      'line-color': '#065f46', // emerald-800
      'target-arrow-color': '#10b981' // emerald-500
    }
  },

  // Edge to Unreachable Device (Dashed Rose Line)
  {
    selector: 'edge[status = "UNREACHABLE"]',
    style: {
      'line-color': '#881337', // rose-900
      'target-arrow-color': '#f43f5e', // rose-500
      'line-style': 'dashed'
    }
  },

  // Edge to Group Card (Mode 1)
  {
    selector: 'edge[edgeType = "group-link"]',
    style: {
      'line-color': '#b45309', // amber-700
      'target-arrow-color': '#f59e0b',
      'width': 2.5
    }
  }
];

// ---------------------------------------------------------------------------
// Cytoscape Lifecycle & Event Handlers
// ---------------------------------------------------------------------------
function initCytoscape() {
  if (!cyContainerRef.value) return;

  // Destroy previous instance cleanly
  if (cy.value) {
    cy.value.destroy();
    cy.value = null;
  }

  const elements = buildElements();

  const instance = cytoscape({
    container: cyContainerRef.value,
    elements,
    style: cytoscapeStylesheet,
    layout: {
      name: 'dagre',
      rankDir: 'TB',
      nodeSep: 60,
      rankSep: 75,
      animate: false
    } as any,
    zoom: 1,
    minZoom: 0.25,
    maxZoom: 3.0,
    wheelSensitivity: 0.22, // Smooth, natural cursor-anchored wheel zooming
    boxSelectionEnabled: false,
    autounselectify: false
  });

  // Fit graph cleanly with margin
  instance.fit(undefined, 50);

  // Click on Device Node -> Emit Inspect Event
  instance.on('tap', 'node', (evt) => {
    const node = evt.target as NodeSingular;
    const deviceId = node.data('deviceId');
    if (deviceId && Number(deviceId) > 0) {
      emit('inspect', Number(deviceId));
    }
  });

  // Hover over Node -> Show Tactical Floating Tooltip
  instance.on('mouseover', 'node', (evt) => {
    const node = evt.target as NodeSingular;
    const pos = node.renderedPosition();
    const data = node.data();

    // Do not show full telemetry card on synthetic fallback
    if (data.id === 'gateway_fallback') return;

    hoveredNode.value = {
      deviceId: data.deviceId,
      displayName: data.label,
      ip: data.ip,
      mac: data.mac,
      typeName: data.typeName || 'Network Node',
      groupName: data.groupName,
      status: data.status || 'UNKNOWN',
      isMaintenance: !!data.isMaintenance,
      latencyUs: data.latencyUs,
      lossPct: data.lossPct,
      x: pos.x,
      y: pos.y
    };
  });

  instance.on('mouseout', 'node', () => {
    hoveredNode.value = null;
  });

  // Tap Background -> Close Hover Card
  instance.on('tap', (evt) => {
    if (evt.target === instance) {
      hoveredNode.value = null;
    }
  });

  cy.value = instance;
}

// Re-render graph when devices, groups or layout mode change
function refreshGraph() {
  if (!cy.value) {
    initCytoscape();
    return;
  }

  const elements = buildElements();
  cy.value.elements().remove();
  cy.value.add(elements);

  const layout = cy.value.layout({
    name: 'dagre',
    rankDir: 'TB',
    nodeSep: layoutMode.value === 'ROUTER_GROUPS_PCS' ? 55 : 65,
    rankSep: 75,
    animate: true,
    animationDuration: 300
  } as any);

  layout.run();
  cy.value.fit(undefined, 50);
}

// ---------------------------------------------------------------------------
// Canvas Controls (Zoom In, Zoom Out, Reset Fit)
// ---------------------------------------------------------------------------
function zoomIn() {
  if (!cy.value || !cyContainerRef.value) return;
  const currentZoom = cy.value.zoom();
  const rect = cyContainerRef.value.getBoundingClientRect();
  cy.value.zoom({
    level: Math.min(3.0, currentZoom * 1.25),
    renderedPosition: { x: rect.width / 2, y: rect.height / 2 }
  });
}

function zoomOut() {
  if (!cy.value || !cyContainerRef.value) return;
  const currentZoom = cy.value.zoom();
  const rect = cyContainerRef.value.getBoundingClientRect();
  cy.value.zoom({
    level: Math.max(0.25, currentZoom * 0.8),
    renderedPosition: { x: rect.width / 2, y: rect.height / 2 }
  });
}

function resetFit() {
  if (!cy.value) return;
  cy.value.fit(undefined, 50);
}

// Watchers
watch(layoutMode, () => {
  refreshGraph();
});

watch(() => devicesStore.devices, () => {
  refreshGraph();
}, { deep: true });

onMounted(async () => {
  if (!groupsStore.groupsTree || groupsStore.groupsTree.length === 0) {
    await groupsStore.fetchGroups();
  }
  initCytoscape();
});

onBeforeUnmount(() => {
  if (cy.value) {
    cy.value.destroy();
    cy.value = null;
  }
});
</script>

<template>
  <div class="flex-1 flex flex-col h-full bg-zinc-950 relative overflow-hidden select-none">
    
    <!-- Cyber-Tactical Canvas HUD Top Bar -->
    <div class="h-12 border-b border-zinc-800 bg-zinc-900/80 px-4 flex items-center justify-between flex-shrink-0 z-20 backdrop-blur">
      
      <!-- Left: Title & Mode Switcher -->
      <div class="flex items-center space-x-4">
        <div class="flex items-center space-x-2">
          <Network class="w-4 h-4 text-emerald-400" />
          <span class="text-xs font-bold text-zinc-100 tracking-wider font-mono">TOPOLOGY CANVAS</span>
          <span class="text-[10px] text-zinc-400 font-mono hidden sm:inline">(Cytoscape.js Engine)</span>
        </div>

        <div class="h-4 w-px bg-zinc-800"></div>

        <!-- Mode Toggle Segmented Pill -->
        <div class="flex items-center bg-zinc-950 p-0.5 rounded-lg border border-zinc-800">
          <button 
            type="button"
            @click="layoutMode = 'ROUTER_GROUPS_PCS'"
            class="px-2.5 py-1 rounded-md text-[11px] font-medium transition flex items-center gap-1.5"
            :class="layoutMode === 'ROUTER_GROUPS_PCS' 
              ? 'bg-emerald-600 text-white shadow-sm font-semibold' 
              : 'text-zinc-400 hover:text-zinc-200'"
            title="Gateway -> Aggregate Switch -> Computer Labs -> Connected Workstations"
          >
            <Layers class="w-3.5 h-3.5" />
            <span>Lab Hierarchy</span>
          </button>

          <button 
            type="button"
            @click="layoutMode = 'DEVICE_TO_DEVICE'"
            class="px-2.5 py-1 rounded-md text-[11px] font-medium transition flex items-center gap-1.5"
            :class="layoutMode === 'DEVICE_TO_DEVICE' 
              ? 'bg-emerald-600 text-white shadow-sm font-semibold' 
              : 'text-zinc-400 hover:text-zinc-200'"
            title="Pure Device-to-Device Physical Interconnects without Group Headers"
          >
            <ArrowLeftRight class="w-3.5 h-3.5" />
            <span>Device-to-Device</span>
          </button>
        </div>
      </div>

      <!-- Right: Legend & Zoom / Fit Controls -->
      <div class="flex items-center space-x-3">
        <!-- Status Indicator Pill -->
        <div class="hidden lg:flex items-center space-x-3 text-[10px] font-mono text-zinc-400 pr-2 border-r border-zinc-800">
          <div class="flex items-center space-x-1">
            <span class="w-2 h-2 rounded-full bg-emerald-500 shadow-[0_0_6px_rgba(16,185,129,0.5)]"></span>
            <span>Online</span>
          </div>
          <div class="flex items-center space-x-1">
            <span class="w-2 h-2 rounded-full bg-rose-500 shadow-[0_0_6px_rgba(244,63,94,0.5)]"></span>
            <span>Offline</span>
          </div>
          <div class="flex items-center space-x-1">
            <span class="w-2 h-2 rounded-full bg-amber-500 shadow-[0_0_6px_rgba(245,158,11,0.5)]"></span>
            <span>Maintenance</span>
          </div>
        </div>

        <!-- Zoom & Fit Controls -->
        <div class="flex items-center space-x-1 bg-zinc-950 p-0.5 rounded-lg border border-zinc-800">
          <button 
            type="button"
            @click="zoomIn"
            class="p-1.5 rounded text-zinc-400 hover:text-zinc-100 hover:bg-zinc-800 transition"
            title="Zoom In (+)"
          >
            <ZoomIn class="w-3.5 h-3.5" />
          </button>
          <button 
            type="button"
            @click="zoomOut"
            class="p-1.5 rounded text-zinc-400 hover:text-zinc-100 hover:bg-zinc-800 transition"
            title="Zoom Out (-)"
          >
            <ZoomOut class="w-3.5 h-3.5" />
          </button>
          <button 
            type="button"
            @click="resetFit"
            class="p-1.5 rounded text-zinc-400 hover:text-zinc-100 hover:bg-zinc-800 transition"
            title="Fit Canvas to View"
          >
            <RotateCcw class="w-3.5 h-3.5" />
          </button>
        </div>
      </div>
    </div>

    <!-- Cytoscape Dedicated Canvas DOM Container -->
    <div 
      ref="cyContainerRef" 
      class="flex-1 w-full h-full bg-[#080b14] relative cursor-grab active:cursor-grabbing outline-none"
    ></div>

    <!-- Floating Cyber-Tactical Tooltip / Hover Card -->
    <div 
      v-if="hoveredNode"
      class="pointer-events-none absolute z-50 w-52 p-3 rounded-xl bg-zinc-900/95 border border-zinc-700 shadow-2xl backdrop-blur-md text-left font-sans -translate-x-1/2 -translate-y-full mb-3"
      :style="{ left: `${hoveredNode.x}px`, top: `${hoveredNode.y - 12}px` }"
    >
      <div class="flex items-center justify-between gap-1 pb-1.5 border-b border-zinc-800">
        <span class="font-semibold text-xs text-zinc-100 truncate">{{ hoveredNode.displayName }}</span>
        <span 
          v-if="hoveredNode.isMaintenance"
          class="px-1.5 py-0.5 rounded text-[9px] font-semibold border bg-amber-950/70 border-amber-500/40 text-amber-300 font-mono"
        >
          MAINTENANCE
        </span>
        <span 
          v-else-if="hoveredNode.status === 'REACHABLE'"
          class="px-1.5 py-0.5 rounded text-[9px] font-semibold border bg-emerald-950/70 border-emerald-500/40 text-emerald-300 font-mono"
        >
          ONLINE
        </span>
        <span 
          v-else-if="hoveredNode.status === 'UNREACHABLE'"
          class="px-1.5 py-0.5 rounded text-[9px] font-semibold border bg-rose-950/70 border-rose-500/40 text-rose-300 font-mono"
        >
          OFFLINE
        </span>
        <span 
          v-else
          class="px-1.5 py-0.5 rounded text-[9px] font-semibold border bg-zinc-800 border-zinc-600 text-zinc-400 font-mono"
        >
          UNKNOWN
        </span>
      </div>

      <div class="pt-2 space-y-1.5 text-[11px]">
        <div class="flex items-center justify-between">
          <span class="text-zinc-400">IP</span>
          <span class="font-mono text-zinc-200 font-medium">{{ hoveredNode.ip }}</span>
        </div>
        <div class="flex items-center justify-between">
          <span class="text-zinc-400">Type</span>
          <span class="text-zinc-300">{{ hoveredNode.typeName }}</span>
        </div>
        <div v-if="hoveredNode.groupName" class="flex items-center justify-between">
          <span class="text-zinc-400">Scope</span>
          <span class="text-zinc-300 truncate max-w-[100px]">{{ hoveredNode.groupName }}</span>
        </div>
        <div v-if="hoveredNode.latencyUs !== null && hoveredNode.latencyUs !== undefined" class="flex items-center justify-between">
          <span class="text-zinc-400">RTT</span>
          <span class="font-mono text-emerald-400 font-medium">
            {{ hoveredNode.latencyUs < 1000 ? `${hoveredNode.latencyUs} µs` : `${(hoveredNode.latencyUs / 1000).toFixed(1)} ms` }}
          </span>
        </div>
        <div v-else class="flex items-center justify-between">
          <span class="text-zinc-400">RTT</span>
          <span class="font-mono text-zinc-500 italic">N/A</span>
        </div>
      </div>
    </div>

    <!-- Empty State Watermark if No Devices -->
    <div 
      v-if="allDevices.length === 0"
      class="pointer-events-none absolute inset-0 flex items-center justify-center"
    >
      <div class="p-6 rounded-2xl bg-zinc-900/80 border border-zinc-800 text-center max-w-sm backdrop-blur">
        <Network class="w-8 h-8 text-zinc-500 mx-auto mb-2" />
        <h3 class="text-sm font-semibold text-zinc-200">No Equipment Discovered</h3>
        <p class="text-xs text-zinc-400 mt-1">Use Subnet Discovery or Device Inventory to onboard infrastructure.</p>
      </div>
    </div>

  </div>
</template>
