<script setup lang="ts">
import { ref } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { 
  Activity, 
  User, 
  Lock, 
  Eye, 
  EyeOff, 
  ArrowRight, 
  AlertCircle,
  Loader2
} from 'lucide-vue-next';
import { useAuthStore } from '../../stores/auth';

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();

const username = ref<string>('admin');
const password = ref<string>('Admin@NetworkObserver2026!');
const showPassword = ref<boolean>(false);

function togglePasswordVisibility() {
  showPassword.value = !showPassword.value;
}

async function handleLogin() {
  if (!username.value || !password.value || authStore.isLoading) return;
  const success = await authStore.login(username.value, password.value);
  if (success) {
    const redirectPath = (route.query.redirect as string) || '/topology';
    router.push(redirectPath);
  }
}
</script>

<template>
  <div class="fixed inset-0 z-50 bg-zinc-950 flex items-center justify-center p-4 select-none overflow-hidden font-sans">
    
    <!-- Animated Ambient Glowing Orbs -->
    <div class="absolute -top-32 -left-32 w-96 h-96 rounded-full bg-emerald-500/15 blur-[100px] pointer-events-none animate-float-1"></div>
    <div class="absolute -bottom-32 -right-32 w-96 h-96 rounded-full bg-cyan-500/15 blur-[100px] pointer-events-none animate-float-2"></div>
    <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[480px] h-[480px] rounded-full bg-emerald-500/5 blur-[120px] pointer-events-none animate-pulse-glow"></div>

    <!-- Animated Subtle Background Cyber Dot Grid -->
    <div class="absolute inset-0 bg-cyber-dots opacity-40 pointer-events-none animate-grid-drift"></div>
    
    <!-- Vignette / Radial Mask to focus on the center card -->
    <div class="absolute inset-0 bg-radial-vignette pointer-events-none"></div>

    <!-- Sleek Minimalist Glassmorphic Card -->
    <div class="w-full max-w-[390px] relative z-10">
      
      <!-- Ambient Backlight Ring -->
      <div class="absolute -inset-0.5 rounded-2xl bg-gradient-to-b from-emerald-500/20 via-zinc-800/40 to-transparent blur-sm pointer-events-none"></div>

      <div class="relative bg-zinc-900/75 border border-zinc-800/90 rounded-2xl p-7 shadow-2xl backdrop-blur-2xl transition duration-300 hover:border-zinc-700/80">
        
        <!-- Header -->
        <div class="text-center space-y-2 mb-6">
          <div class="inline-flex items-center justify-center w-11 h-11 rounded-xl bg-emerald-500/10 border border-emerald-500/25 text-emerald-400 shadow-[0_0_20px_rgba(16,185,129,0.2)]">
            <Activity class="w-5 h-5 animate-pulse" />
          </div>
          <div>
            <h1 class="text-lg font-semibold tracking-tight text-zinc-100">
              Network Observer
            </h1>
            <p class="text-xs text-zinc-400 mt-0.5">
              Sign in to your telemetry console
            </p>
          </div>
        </div>

        <!-- Error Notification -->
        <transition
          enter-active-class="transition duration-150 ease-out"
          enter-from-class="transform opacity-0 -translate-y-1"
          enter-to-class="transform opacity-100 translate-y-0"
          leave-active-class="transition duration-100 ease-in"
          leave-from-class="transform opacity-100 translate-y-0"
          leave-to-class="transform opacity-0 -translate-y-1"
        >
          <div
            v-if="authStore.error"
            class="mb-4 p-2.5 rounded-lg bg-rose-500/10 border border-rose-500/20 text-rose-400 text-xs flex items-center gap-2"
          >
            <AlertCircle class="w-4 h-4 flex-shrink-0" />
            <span class="truncate">{{ authStore.error }}</span>
          </div>
        </transition>

        <!-- Credentials Form -->
        <form @submit.prevent="handleLogin" class="space-y-4">
          <!-- Username Input -->
          <div class="space-y-1.5">
            <label class="block text-xs font-medium text-zinc-300">
              Username
            </label>
            <div class="relative flex items-center">
              <span class="absolute left-3 text-zinc-500 pointer-events-none">
                <User class="w-4 h-4" />
              </span>
              <input
                v-model="username"
                type="text"
                required
                autocomplete="username"
                placeholder="admin"
                class="w-full bg-zinc-950/80 border border-zinc-800 rounded-lg pl-9 pr-3.5 py-2 text-xs text-zinc-200 placeholder-zinc-600 focus:outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 transition font-sans"
              />
            </div>
          </div>

          <!-- Password Input -->
          <div class="space-y-1.5">
            <label class="block text-xs font-medium text-zinc-300">
              Password
            </label>
            <div class="relative flex items-center">
              <span class="absolute left-3 text-zinc-500 pointer-events-none">
                <Lock class="w-4 h-4" />
              </span>
              <input
                v-model="password"
                :type="showPassword ? 'text' : 'password'"
                required
                autocomplete="current-password"
                placeholder="••••••••••••"
                class="w-full bg-zinc-950/80 border border-zinc-800 rounded-lg pl-9 pr-9 py-2 text-xs text-zinc-200 placeholder-zinc-600 focus:outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 transition font-sans"
              />
              <button
                type="button"
                @click="togglePasswordVisibility"
                class="absolute right-2.5 text-zinc-500 hover:text-zinc-300 p-1 rounded focus:outline-none transition"
                :title="showPassword ? 'Hide password' : 'Show password'"
              >
                <EyeOff v-if="showPassword" class="w-3.5 h-3.5" />
                <Eye v-else class="w-3.5 h-3.5" />
              </button>
            </div>
          </div>

          <!-- Action Button -->
          <button
            type="submit"
            :disabled="authStore.isLoading"
            class="w-full mt-2 py-2.5 px-4 rounded-lg bg-emerald-600 hover:bg-emerald-500 active:bg-emerald-700 disabled:opacity-50 text-white font-medium text-xs tracking-wide transition duration-150 shadow-sm shadow-emerald-950 flex items-center justify-center gap-2 group cursor-pointer"
          >
            <Loader2 v-if="authStore.isLoading" class="w-4 h-4 animate-spin text-white" />
            <template v-else>
              <span>Sign In</span>
              <ArrowRight class="w-3.5 h-3.5 group-hover:translate-x-0.5 transition-transform" />
            </template>
          </button>
        </form>

        <!-- Subtle Footer -->
        <div class="mt-6 pt-4 border-t border-zinc-800/80 text-center">
          <p class="text-[11px] text-zinc-400">
            Air-Gapped Telemetry &bull; Zero Cloud Egress
          </p>
        </div>

      </div>
    </div>

  </div>
</template>

<style scoped>
.bg-cyber-dots {
  background-image: radial-gradient(rgba(255, 255, 255, 0.08) 1px, transparent 1px);
  background-size: 24px 24px;
}

.bg-radial-vignette {
  background: radial-gradient(circle at center, transparent 30%, rgba(9, 9, 11, 0.8) 100%);
}

@keyframes float-1 {
  0%, 100% {
    transform: translate(0px, 0px) scale(1);
  }
  50% {
    transform: translate(25px, -20px) scale(1.06);
  }
}

@keyframes float-2 {
  0%, 100% {
    transform: translate(0px, 0px) scale(1);
  }
  50% {
    transform: translate(-20px, 25px) scale(0.96);
  }
}

@keyframes pulse-glow {
  0%, 100% {
    opacity: 0.15;
    transform: translate(-50%, -50%) scale(1);
  }
  50% {
    opacity: 0.3;
    transform: translate(-50%, -50%) scale(1.1);
  }
}

@keyframes grid-drift {
  0% {
    background-position: 0 0;
  }
  100% {
    background-position: 24px 24px;
  }
}

.animate-float-1 {
  animation: float-1 12s ease-in-out infinite;
}

.animate-float-2 {
  animation: float-2 14s ease-in-out infinite;
}

.animate-pulse-glow {
  animation: pulse-glow 8s ease-in-out infinite;
}

.animate-grid-drift {
  animation: grid-drift 20s linear infinite;
}
</style>
