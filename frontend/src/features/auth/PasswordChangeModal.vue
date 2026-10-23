<script setup lang="ts">
import { ref } from 'vue';
import ModalWrapper from '../../components/common/ModalWrapper.vue';
import { apiClient } from '../../api/client';
import { Eye, EyeOff, CheckCircle2 } from 'lucide-vue-next';

defineProps<{
  isOpen: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'success'): void;
}>();

const currentPassword = ref('');
const newPassword = ref('');
const confirmPassword = ref('');

const showCurrent = ref(false);
const showNew = ref(false);
const showConfirm = ref(false);

const isSubmitting = ref(false);
const error = ref<string | null>(null);
const successMessage = ref<string | null>(null);

async function handleChangePassword() {
  error.value = null;
  successMessage.value = null;

  if (!currentPassword.value || !newPassword.value) {
    error.value = 'Current and new passwords are required.';
    return;
  }

  if (newPassword.value !== confirmPassword.value) {
    error.value = 'New password and confirmation do not match.';
    return;
  }

  if (newPassword.value.length < 12) {
    error.value = 'Password must be at least 12 characters long and include uppercase, lowercase, digit, and special character.';
    return;
  }

  isSubmitting.value = true;
  try {
    await apiClient.put('/auth/password', {
      current_password: currentPassword.value,
      new_password: newPassword.value
    });
    successMessage.value = 'Password changed successfully! Active sessions have been invalidated.';
    currentPassword.value = '';
    newPassword.value = '';
    confirmPassword.value = '';
    setTimeout(() => {
      emit('success');
      emit('close');
    }, 1500);
  } catch (err: any) {
    error.value = err.response?.data?.error || err.message || 'Failed to update passphrase.';
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <ModalWrapper
    :is-open="isOpen"
    title="Update Enclave Passphrase"
    subtitle="Argon2id (m=64MB, t=3, p=4) Protected"
    maxWidthClass="max-w-md"
    @close="emit('close')"
  >
    <div v-if="successMessage" class="mb-4 p-3 rounded bg-emerald-950/40 border border-emerald-500/50 text-emerald-300 text-xs font-mono flex items-center space-x-2">
      <CheckCircle2 class="w-4 h-4 flex-shrink-0" />
      <span>{{ successMessage }}</span>
    </div>

    <div v-if="error" class="mb-4 p-3 rounded bg-rose-950/40 border border-rose-500/50 text-rose-300 text-xs font-mono">
      {{ error }}
    </div>

    <form @submit.prevent="handleChangePassword" class="space-y-4 text-xs font-mono">
      <!-- Current Password -->
      <div>
        <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Current Passphrase</label>
        <div class="relative">
          <input
            v-model="currentPassword"
            :type="showCurrent ? 'text' : 'password'"
            required
            class="w-full bg-tactical-panel border border-tactical-border rounded pl-3 pr-9 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          />
          <button
            type="button"
            @click="showCurrent = !showCurrent"
            class="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-tactical-cyan"
          >
            <EyeOff v-if="showCurrent" class="w-4 h-4" />
            <Eye v-else class="w-4 h-4" />
          </button>
        </div>
      </div>

      <!-- New Password -->
      <div>
        <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">New Passphrase (Min 12 Chars)</label>
        <div class="relative">
          <input
            v-model="newPassword"
            :type="showNew ? 'text' : 'password'"
            required
            class="w-full bg-tactical-panel border border-tactical-border rounded pl-3 pr-9 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          />
          <button
            type="button"
            @click="showNew = !showNew"
            class="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-tactical-cyan"
          >
            <EyeOff v-if="showNew" class="w-4 h-4" />
            <Eye v-else class="w-4 h-4" />
          </button>
        </div>
      </div>

      <!-- Confirm Password -->
      <div>
        <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Confirm New Passphrase</label>
        <div class="relative">
          <input
            v-model="confirmPassword"
            :type="showConfirm ? 'text' : 'password'"
            required
            class="w-full bg-tactical-panel border border-tactical-border rounded pl-3 pr-9 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          />
          <button
            type="button"
            @click="showConfirm = !showConfirm"
            class="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-tactical-cyan"
          >
            <EyeOff v-if="showConfirm" class="w-4 h-4" />
            <Eye v-else class="w-4 h-4" />
          </button>
        </div>
      </div>
    </form>

    <template #footer>
      <button
        @click="emit('close')"
        type="button"
        class="px-4 py-2 rounded text-xs font-mono bg-tactical-panel border border-tactical-border text-slate-300 hover:text-white"
      >
        Cancel
      </button>

      <button
        @click="handleChangePassword"
        :disabled="isSubmitting"
        type="button"
        class="px-5 py-2 rounded text-xs font-mono font-bold bg-tactical-cyan text-slate-950 hover:bg-cyan-400 transition shadow-[0_0_12px_rgba(0,240,255,0.3)] disabled:opacity-50"
      >
        {{ isSubmitting ? 'Updating...' : 'Update Passphrase' }}
      </button>
    </template>
  </ModalWrapper>
</template>
