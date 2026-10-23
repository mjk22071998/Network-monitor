<script setup lang="ts">
import { ref, watch, onMounted } from 'vue';
import { useConfigStore } from '../../stores/config';
import { useAuthStore } from '../../stores/auth';
import { 
  Settings, 
  ShieldCheck, 
  Sliders, 
  Save, 
  CheckCircle2, 
  AlertCircle 
} from 'lucide-vue-next';

const configStore = useConfigStore();
const authStore = useAuthStore();

// Site form
const siteName = ref('');
const siteCode = ref('');
const siteDesc = ref('');

// Monitoring timers form
const reachInterval = ref(5);
const reachTimeout = ref(2000);
const healthInterval = ref(30);
const healthTimeout = ref(5000);
const cascadeSuppression = ref(true);

const isSavingSite = ref(false);
const isSavingConfig = ref(false);
const siteSuccess = ref<string | null>(null);
const configSuccess = ref<string | null>(null);
const errorMessage = ref<string | null>(null);

onMounted(async () => {
  await configStore.fetchConfig();
  populateForms();
});

watch(() => configStore.site, () => {
  populateForms();
});

function populateForms() {
  if (configStore.site) {
    siteName.value = configStore.site.site_name;
    siteCode.value = configStore.site.site_code;
    siteDesc.value = configStore.site.description || '';
  }
  if (configStore.monitoringConfig) {
    reachInterval.value = configStore.monitoringConfig.reachability_interval_sec;
    reachTimeout.value = configStore.monitoringConfig.reachability_timeout_ms;
    healthInterval.value = configStore.monitoringConfig.health_interval_sec;
    healthTimeout.value = configStore.monitoringConfig.health_timeout_ms;
    cascadeSuppression.value = configStore.monitoringConfig.cascade_suppression_enabled;
  }
}

async function handleSaveSite() {
  errorMessage.value = null;
  siteSuccess.value = null;
  isSavingSite.value = true;
  try {
    await configStore.updateSite({
      site_name: siteName.value.trim(),
      site_code: siteCode.value.trim(),
      description: siteDesc.value.trim() || null
    });
    siteSuccess.value = 'Site enclave metadata updated successfully!';
    setTimeout(() => { siteSuccess.value = null; }, 3000);
  } catch (err: any) {
    errorMessage.value = err.response?.data?.error || err.message || 'Failed to update site';
  } finally {
    isSavingSite.value = false;
  }
}

async function handleSaveConfig() {
  errorMessage.value = null;
  configSuccess.value = null;
  isSavingConfig.value = true;
  try {
    await configStore.updateMonitoringConfig({
      reachability_interval_sec: reachInterval.value,
      reachability_timeout_ms: reachTimeout.value,
      health_interval_sec: healthInterval.value,
      health_timeout_ms: healthTimeout.value,
      cascade_suppression_enabled: cascadeSuppression.value
    });
    configSuccess.value = 'Monitoring engine singleton config updated!';
    setTimeout(() => { configSuccess.value = null; }, 3000);
  } catch (err: any) {
    errorMessage.value = err.response?.data?.error || err.message || 'Failed to update monitoring config';
  } finally {
    isSavingConfig.value = false;
  }
}
</script>

<template>
  <div class="h-full flex flex-col bg-tactical-surface rounded-xl border border-tactical-border overflow-hidden select-none font-mono">
    
    <!-- Header -->
    <div class="p-4 border-b border-tactical-border bg-tactical-surface flex items-center justify-between">
      <div>
        <h2 class="text-sm font-bold uppercase tracking-wider text-slate-100 flex items-center gap-2">
          <Settings class="w-4 h-4 text-tactical-cyan" />
          <span>Campus Enclave & Telemetry Poller Configuration</span>
        </h2>
        <p class="text-[10px] text-slate-500 font-sans">
          Manage physical campus site metadata, polling timers, and topological cascade suppression thresholds.
        </p>
      </div>
    </div>

    <!-- Error Banner -->
    <div v-if="errorMessage" class="m-4 p-3 rounded bg-rose-950/40 border border-rose-500/50 text-rose-300 text-xs flex items-center space-x-2">
      <AlertCircle class="w-4 h-4 flex-shrink-0" />
      <span>{{ errorMessage }}</span>
    </div>

    <!-- Forms Container -->
    <div class="flex-1 overflow-auto p-6 grid grid-cols-1 md:grid-cols-2 gap-6">
      
      <!-- Card 1: Site Enclave Information -->
      <div class="p-5 rounded-lg bg-tactical-panel border border-tactical-border space-y-4">
        <div class="flex items-center justify-between border-b border-tactical-border/60 pb-3">
          <span class="font-bold text-slate-200 text-xs flex items-center gap-2">
            <ShieldCheck class="w-4 h-4 text-tactical-cyan" />
            <span>Campus Site Enclave Identity</span>
          </span>
          <span class="text-[10px] text-slate-500">Site #1</span>
        </div>

        <div v-if="siteSuccess" class="p-2.5 rounded bg-emerald-950/40 border border-emerald-500/50 text-emerald-300 text-xs flex items-center gap-2">
          <CheckCircle2 class="w-4 h-4 flex-shrink-0" />
          <span>{{ siteSuccess }}</span>
        </div>

        <form @submit.prevent="handleSaveSite" class="space-y-4 text-xs font-mono">
          <div>
            <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Site Facility Name *</label>
            <input
              v-model="siteName"
              type="text"
              required
              :disabled="!authStore.isAdmin"
              class="w-full bg-tactical-surface border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan disabled:opacity-50"
            />
          </div>

          <div>
            <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Facility Code *</label>
            <input
              v-model="siteCode"
              type="text"
              required
              :disabled="!authStore.isAdmin"
              class="w-full bg-tactical-surface border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan disabled:opacity-50"
            />
          </div>

          <div>
            <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Operational Scope Description</label>
            <textarea
              v-model="siteDesc"
              rows="3"
              :disabled="!authStore.isAdmin"
              class="w-full bg-tactical-surface border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan disabled:opacity-50"
            ></textarea>
          </div>

          <div v-if="authStore.isAdmin" class="pt-2 flex justify-end">
            <button
              type="submit"
              :disabled="isSavingSite"
              class="flex items-center space-x-1.5 px-4 py-2 rounded-lg bg-tactical-cyan hover:bg-cyan-400 text-slate-950 font-bold transition shadow-[0_0_12px_rgba(0,240,255,0.25)] disabled:opacity-50"
            >
              <Save class="w-4 h-4" />
              <span>{{ isSavingSite ? 'Saving...' : 'Update Site Profile' }}</span>
            </button>
          </div>
        </form>
      </div>

      <!-- Card 2: Monitoring Engine Timers (Singleton) -->
      <div class="p-5 rounded-lg bg-tactical-panel border border-tactical-border space-y-4">
        <div class="flex items-center justify-between border-b border-tactical-border/60 pb-3">
          <span class="font-bold text-slate-200 text-xs flex items-center gap-2">
            <Sliders class="w-4 h-4 text-tactical-amber" />
            <span>High-Performance Telemetry Poller</span>
          </span>
          <span class="text-[10px] text-slate-500">Worker Singleton</span>
        </div>

        <div v-if="configSuccess" class="p-2.5 rounded bg-emerald-950/40 border border-emerald-500/50 text-emerald-300 text-xs flex items-center gap-2">
          <CheckCircle2 class="w-4 h-4 flex-shrink-0" />
          <span>{{ configSuccess }}</span>
        </div>

        <form @submit.prevent="handleSaveConfig" class="space-y-4 text-xs font-mono">
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Ping Interval (Sec)</label>
              <input
                v-model.number="reachInterval"
                type="number"
                min="1"
                max="60"
                required
                :disabled="!authStore.isAdmin"
                class="w-full bg-tactical-surface border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan disabled:opacity-50"
              />
            </div>

            <div>
              <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Ping Timeout (Ms)</label>
              <input
                v-model.number="reachTimeout"
                type="number"
                min="100"
                max="5000"
                required
                :disabled="!authStore.isAdmin"
                class="w-full bg-tactical-surface border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan disabled:opacity-50"
              />
            </div>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Health Interval (Sec)</label>
              <input
                v-model.number="healthInterval"
                type="number"
                min="5"
                max="300"
                required
                :disabled="!authStore.isAdmin"
                class="w-full bg-tactical-surface border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan disabled:opacity-50"
              />
            </div>

            <div>
              <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Health Timeout (Ms)</label>
              <input
                v-model.number="healthTimeout"
                type="number"
                min="500"
                max="10000"
                required
                :disabled="!authStore.isAdmin"
                class="w-full bg-tactical-surface border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan disabled:opacity-50"
              />
            </div>
          </div>

          <!-- Cascade Suppression Toggle -->
          <div class="p-3 rounded bg-tactical-surface border border-tactical-border flex items-start space-x-3">
            <input
              v-model="cascadeSuppression"
              type="checkbox"
              id="cascade-chk"
              :disabled="!authStore.isAdmin"
              class="mt-0.5 rounded bg-tactical-panel border-tactical-border text-tactical-cyan focus:ring-0 disabled:opacity-50"
            />
            <label for="cascade-chk" class="text-xs text-slate-200 select-none">
              <strong class="text-tactical-cyan block">Topological Cascade Outage Suppression</strong>
              <span class="text-[11px] text-slate-400 leading-relaxed block mt-0.5">
                When an upstream switch fails, the poller automatically suppresses synthetic ping sweeps for all child workstations to prevent network flooding and alarm storms.
              </span>
            </label>
          </div>

          <div v-if="authStore.isAdmin" class="pt-2 flex justify-end">
            <button
              type="submit"
              :disabled="isSavingConfig"
              class="flex items-center space-x-1.5 px-4 py-2 rounded-lg bg-tactical-amber hover:bg-amber-400 text-slate-950 font-bold transition shadow-[0_0_12px_rgba(255,183,3,0.25)] disabled:opacity-50"
            >
              <Save class="w-4 h-4" />
              <span>{{ isSavingConfig ? 'Saving...' : 'Update Poller Timers' }}</span>
            </button>
          </div>
        </form>
      </div>

    </div>

  </div>
</template>
