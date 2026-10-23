<script setup lang="ts">
import { ref, onMounted, onUnmounted, shallowRef, watch } from 'vue';
import { 
  Activity, 
  ShieldCheck, 
  RefreshCw, 
  LogOut, 
  UserCheck,
  Network,
  List,
  Layers,
  Radar,
  FileSpreadsheet,
  Settings,
  KeyRound
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
  // 5s periodic background refresh keeping HUD and topology badges live
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
  <div v-if="!authStore.isInitialized" class="min-h-screen bg-tactical-bg flex items-center justify-center font-mono">
    <div class="text-center space-y-3">
      <Activity class="w-8 h-8 text-tactical-cyan mx-auto animate-spin" />
      <div class="text-xs tracking-widest text-slate-400 uppercase">
        INITIALIZING ENCLAVE SECURITY SUBSYSTEM...
      </div>
    </div>
  </div>

  <!-- Pre-Dashboard Cyber-Tactical Login Portal -->
  <LoginView v-else-if="!authStore.isAuthenticated" />

  <!-- Authenticated Operations Workspace -->
  <div v-else class="min-h-screen bg-tactical-bg text-slate-100 flex flex-col font-sans select-none overflow-x-hidden">
    
    <!-- Top Tactical Navigation Bar -->
    <header class="h-14 border-b border-tactical-border bg-tactical-surface px-6 flex items-center justify-between sticky top-0 z-30">
      <div class="flex items-center space-x-3">
        <div class="w-8 h-8 rounded bg-tactical-panel border border-tactical-cyan/40 flex items-center justify-center text-tactical-cyan shadow-[0_0_12px_rgba(0,240,255,0.2)]">
          <Activity class="w-5 h-5 animate-pulse" />
        </div>
        <div>
          <h1 class="text-sm font-semibold tracking-wider uppercase text-slate-100 flex items-center gap-2">
            Network Observer
            <span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-tactical-panel border border-tactical-border text-tactical-cyan">
              v2.2-CAMPUS
            </span>
          </h1>
          <p class="text-[10px] font-mono text-slate-400">UNIVERSITY & ENTERPRISE COMPUTER LAB NOC</p>
        </div>
      </div>

      <!-- Center Status & RBAC Pill -->
      <div class="hidden md:flex items-center space-x-5 text-xs font-mono">
        <div class="flex items-center space-x-2">
          <span class="relative flex h-2 w-2">
            <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-tactical-online opacity-75"></span>
            <span class="relative inline-flex rounded-full h-2 w-2 bg-tactical-online"></span>
          </span>
          <span class="text-slate-300">TELEMETRY: ACTIVE (5s)</span>
        </div>
        <div class="text-slate-600">|</div>
        <div class="flex items-center space-x-1.5 text-slate-300">
          <UserCheck class="w-4 h-4 text-tactical-cyan" />
          <span class="text-slate-400 font-semibold">{{ authStore.username }}</span>
          <span class="text-[10px] px-1.5 py-0.5 rounded bg-tactical-panel border border-tactical-cyan/40 text-tactical-cyan uppercase">
            {{ authStore.roleName }}
          </span>
        </div>
      </div>

      <!-- Right Controls: Ping, Password Change & Logout -->
      <div class="flex items-center space-x-2.5">
        <button
          @click="checkApiHealth"
          :disabled="isLoadingHealth"
          class="flex items-center space-x-1.5 px-2.5 py-1.5 rounded text-xs font-mono bg-tactical-panel border border-tactical-border hover:border-tactical-cyan text-slate-200 hover:text-tactical-cyan transition"
          title="Query Backend Health Endpoint"
        >
          <RefreshCw class="w-3.5 h-3.5" :class="{ 'animate-spin': isLoadingHealth }" />
          <span class="hidden sm:inline">PING API</span>
        </button>

        <button
          @click="isPasswordModalOpen = true"
          class="flex items-center space-x-1.5 px-2.5 py-1.5 rounded text-xs font-mono bg-tactical-panel border border-tactical-border hover:border-tactical-cyan text-slate-300 hover:text-tactical-cyan transition"
          title="Update Passphrase"
        >
          <KeyRound class="w-3.5 h-3.5" />
          <span class="hidden sm:inline">SECURITY</span>
        </button>

        <button
          @click="handleLogout"
          class="flex items-center space-x-1.5 px-2.5 py-1.5 rounded text-xs font-mono bg-tactical-panel border border-tactical-border hover:border-tactical-alert/60 text-slate-300 hover:text-tactical-alert transition"
        >
          <LogOut class="w-3.5 h-3.5" />
          <span>LOGOUT</span>
        </button>
      </div>
    </header>

    <!-- Top HUD Metric Banner (5 Tactical Cards) -->
    <div class="px-6 pt-5 pb-2">
      <HudMetricBanner @filter-status="filterByStatusFromHud" />
    </div>

    <!-- View Navigation Tab Bar -->
    <div class="px-6 pt-3 pb-2 flex items-center space-x-2 overflow-x-auto text-xs font-mono select-none">
      <button
        @click="currentTab = 'TOPOLOGY'"
        class="flex items-center space-x-2 px-3.5 py-2 rounded-lg border transition whitespace-nowrap"
        :class="currentTab === 'TOPOLOGY'
          ? 'bg-tactical-cyan/15 border-tactical-cyan text-tactical-cyan font-bold shadow-[0_0_12px_rgba(0,240,255,0.2)]'
          : 'bg-tactical-surface border-tactical-border text-slate-400 hover:text-slate-200 hover:border-slate-700'"
      >
        <Network class="w-4 h-4" />
        <span>Logical Topology Canvas</span>
      </button>

      <button
        @click="currentTab = 'INVENTORY'"
        class="flex items-center space-x-2 px-3.5 py-2 rounded-lg border transition whitespace-nowrap"
        :class="currentTab === 'INVENTORY'
          ? 'bg-tactical-cyan/15 border-tactical-cyan text-tactical-cyan font-bold shadow-[0_0_12px_rgba(0,240,255,0.2)]'
          : 'bg-tactical-surface border-tactical-border text-slate-400 hover:text-slate-200 hover:border-slate-700'"
      >
        <List class="w-4 h-4" />
        <span>Device Inventory ({{ devicesStore.totalCount }})</span>
      </button>

      <button
        @click="currentTab = 'LAB_GROUPS'"
        class="flex items-center space-x-2 px-3.5 py-2 rounded-lg border transition whitespace-nowrap"
        :class="currentTab === 'LAB_GROUPS'
          ? 'bg-tactical-cyan/15 border-tactical-cyan text-tactical-cyan font-bold shadow-[0_0_12px_rgba(0,240,255,0.2)]'
          : 'bg-tactical-surface border-tactical-border text-slate-400 hover:text-slate-200 hover:border-slate-700'"
      >
        <Layers class="w-4 h-4" />
        <span>Computer Labs & Hierarchy</span>
      </button>

      <button
        @click="currentTab = 'SUBNET_SWEEP'"
        class="flex items-center space-x-2 px-3.5 py-2 rounded-lg border transition whitespace-nowrap"
        :class="currentTab === 'SUBNET_SWEEP'
          ? 'bg-tactical-cyan/15 border-tactical-cyan text-tactical-cyan font-bold shadow-[0_0_12px_rgba(0,240,255,0.2)]'
          : 'bg-tactical-surface border-tactical-border text-slate-400 hover:text-slate-200 hover:border-slate-700'"
      >
        <Radar class="w-4 h-4" />
        <span>Subnet Sweep Discovery</span>
      </button>

      <button
        @click="currentTab = 'BULK_INGEST'"
        class="flex items-center space-x-2 px-3.5 py-2 rounded-lg border transition whitespace-nowrap"
        :class="currentTab === 'BULK_INGEST'
          ? 'bg-tactical-cyan/15 border-tactical-cyan text-tactical-cyan font-bold shadow-[0_0_12px_rgba(0,240,255,0.2)]'
          : 'bg-tactical-surface border-tactical-border text-slate-400 hover:text-slate-200 hover:border-slate-700'"
      >
        <FileSpreadsheet class="w-4 h-4" />
        <span>Bulk Ingestion Pipeline</span>
      </button>

      <button
        @click="currentTab = 'AUDIT_TRAIL'"
        class="flex items-center space-x-2 px-3.5 py-2 rounded-lg border transition whitespace-nowrap"
        :class="currentTab === 'AUDIT_TRAIL'
          ? 'bg-tactical-cyan/15 border-tactical-cyan text-tactical-cyan font-bold shadow-[0_0_12px_rgba(0,240,255,0.2)]'
          : 'bg-tactical-surface border-tactical-border text-slate-400 hover:text-slate-200 hover:border-slate-700'"
      >
        <ShieldCheck class="w-4 h-4" />
        <span>Security Audit Trail</span>
      </button>

      <button
        @click="currentTab = 'SETTINGS'"
        class="flex items-center space-x-2 px-3.5 py-2 rounded-lg border transition whitespace-nowrap"
        :class="currentTab === 'SETTINGS'
          ? 'bg-tactical-cyan/15 border-tactical-cyan text-tactical-cyan font-bold shadow-[0_0_12px_rgba(0,240,255,0.2)]'
          : 'bg-tactical-surface border-tactical-border text-slate-400 hover:text-slate-200 hover:border-slate-700'"
      >
        <Settings class="w-4 h-4" />
        <span>Enclave & Poller Settings</span>
      </button>
    </div>

    <!-- Main Dynamic Workspace View -->
    <main class="flex-1 px-6 pb-6 pt-2 flex flex-col min-h-[500px]">
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
