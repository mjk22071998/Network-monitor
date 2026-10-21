<script setup lang="ts">
import { ref } from 'vue';
import { 
  ShieldCheck, 
  Lock, 
  User, 
  Activity, 
  AlertCircle, 
  ArrowRight,
  Eye,
  EyeOff
} from 'lucide-vue-next';
import { useAuthStore } from '../../stores/auth';

const authStore = useAuthStore();
const username = ref<string>('admin');
const password = ref<string>('Admin@NetworkObserver2026!');
const showPassword = ref<boolean>(false);

function togglePasswordVisibility() {
  showPassword.value = !showPassword.value;
}

async function handleLogin() {
  if (!username.value || !password.value) return;
  await authStore.login(username.value, password.value);
}
</script>

<template>
  <div class="fixed inset-0 z-50 bg-tactical-bg bg-radar-dots flex items-center justify-center p-4 select-none">
    <div class="absolute inset-0 bg-tactical-grid opacity-75 pointer-events-none"></div>

    <!-- Centered Tactical Auth Card -->
    <div class="w-full max-w-md bg-tactical-surface/95 border-2 border-tactical-border hover:border-tactical-cyan/40 rounded-2xl p-8 shadow-2xl relative z-10 backdrop-blur-xl transition duration-300">
      
      <!-- Brand & Enclave Status Header -->
      <div class="text-center space-y-3 mb-6">
        <div class="w-14 h-14 mx-auto rounded-xl bg-tactical-panel border border-tactical-cyan/50 flex items-center justify-center text-tactical-cyan shadow-[0_0_20px_rgba(0,240,255,0.25)]">
          <Activity class="w-7 h-7 animate-pulse text-tactical-cyan" />
        </div>
        
        <div>
          <h1 class="font-mono text-xl font-bold tracking-wider text-slate-100 flex items-center justify-center gap-2">
            NETWORK OBSERVER
            <span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-tactical-panel border border-tactical-border text-tactical-cyan">
              v2.2
            </span>
          </h1>
          <p class="text-[11px] font-mono text-slate-400 mt-0.5">INDUSTRIAL LAN TELEMETRY & TOPOLOGY PLATFORM</p>
        </div>

        <!-- Air-Gap Status Banner -->
        <div class="inline-flex items-center space-x-2 px-3 py-1 rounded-full bg-tactical-panel border border-tactical-online/30 text-[10px] font-mono text-tactical-online">
          <span class="w-2 h-2 rounded-full bg-tactical-online animate-ping"></span>
          <span class="font-bold tracking-wide">ENCLAVE: PRIMARY SITE | 100% AIR-GAP SECURE</span>
        </div>
      </div>

      <!-- Error Envelope Alert -->
      <div
        v-if="authStore.error"
        class="mb-4 p-3 rounded-lg bg-tactical-alert/10 border border-tactical-alert/40 text-tactical-alert text-xs font-mono flex items-start space-x-2"
      >
        <AlertCircle class="w-4 h-4 flex-shrink-0 mt-0.5" />
        <div class="flex-1 text-[11px] leading-relaxed">{{ authStore.error }}</div>
      </div>

      <!-- Credentials Form -->
      <form @submit.prevent="handleLogin" class="space-y-4 text-xs font-mono">
        <div>
          <label class="block text-slate-400 mb-1.5 uppercase tracking-wider text-[10px] font-bold flex items-center gap-1.5">
            <User class="w-3.5 h-3.5 text-tactical-cyan" />
            <span>Operator Identity</span>
          </label>
          <input
            v-model="username"
            type="text"
            required
            autocomplete="username"
            placeholder="e.g. admin or operator"
            class="w-full bg-tactical-panel border border-tactical-border rounded-lg px-3.5 py-2.5 text-slate-200 focus:outline-none focus:border-tactical-cyan focus:ring-1 focus:ring-tactical-cyan font-mono transition"
          />
        </div>

        <div>
          <label class="block text-slate-400 mb-1.5 uppercase tracking-wider text-[10px] font-bold flex items-center justify-between">
            <span class="flex items-center gap-1.5">
              <Lock class="w-3.5 h-3.5 text-tactical-cyan" />
              <span>Enclave Passphrase</span>
            </span>
            <span class="text-[9px] text-slate-500 font-normal">Argon2id Protected</span>
          </label>
          <div class="relative">
            <input
              v-model="password"
              :type="showPassword ? 'text' : 'password'"
              required
              autocomplete="current-password"
              placeholder="••••••••••••"
              class="w-full bg-tactical-panel border border-tactical-border rounded-lg pl-3.5 pr-10 py-2.5 text-slate-200 focus:outline-none focus:border-tactical-cyan focus:ring-1 focus:ring-tactical-cyan font-mono transition"
            />
            <button
              type="button"
              @click="togglePasswordVisibility"
              class="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-tactical-cyan p-1 rounded focus:outline-none transition"
              :title="showPassword ? 'Hide passphrase' : 'Show passphrase'"
            >
              <EyeOff v-if="showPassword" class="w-4 h-4" />
              <Eye v-else class="w-4 h-4" />
            </button>
          </div>
        </div>

        <!-- Target Enclave Badge -->
        <div class="p-2.5 rounded bg-tactical-panel border border-tactical-border text-[11px] text-slate-400 flex items-center justify-between">
          <span class="text-[10px] uppercase font-bold text-slate-500">Target Enclave:</span>
          <span class="text-tactical-cyan font-mono font-semibold flex items-center gap-1">
            <ShieldCheck class="w-3.5 h-3.5 text-tactical-cyan" />
            Site 01 (SCADA / Primary)
          </span>
        </div>

        <!-- Submit Button -->
        <button
          type="submit"
          :disabled="authStore.isLoading"
          class="w-full py-3 rounded-lg bg-gradient-to-r from-cyan-600 via-blue-600 to-indigo-600 hover:from-cyan-500 hover:to-blue-500 disabled:opacity-50 text-white font-bold text-xs tracking-wider uppercase transition shadow-lg shadow-cyan-600/30 flex items-center justify-center space-x-2"
        >
          <span v-if="authStore.isLoading" class="flex items-center gap-2">
            <Activity class="w-4 h-4 animate-spin text-white" />
            <span>Verifying Argon2id Hash...</span>
          </span>
          <span v-else class="flex items-center gap-2">
            <span>Authenticate to Enclave</span>
            <ArrowRight class="w-4 h-4" />
          </span>
        </button>
      </form>

      <!-- Footer Policy Note -->
      <div class="mt-6 pt-4 border-t border-tactical-border text-center text-[10px] font-mono text-slate-500 space-y-1">
        <p>Restricted Mission-Critical SCADA Enclave</p>
        <p class="text-[9px] text-slate-600">Zero Public Self-Registration | RFC 9106 Argon2id Cryptography</p>
      </div>

    </div>
  </div>
</template>
