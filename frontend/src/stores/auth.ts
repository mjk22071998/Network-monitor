import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { authApi } from '../api/authApi';
import type { CurrentUser } from '../types/auth';

export const useAuthStore = defineStore('auth', () => {
  const user = ref<CurrentUser | null>(null);
  const token = ref<string | null>(sessionStorage.getItem('observer_session_token'));
  const isLoading = ref<boolean>(false);
  const isInitialized = ref<boolean>(false);
  const error = ref<string | null>(null);

  const isAuthenticated = computed(() => !!user.value);
  const roleName = computed(() => user.value?.role_name ?? 'OPERATOR');
  const username = computed(() => user.value?.username ?? '');

  async function checkAuth(): Promise<boolean> {
    isLoading.value = true;
    error.value = null;
    try {
      const currentUser = await authApi.getMe();
      user.value = currentUser;
      return true;
    } catch {
      user.value = null;
      token.value = null;
      sessionStorage.removeItem('observer_session_token');
      return false;
    } finally {
      isLoading.value = false;
      isInitialized.value = true;
    }
  }

  async function login(usernameInput: string, passwordInput: string): Promise<boolean> {
    isLoading.value = true;
    error.value = null;
    try {
      const res = await authApi.login(usernameInput, passwordInput);
      token.value = res.token;
      sessionStorage.setItem('observer_session_token', res.token);

      // Fetch full identity profile
      const currentUser = await authApi.getMe();
      user.value = currentUser;
      return true;
    } catch (err: any) {
      if (err.response?.data?.error?.message) {
        error.value = err.response.data.error.message;
      } else if (err.response?.status === 401) {
        error.value = 'Invalid operator credentials or account deactivated.';
      } else {
        error.value = 'Unable to connect to enclave security server.';
      }
      return false;
    } finally {
      isLoading.value = false;
    }
  }

  async function logout(): Promise<void> {
    try {
      await authApi.logout();
    } catch {
      // Ignored if network issue or already expired
    } finally {
      user.value = null;
      token.value = null;
      sessionStorage.removeItem('observer_session_token');
    }
  }

  return {
    user,
    token,
    isLoading,
    isInitialized,
    error,
    isAuthenticated,
    roleName,
    username,
    checkAuth,
    login,
    logout
  };
});
