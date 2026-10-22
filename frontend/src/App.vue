<script setup lang="ts">
import { ref, onMounted, shallowRef } from 'vue';
import { 
  Activity, 
  ShieldCheck, 
  Server, 
  Radio, 
  Cpu, 
  RefreshCw, 
  LogOut, 
  UserCheck 
} from 'lucide-vue-next';
import axios from 'axios';
import { useAuthStore } from './stores/auth';
import LoginView from './features/auth/LoginView.vue';

interface ApiHealthResponse {
  status: string;
  version: string;
  timestamp: string;
}

const authStore = useAuthStore();
const apiHealth = shallowRef<ApiHealthResponse | null>(null);
const isLoadingHealth = ref<boolean>(false);
const errorMsg = ref<string | null>(null);

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
  await authStore.logout();
}

onMounted(async () => {
  await authStore.checkAuth();
  await checkApiHealth();
});
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
  <div v-else class="min-h-screen bg-tactical-bg text-slate-100 flex flex-col font-sans select-none">
    
    <!-- Top Tactical Navigation Bar -->
    <header class="h-14 border-b border-tactical-border bg-tactical-surface px-6 flex items-center justify-between">
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
      <div class="flex items-center space-x-5 text-xs font-mono">
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

      <!-- Right Controls: Ping & Logout -->
      <div class="flex items-center space-x-3">
        <button
          @click="checkApiHealth"
          :disabled="isLoadingHealth"
          class="flex items-center space-x-1.5 px-3 py-1.5 rounded text-xs font-mono bg-tactical-panel border border-tactical-border hover:border-tactical-cyan text-slate-200 hover:text-tactical-cyan transition"
        >
          <RefreshCw class="w-3.5 h-3.5" :class="{ 'animate-spin': isLoadingHealth }" />
          <span>PING API</span>
        </button>

        <button
          @click="handleLogout"
          class="flex items-center space-x-1.5 px-3 py-1.5 rounded text-xs font-mono bg-tactical-panel border border-tactical-border hover:border-tactical-alert/60 text-slate-300 hover:text-tactical-alert transition"
        >
          <LogOut class="w-3.5 h-3.5" />
          <span>LOGOUT</span>
        </button>
      </div>
    </header>

    <!-- Top HUD Telemetry Cards -->
    <section class="p-6 grid grid-cols-1 md:grid-cols-4 gap-4">
      <div class="p-4 rounded-lg bg-tactical-surface border border-tactical-border relative overflow-hidden">
        <div class="text-[11px] font-mono uppercase tracking-wider text-slate-400 flex items-center justify-between">
          <span>Backend API Status</span>
          <Server class="w-4 h-4 text-tactical-cyan" />
        </div>
        <div class="mt-2 flex items-baseline space-x-2">
          <span
            v-if="apiHealth"
            class="text-2xl font-mono font-bold text-tactical-online"
          >
            {{ apiHealth.status }}
          </span>
          <span v-else-if="isLoadingHealth" class="text-2xl font-mono font-bold text-tactical-amber animate-pulse">
            CHECKING...
          </span>
          <span v-else class="text-2xl font-mono font-bold text-tactical-alert">
            OFFLINE
          </span>
          <span v-if="apiHealth" class="text-xs font-mono text-slate-400">
            (ver {{ apiHealth.version }})
          </span>
        </div>
        <div class="mt-1 text-[10px] font-mono text-slate-500">
          Reverse Proxy: localhost:5000/api
        </div>
      </div>

      <div class="p-4 rounded-lg bg-tactical-surface border border-tactical-border">
        <div class="text-[11px] font-mono uppercase tracking-wider text-slate-400 flex items-center justify-between">
          <span>Active Session</span>
          <ShieldCheck class="w-4 h-4 text-tactical-online" />
        </div>
        <div class="mt-2 text-2xl font-mono font-bold text-slate-100 flex items-baseline gap-2">
          <span>{{ authStore.username }}</span>
          <span class="text-xs font-normal text-tactical-cyan font-mono">ID: #{{ authStore.user?.user_id }}</span>
        </div>
        <div class="mt-1 text-[10px] font-mono text-slate-500">
          Role: {{ authStore.roleName }} (Site #{{ authStore.user?.site_id }})
        </div>
      </div>

      <div class="p-4 rounded-lg bg-tactical-surface border border-tactical-border">
        <div class="text-[11px] font-mono uppercase tracking-wider text-slate-400 flex items-center justify-between">
          <span>Visual Engines</span>
          <Cpu class="w-4 h-4 text-slate-400" />
        </div>
        <div class="mt-2 text-sm font-mono text-slate-100 space-y-0.5">
          <div class="text-tactical-cyan">Cytoscape.js (Topology)</div>
          <div class="text-tactical-amber">Apache ECharts (HUD)</div>
        </div>
        <div class="mt-1 text-[10px] font-mono text-slate-500">
          Multi-layer HTML5 Canvas + markRaw()
        </div>
      </div>

      <div class="p-4 rounded-lg bg-tactical-surface border border-tactical-border">
        <div class="text-[11px] font-mono uppercase tracking-wider text-slate-400 flex items-center justify-between">
          <span>RAM Allowance Budget</span>
          <Activity class="w-4 h-4 text-tactical-cyan" />
        </div>
        <div class="mt-2 text-2xl font-mono font-bold text-tactical-cyan">
          500 <span class="text-xs font-normal text-slate-400 font-sans">MB</span>
        </div>
        <div class="mt-1 text-[10px] font-mono text-slate-500">
          64MB Argon2id + shallowRef() batches
        </div>
      </div>
    </section>

    <!-- Main Workspace Area -->
    <main class="flex-1 px-6 pb-6 flex gap-4">
      <!-- Topology Canvas Area -->
      <div class="flex-1 rounded-lg bg-tactical-surface border border-tactical-border flex flex-col p-4">
        <div class="flex items-center justify-between pb-3 border-b border-tactical-border">
          <div class="flex items-center space-x-2">
            <span class="text-xs font-mono font-semibold uppercase tracking-wider text-slate-300">
              Logical Topology Tree (Cytoscape.js)
            </span>
            <span class="px-1.5 py-0.5 text-[9px] font-mono rounded bg-tactical-panel border border-tactical-border text-tactical-cyan">
              2-BRANCH PARALLEL
            </span>
          </div>
          <div class="text-[10px] font-mono text-slate-500">
            Phase 3 Inventory Ready
          </div>
        </div>
        <div class="flex-1 flex items-center justify-center border border-dashed border-tactical-border/60 rounded mt-4 min-h-[350px] bg-tactical-bg/40">
          <div class="text-center space-y-2">
            <Radio class="w-8 h-8 text-tactical-cyan/40 mx-auto animate-pulse" />
            <div class="text-xs font-mono text-slate-400">Cytoscape 60fps Canvas Container Ready</div>
            <div class="text-[10px] font-mono text-slate-600">Core Router &rarr; Distribution &rarr; Access Switches &rarr; Leaf Endpoints</div>
          </div>
        </div>
      </div>

      <!-- Right Inspector Panel -->
      <aside class="w-80 rounded-lg bg-tactical-surface border border-tactical-border flex flex-col p-4">
        <div class="pb-3 border-b border-tactical-border">
          <span class="text-xs font-mono font-semibold uppercase tracking-wider text-slate-300">
            Telemetry Inspector
          </span>
        </div>
        <div class="flex-1 flex items-center justify-center border border-dashed border-tactical-border/60 rounded mt-4 bg-tactical-bg/40">
          <div class="text-center space-y-2 p-4">
            <Activity class="w-6 h-6 text-tactical-amber/50 mx-auto" />
            <div class="text-xs font-mono text-slate-400">Apache ECharts Ready</div>
            <div class="text-[10px] font-mono text-slate-600">Microsecond RTT & Clock Drift Graphing</div>
          </div>
        </div>
      </aside>
    </main>
  </div>
</template>
