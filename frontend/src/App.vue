<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, shallowRef, watch } from 'vue';
import { 
  Activity, 
  ShieldCheck, 
  RefreshCw, 
  LogOut, 
  Network, 
  List, 
  Layers, 
  Radar, 
  FileSpreadsheet, 
  Settings, 
  KeyRound, 
  Plus 
} from 'lucide-vue-next';
import axios from 'axios';
import { useAuthStore } from './stores/auth';
import { useDevicesStore } from './stores/devices';
import { useGroupsStore } from './stores/groups';
import { useConfigStore } from './stores/config';
import LoginView from './features/auth/LoginView.vue';
import HudMetricBanner from './features/telemetry/HudMetricBanner.vue';
import TopologyView from './features/topology/TopologyView.vue';
import DeviceInventoryView from './features/inventory/DeviceInventoryView.vue';
import GroupHierarchyView from './features/settings/GroupHierarchyView.vue';
import SubnetDiscoveryView from './features/discovery/SubnetDiscoveryView.vue';
import BulkIngestView from './features/inventory/BulkIngestView.vue';
import AuditTrailView from './features/audit/AuditTrailView.vue';
import EnclaveSettingsView from './features/settings/EnclaveSettingsView.vue';
import TelemetryInspector from './features/telemetry/TelemetryInspector.vue';
import DeviceModal from './features/inventory/DeviceModal.vue';
import DeleteConfirmModal from './features/inventory/DeleteConfirmModal.vue';
import GroupModal from './features/settings/GroupModal.vue';
import BatchCommitModal from './features/inventory/BatchCommitModal.vue';
import PasswordChangeModal from './features/auth/PasswordChangeModal.vue';
import type { Device } from './types/device';
import type { GroupNode } from './types/group';
import type { DiscoveredHost } from './types/discovery';

interface ApiHealthResponse {
  status: string;
  version: string;
  timestamp: string;
}

const authStore = useAuthStore();
const devicesStore = useDevicesStore();
const groupsStore = useGroupsStore();
const configStore = useConfigStore();

const apiHealth = shallowRef<ApiHealthResponse | null>(null);
const isLoadingHealth = ref<boolean>(false);
const errorMsg = ref<string | null>(null);

// Active Tab state
type TabType = 'TOPOLOGY' | 'INVENTORY' | 'LAB_GROUPS' | 'SUBNET_SWEEP' | 'BULK_INGEST' | 'AUDIT_TRAIL' | 'SETTINGS';
const currentTab = ref<TabType>('TOPOLOGY');

const tabTitle = computed(() => {
  switch (currentTab.value) {
    case 'TOPOLOGY': return 'Topology Canvas';
    case 'INVENTORY': return 'Device Inventory';
    case 'LAB_GROUPS': return 'Computer Labs & Hierarchy';
    case 'SUBNET_SWEEP': return 'Subnet Sweep Discovery';
    case 'BULK_INGEST': return 'Bulk Ingestion Pipeline';
    case 'AUDIT_TRAIL': return 'Security Audit Trail';
    case 'SETTINGS': return 'Enclave Settings';
    default: return 'Overview';
  }
});

// Modals state
const isDeviceModalOpen = ref(false);
const deviceToEdit = ref<Device | null>(null);

const isDeleteModalOpen = ref(false);
const deviceToDelete = ref<Device | null>(null);

const isGroupModalOpen = ref(false);
const groupToEdit = ref<GroupNode | null>(null);
const parentGroupForNewSubgroup = ref<GroupNode | null>(null);

const isBatchCommitModalOpen = ref(false);
const isPasswordModalOpen = ref(false);

let pollingTimer: any = null;

async function checkApiHealth() {
  isLoadingHealth.value = true;
  errorMsg.value = null;
  try {
    const res = await axios.get<ApiHealthResponse>('/api/v1/health');
    apiHealth.value = res.data;
  } catch (err: any) {
    errorMsg.value = err.message || 'API connection failed';
  } finally {
    isLoadingHealth.value = false;
  }
}

async function handleLogout() {
  stopAutoRefresh();
  await authStore.logout();
}

function startAutoRefresh() {
  stopAutoRefresh();
  pollingTimer = setInterval(async () => {
    if (authStore.isAuthenticated) {
      await devicesStore.fetchDevices();
    }
  }, 5000);
}

function stopAutoRefresh() {
  if (pollingTimer) {
    clearInterval(pollingTimer);
    pollingTimer = null;
  }
}

watch(() => authStore.isAuthenticated, async (isAuth) => {
  if (isAuth) {
    await Promise.all([
      devicesStore.fetchDevices(),
      groupsStore.fetchGroups(),
      configStore.fetchConfig()
    ]);
    startAutoRefresh();
  } else {
    stopAutoRefresh();
  }
});

onMounted(async () => {
  await authStore.checkAuth();
  await checkApiHealth();
  if (authStore.isAuthenticated) {
    await Promise.all([
      devicesStore.fetchDevices(),
      groupsStore.fetchGroups(),
      configStore.fetchConfig()
    ]);
    startAutoRefresh();
  }
});

onUnmounted(() => {
  stopAutoRefresh();
});

// Modal Actions
function openCreateDeviceModal() {
  deviceToEdit.value = null;
  isDeviceModalOpen.value = true;
}

function openEditDeviceModal(device: Device) {
  deviceToEdit.value = device;
  isDeviceModalOpen.value = true;
}

function openDeleteDeviceModal(device: Device) {
  deviceToDelete.value = device;
  isDeleteModalOpen.value = true;
}

function openCreateTopGroupModal() {
  groupToEdit.value = null;
  parentGroupForNewSubgroup.value = null;
  isGroupModalOpen.value = true;
}

function openCreateSubgroupModal(parentGroup: GroupNode) {
  groupToEdit.value = null;
  parentGroupForNewSubgroup.value = parentGroup;
  isGroupModalOpen.value = true;
}

function openEditGroupModal(group: GroupNode) {
  groupToEdit.value = group;
  parentGroupForNewSubgroup.value = null;
  isGroupModalOpen.value = true;
}

function handleImportDiscoveredHost(host: DiscoveredHost) {
  deviceToEdit.value = {
    device_id: 0,
    display_name: host.hostname || `HOST-${host.ip_address.replace(/\./g, '-')}`,
    primary_ip: host.ip_address,
    mac_address: host.mac_address || '00:00:00:00:00:00',
    type_id: 5, // PC default
    group_id: 1,
    parent_device_id: null,
    zone: 'Discovered Subnet',
    vendor: host.vendor,
    model: null,
    is_monitored: true,
    is_in_maintenance: false,
    created_at: new Date().toISOString(),
    updated_at: new Date().toISOString()
  };
  isDeviceModalOpen.value = true;
}

function filterByStatusFromHud(status: string) {
  devicesStore.selectedStatusFilter = status;
  currentTab.value = 'INVENTORY';
}
</script>

<template>
  <!-- Loading Gate before session resolution -->
  <div v-if="!authStore.isInitialized" class="min-h-screen bg-zinc-950 flex items-center justify-center font-mono">
    <div class="text-center space-y-3">
      <Activity class="w-8 h-8 text-emerald-400 mx-auto animate-spin" />
      <div class="text-xs tracking-widest text-zinc-400 uppercase">
        Initializing Network Observer...
      </div>
    </div>
  </div>

  <!-- Pre-Dashboard Login Portal -->
  <LoginView v-else-if="!authStore.isAuthenticated" />

  <!-- Authenticated Operations Workspace with Sidebar Layout -->
  <div v-else class="h-screen w-screen flex bg-zinc-950 text-zinc-100 font-sans select-none overflow-hidden">
    
    <!-- Left Sidebar (shadcn UI style) -->
    <aside class="w-64 bg-zinc-900/90 border-r border-zinc-800 flex flex-col flex-shrink-0 z-30 justify-between select-none backdrop-blur">
      
      <!-- Top Brand & Navigation -->
      <div class="flex flex-col min-h-0 overflow-y-auto">
        <!-- Brand Header -->
        <div class="h-14 px-4 flex items-center gap-3 border-b border-zinc-800 flex-shrink-0">
          <div class="w-8 h-8 rounded-lg bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400 shadow-sm">
            <Activity class="w-4 h-4 animate-pulse" />
          </div>
          <div class="min-w-0">
            <div class="flex items-center gap-2">
              <span class="font-bold text-sm text-zinc-100 tracking-tight">Observer LAN</span>
              <span class="text-[10px] px-1.5 py-0.2 rounded bg-zinc-800 text-emerald-400 font-mono border border-zinc-700">v2.2</span>
            </div>
            <div class="text-[11px] text-zinc-400 truncate">Computer Labs NOC</div>
          </div>
        </div>

        <!-- Navigation Section -->
        <nav class="p-3 space-y-1">
          <div class="px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-zinc-400">
            Monitoring
          </div>

          <button
            @click="currentTab = 'TOPOLOGY'"
            class="w-full flex items-center gap-3 px-3 py-2 rounded-md text-xs font-medium transition-colors text-left"
            :class="currentTab === 'TOPOLOGY'
              ? 'bg-zinc-800 text-zinc-100 shadow-sm'
              : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
          >
            <Network class="w-4 h-4 text-emerald-400" />
            <span>Topology Canvas</span>
          </button>

          <button
            @click="currentTab = 'INVENTORY'"
            class="w-full flex items-center justify-between px-3 py-2 rounded-md text-xs font-medium transition-colors text-left"
            :class="currentTab === 'INVENTORY'
              ? 'bg-zinc-800 text-zinc-100 shadow-sm'
              : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
          >
            <div class="flex items-center gap-3">
              <List class="w-4 h-4 text-cyan-400" />
              <span>Device Inventory</span>
            </div>
            <span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-zinc-800/80 text-zinc-400 border border-zinc-700/60">
              {{ devicesStore.totalCount }}
            </span>
          </button>

          <button
            @click="currentTab = 'LAB_GROUPS'"
            class="w-full flex items-center gap-3 px-3 py-2 rounded-md text-xs font-medium transition-colors text-left"
            :class="currentTab === 'LAB_GROUPS'
              ? 'bg-zinc-800 text-zinc-100 shadow-sm'
              : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
          >
            <Layers class="w-4 h-4 text-amber-400" />
            <span>Computer Labs</span>
          </button>

          <button
            @click="currentTab = 'SUBNET_SWEEP'"
            class="w-full flex items-center gap-3 px-3 py-2 rounded-md text-xs font-medium transition-colors text-left"
            :class="currentTab === 'SUBNET_SWEEP'
              ? 'bg-zinc-800 text-zinc-100 shadow-sm'
              : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
          >
            <Radar class="w-4 h-4 text-cyan-400" />
            <span>Subnet Discovery</span>
          </button>

          <div class="pt-3 px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-zinc-400">
            Operations
          </div>

          <button
            @click="currentTab = 'BULK_INGEST'"
            class="w-full flex items-center gap-3 px-3 py-2 rounded-md text-xs font-medium transition-colors text-left"
            :class="currentTab === 'BULK_INGEST'
              ? 'bg-zinc-800 text-zinc-100 shadow-sm'
              : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
          >
            <FileSpreadsheet class="w-4 h-4 text-violet-400" />
            <span>Bulk Ingestion</span>
          </button>

          <button
            @click="currentTab = 'AUDIT_TRAIL'"
            class="w-full flex items-center gap-3 px-3 py-2 rounded-md text-xs font-medium transition-colors text-left"
            :class="currentTab === 'AUDIT_TRAIL'
              ? 'bg-zinc-800 text-zinc-100 shadow-sm'
              : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
          >
            <ShieldCheck class="w-4 h-4 text-emerald-400" />
            <span>Audit Trail</span>
          </button>

          <button
            @click="currentTab = 'SETTINGS'"
            class="w-full flex items-center gap-3 px-3 py-2 rounded-md text-xs font-medium transition-colors text-left"
            :class="currentTab === 'SETTINGS'
              ? 'bg-zinc-800 text-zinc-100 shadow-sm'
              : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
          >
            <Settings class="w-4 h-4 text-zinc-400" />
            <span>Enclave Settings</span>
          </button>
        </nav>
      </div>

      <!-- Bottom User Profile & Status Controls -->
      <div class="p-3 border-t border-zinc-800 bg-zinc-900/60 space-y-2 flex-shrink-0">
        <!-- Live Telemetry Status -->
        <div class="px-2 py-1 flex items-center justify-between text-[11px] text-zinc-400 font-mono">
          <div class="flex items-center gap-2">
            <span class="relative flex h-2 w-2">
              <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
              <span class="relative inline-flex rounded-full h-2 w-2 bg-emerald-500"></span>
            </span>
            <span>5s Telemetry Active</span>
          </div>
          <button
            @click="checkApiHealth"
            :disabled="isLoadingHealth"
            class="p-1 rounded hover:bg-zinc-800 text-zinc-400 hover:text-zinc-200 transition"
            title="Refresh Health"
          >
            <RefreshCw class="w-3.5 h-3.5" :class="{ 'animate-spin': isLoadingHealth }" />
          </button>
        </div>

        <!-- User Card -->
        <div class="p-2 rounded-lg bg-zinc-800/60 border border-zinc-700/60 flex items-center justify-between">
          <div class="flex items-center gap-2.5 min-w-0">
            <div class="w-7 h-7 rounded-md bg-zinc-700 flex items-center justify-center font-bold text-xs text-zinc-200 flex-shrink-0">
              {{ (authStore.username || 'A')[0].toUpperCase() }}
            </div>
            <div class="min-w-0 truncate">
              <div class="text-xs font-medium text-zinc-200 truncate">{{ authStore.username }}</div>
              <div class="text-[10px] text-emerald-400 uppercase font-mono">{{ authStore.roleName }}</div>
            </div>
          </div>
          <div class="flex items-center gap-1">
            <button
              @click="isPasswordModalOpen = true"
              class="p-1.5 rounded-md hover:bg-zinc-700 text-zinc-400 hover:text-zinc-200 transition"
              title="Change Passphrase"
            >
              <KeyRound class="w-3.5 h-3.5" />
            </button>
            <button
              @click="handleLogout"
              class="p-1.5 rounded-md hover:bg-rose-950/60 text-zinc-400 hover:text-rose-400 transition"
              title="Log Out"
            >
              <LogOut class="w-3.5 h-3.5" />
            </button>
          </div>
        </div>
      </div>
    </aside>

    <!-- Main Workspace Area -->
    <div class="flex-1 flex flex-col h-screen overflow-hidden min-w-0">
      
      <!-- Top Header Strip (Minimal, clean, un-cluttered) -->
      <header class="h-14 border-b border-zinc-800 bg-zinc-900/60 px-6 flex items-center justify-between flex-shrink-0 backdrop-blur">
        <div class="flex items-center gap-3">
          <h2 class="text-sm font-semibold text-zinc-100 tracking-tight">
            {{ tabTitle }}
          </h2>
          <span class="text-xs text-zinc-400 hidden sm:inline">/</span>
          <span class="text-xs text-zinc-400 hidden sm:inline">Computer Labs & Campus Network</span>
        </div>

        <!-- Action Controls -->
        <div class="flex items-center gap-2.5">
          <button
            v-if="currentTab === 'INVENTORY'"
            @click="openCreateDeviceModal"
            class="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium bg-emerald-600 hover:bg-emerald-500 text-white transition shadow-sm"
          >
            <Plus class="w-3.5 h-3.5" />
            <span>Add Device</span>
          </button>
        </div>
      </header>

      <!-- Minimal HUD Metric Banner Strip -->
      <div class="px-6 pt-3 pb-2 flex-shrink-0">
        <HudMetricBanner @filter-status="filterByStatusFromHud" />
      </div>

      <!-- Dynamic Workspace View Container -->
      <main class="flex-1 px-6 pb-6 pt-1 flex flex-col min-h-0 overflow-auto">
        <TopologyView 
          v-if="currentTab === 'TOPOLOGY'" 
          @inspect="(id) => devicesStore.selectDevice(id)"
        />

        <DeviceInventoryView 
          v-else-if="currentTab === 'INVENTORY'"
          @inspect="(id) => devicesStore.selectDevice(id)"
          @create="openCreateDeviceModal"
          @edit="(d) => openEditDeviceModal(d)"
          @delete="(d) => openDeleteDeviceModal(d)"
        />

        <GroupHierarchyView 
          v-else-if="currentTab === 'LAB_GROUPS'"
          @create-top="openCreateTopGroupModal"
          @create-sub="(parent) => openCreateSubgroupModal(parent)"
          @edit="(g) => openEditGroupModal(g)"
        />

        <SubnetDiscoveryView 
          v-else-if="currentTab === 'SUBNET_SWEEP'"
          @import-host="(h) => handleImportDiscoveredHost(h)"
        />

        <BulkIngestView 
          v-else-if="currentTab === 'BULK_INGEST'"
          @commit="isBatchCommitModalOpen = true"
        />

        <AuditTrailView 
          v-else-if="currentTab === 'AUDIT_TRAIL'"
        />

        <EnclaveSettingsView 
          v-else-if="currentTab === 'SETTINGS'"
        />
      </main>
    </div>

    <!-- Slide-Over Telemetry Inspector (Right Drawer) -->
    <TelemetryInspector 
      v-if="devicesStore.selectedDevice"
      @close="devicesStore.clearSelectedDevice()"
      @edit="(d) => openEditDeviceModal(d)"
      @delete="(d) => openDeleteDeviceModal(d)"
    />

    <!-- 1:1 Schema Create / Edit Device Modal -->
    <DeviceModal
      :is-open="isDeviceModalOpen"
      :device-to-edit="deviceToEdit"
      @close="isDeviceModalOpen = false"
      @saved="devicesStore.fetchDevices()"
    />

    <!-- Orphan-Safe Deletion Dialog -->
    <DeleteConfirmModal
      :is-open="isDeleteModalOpen"
      :device="deviceToDelete"
      @close="isDeleteModalOpen = false"
      @deleted="devicesStore.fetchDevices()"
    />

    <!-- Lab Group Modal -->
    <GroupModal
      :is-open="isGroupModalOpen"
      :group-to-edit="groupToEdit"
      :parent-group-for-new-subgroup="parentGroupForNewSubgroup"
      @close="isGroupModalOpen = false"
      @saved="groupsStore.fetchGroups()"
    />

    <!-- Batch Commit Modal -->
    <BatchCommitModal
      :is-open="isBatchCommitModalOpen"
      @close="isBatchCommitModalOpen = false"
      @committed="devicesStore.fetchDevices()"
    />

    <!-- Passphrase Change Modal -->
    <PasswordChangeModal
      :is-open="isPasswordModalOpen"
      @close="isPasswordModalOpen = false"
    />

  </div>
</template>
