<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useAuthStore } from '../../stores/auth';
import { usersApi } from '../../api/usersApi';
import type { UserListItem, CreateUserPayload, UpdateUserPayload } from '../../types/user';
import { 
  Users, 
  UserPlus, 
  Trash2, 
  Edit3, 
  CheckCircle2, 
  AlertCircle, 
  RefreshCw,
  X
} from 'lucide-vue-next';

const authStore = useAuthStore();

const users = ref<UserListItem[]>([]);
const isLoading = ref<boolean>(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);

// Modal state
const isCreateModalOpen = ref(false);
const isEditModalOpen = ref(false);
const isDeleteModalOpen = ref(false);

const userToEdit = ref<UserListItem | null>(null);
const userToDelete = ref<UserListItem | null>(null);

// Create form
const newUsername = ref('');
const newPassword = ref('');
const newRoleId = ref<number>(2); // Default: Network Engineer
const newSiteId = ref<number>(1);
const isSubmittingCreate = ref(false);
const createError = ref<string | null>(null);

// Edit form
const editRoleId = ref<number>(2);
const editIsActive = ref<boolean>(true);
const editResetPassword = ref<string>('');
const isSubmittingEdit = ref(false);
const editError = ref<string | null>(null);

// Delete state
const isSubmittingDelete = ref(false);
const deleteError = ref<string | null>(null);

// Roles reference
const ROLES = [
  { id: 1, name: 'Network Admin', desc: 'Full administrative access and policy governance' },
  { id: 2, name: 'Network Engineer', desc: 'Topology configuration, subnet sweep, and telemetry inspection' },
  { id: 3, name: 'IT Support', desc: 'Read-only telemetry monitoring and diagnostic queries' }
];

function getRoleBadgeClass(roleName: string) {
  const r = (roleName || '').toLowerCase();
  if (r.includes('admin')) {
    return 'bg-amber-950/60 border-amber-500/40 text-amber-300';
  } else if (r.includes('engineer')) {
    return 'bg-cyan-950/60 border-cyan-500/40 text-cyan-300';
  } else {
    return 'bg-blue-950/60 border-blue-500/40 text-blue-300';
  }
}

function getRoleIdFromRoleName(roleName: string): number {
  const r = (roleName || '').toLowerCase();
  if (r.includes('admin')) return 1;
  if (r.includes('engineer')) return 2;
  return 3;
}

// Stats
const totalUsers = computed(() => users.value.length);
const adminCount = computed(() => users.value.filter(u => (u.role || '').toLowerCase().includes('admin')).length);
const engineerCount = computed(() => users.value.filter(u => (u.role || '').toLowerCase().includes('engineer')).length);
const supportCount = computed(() => users.value.filter(u => (u.role || '').toLowerCase().includes('support')).length);

async function fetchUsers() {
  isLoading.value = true;
  errorMessage.value = null;
  try {
    users.value = await usersApi.getUsers();
  } catch (err: any) {
    errorMessage.value = err.response?.data?.error || err.message || 'Failed to load operator accounts';
  } finally {
    isLoading.value = false;
  }
}

onMounted(() => {
  fetchUsers();
});

// Create User
function openCreateModal() {
  newUsername.value = '';
  newPassword.value = '';
  newRoleId.value = 2;
  newSiteId.value = 1;
  createError.value = null;
  isCreateModalOpen.value = true;
}

async function handleCreateUser() {
  if (!newUsername.value.trim() || !newPassword.value) {
    createError.value = 'Username and passphrase are required.';
    return;
  }
  isSubmittingCreate.value = true;
  createError.value = null;
  try {
    const payload: CreateUserPayload = {
      username: newUsername.value.trim(),
      password: newPassword.value,
      role_id: newRoleId.value,
      site_id: newSiteId.value
    };
    await usersApi.createUser(payload);
    isCreateModalOpen.value = false;
    successMessage.value = `Operator account '${newUsername.value}' provisioned successfully!`;
    setTimeout(() => { successMessage.value = null; }, 3500);
    await fetchUsers();
  } catch (err: any) {
    createError.value = err.response?.data?.error || err.message || 'Failed to provision user';
  } finally {
    isSubmittingCreate.value = false;
  }
}

// Edit User
function openEditModal(user: UserListItem) {
  userToEdit.value = user;
  editRoleId.value = getRoleIdFromRoleName(user.role);
  editIsActive.value = user.is_active;
  editResetPassword.value = '';
  editError.value = null;
  isEditModalOpen.value = true;
}

async function handleUpdateUser() {
  if (!userToEdit.value) return;
  isSubmittingEdit.value = true;
  editError.value = null;
  try {
    const payload: UpdateUserPayload = {
      role_id: editRoleId.value,
      is_active: editIsActive.value,
      reset_password: editResetPassword.value.trim() ? editResetPassword.value.trim() : undefined
    };
    await usersApi.updateUser(userToEdit.value.user_id, payload);
    isEditModalOpen.value = false;
    successMessage.value = `Account '${userToEdit.value.username}' updated successfully!`;
    setTimeout(() => { successMessage.value = null; }, 3500);
    await fetchUsers();
  } catch (err: any) {
    editError.value = err.response?.data?.error || err.message || 'Failed to update account';
  } finally {
    isSubmittingEdit.value = false;
  }
}

// Delete / Deactivate User
function openDeleteModal(user: UserListItem) {
  userToDelete.value = user;
  deleteError.value = null;
  isDeleteModalOpen.value = true;
}

async function handleDeleteUser() {
  if (!userToDelete.value) return;
  isSubmittingDelete.value = true;
  deleteError.value = null;
  try {
    await usersApi.deleteUser(userToDelete.value.user_id);
    isDeleteModalOpen.value = false;
    successMessage.value = `Account '${userToDelete.value.username}' removed/deactivated.`;
    setTimeout(() => { successMessage.value = null; }, 3500);
    await fetchUsers();
  } catch (err: any) {
    deleteError.value = err.response?.data?.error || err.message || 'Failed to delete account';
  } finally {
    isSubmittingDelete.value = false;
  }
}
</script>

<template>
  <div class="space-y-6 max-w-7xl mx-auto w-full">
    
    <!-- Header Banner -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-zinc-800 pb-5">
      <div>
        <div class="flex items-center gap-2.5">
          <div class="w-8 h-8 rounded-lg bg-purple-500/10 border border-purple-500/30 flex items-center justify-center text-purple-400">
            <Users class="w-4 h-4" />
          </div>
          <h1 class="text-lg font-semibold text-zinc-100 tracking-tight">Operator Account Governance</h1>
        </div>
        <p class="text-xs text-zinc-400 mt-1">
          Manage authorized campus network engineers, operators, and role-based access controls.
        </p>
      </div>

      <div class="flex items-center gap-2">
        <button
          @click="fetchUsers"
          :disabled="isLoading"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-zinc-800 border border-zinc-700 text-zinc-200 hover:text-zinc-100 hover:bg-zinc-700 text-xs font-medium transition disabled:opacity-50"
          title="Refresh Operator List"
        >
          <RefreshCw class="w-3.5 h-3.5" :class="{ 'animate-spin': isLoading }" />
          <span>Refresh</span>
        </button>

        <button
          @click="openCreateModal"
          class="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-md bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-medium shadow-sm transition"
        >
          <UserPlus class="w-3.5 h-3.5" />
          <span>Provision Operator</span>
        </button>
      </div>
    </div>

    <!-- Alert Banners -->
    <div v-if="successMessage" class="p-3 rounded-lg bg-emerald-950/60 border border-emerald-500/50 flex items-center gap-2 text-xs text-emerald-300">
      <CheckCircle2 class="w-4 h-4 text-emerald-400 flex-shrink-0" />
      <span>{{ successMessage }}</span>
    </div>

    <div v-if="errorMessage" class="p-3 rounded-lg bg-rose-950/60 border border-rose-500/50 flex items-center gap-2 text-xs text-rose-300">
      <AlertCircle class="w-4 h-4 text-rose-400 flex-shrink-0" />
      <span>{{ errorMessage }}</span>
    </div>

    <!-- Metrics Strip (Minimal, shadcn style) -->
    <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
      <div class="p-3 rounded-lg bg-zinc-900 border border-zinc-800 shadow-sm">
        <div class="text-[11px] text-zinc-400 font-medium">Total Operators</div>
        <div class="text-lg font-bold text-zinc-100 mt-1 font-mono">{{ totalUsers }}</div>
      </div>
      <div class="p-3 rounded-lg bg-zinc-900 border border-zinc-800 shadow-sm">
        <div class="text-[11px] text-amber-400 font-medium">Network Admins</div>
        <div class="text-lg font-bold text-amber-300 mt-1 font-mono">{{ adminCount }}</div>
      </div>
      <div class="p-3 rounded-lg bg-zinc-900 border border-zinc-800 shadow-sm">
        <div class="text-[11px] text-cyan-400 font-medium">Network Engineers</div>
        <div class="text-lg font-bold text-cyan-300 mt-1 font-mono">{{ engineerCount }}</div>
      </div>
      <div class="p-3 rounded-lg bg-zinc-900 border border-zinc-800 shadow-sm">
        <div class="text-[11px] text-blue-400 font-medium">IT Support</div>
        <div class="text-lg font-bold text-blue-300 mt-1 font-mono">{{ supportCount }}</div>
      </div>
    </div>

    <!-- Operators Table Card -->
    <div class="rounded-xl border border-zinc-800 bg-zinc-900/90 shadow-sm overflow-hidden">
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse text-xs">
          <thead>
            <tr class="border-b border-zinc-800 bg-zinc-950/60 text-zinc-400 font-mono text-[11px]">
              <th class="py-3 px-4 font-medium">OPERATOR USERNAME</th>
              <th class="py-3 px-4 font-medium">ROLE</th>
              <th class="py-3 px-4 font-medium">ENCLAVE SITE</th>
              <th class="py-3 px-4 font-medium">STATUS</th>
              <th class="py-3 px-4 font-medium">REGISTERED AT</th>
              <th class="py-3 px-4 font-medium text-right">ACTIONS</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-zinc-800">
            <tr 
              v-for="user in users" 
              :key="user.user_id"
              class="hover:bg-zinc-800/40 transition-colors"
            >
              <!-- Username -->
              <td class="py-3 px-4">
                <div class="flex items-center gap-2.5">
                  <div class="w-7 h-7 rounded-md bg-zinc-800 border border-zinc-700 flex items-center justify-center font-bold text-xs text-zinc-200">
                    {{ (user.username || 'U')[0].toUpperCase() }}
                  </div>
                  <div>
                    <span class="font-medium text-zinc-100">{{ user.username }}</span>
                    <span 
                      v-if="user.username === authStore.username" 
                      class="ml-2 px-1.5 py-0.2 rounded text-[9px] bg-zinc-800 text-zinc-400 border border-zinc-700 font-mono"
                    >
                      YOU
                    </span>
                  </div>
                </div>
              </td>

              <!-- Role -->
              <td class="py-3 px-4">
                <span 
                  class="px-2 py-0.5 rounded text-[10px] font-semibold border font-mono"
                  :class="getRoleBadgeClass(user.role)"
                >
                  {{ user.role }}
                </span>
              </td>

              <!-- Site -->
              <td class="py-3 px-4 text-zinc-300">
                {{ user.site_name || 'Primary Enclave Site' }}
              </td>

              <!-- Active Status -->
              <td class="py-3 px-4">
                <span 
                  v-if="user.is_active" 
                  class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-medium bg-emerald-950/60 text-emerald-300 border border-emerald-500/40"
                >
                  <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span> ACTIVE
                </span>
                <span 
                  v-else 
                  class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-medium bg-zinc-800 text-zinc-400 border border-zinc-700"
                >
                  <span class="w-1.5 h-1.5 rounded-full bg-zinc-500"></span> INACTIVE
                </span>
              </td>

              <!-- Created At -->
              <td class="py-3 px-4 font-mono text-zinc-400 text-[11px]">
                {{ new Date(user.created_at).toLocaleString() }}
              </td>

              <!-- Actions -->
              <td class="py-3 px-4 text-right">
                <div class="flex items-center justify-end gap-1.5">
                  <button
                    @click="openEditModal(user)"
                    class="p-1.5 rounded hover:bg-zinc-800 text-zinc-300 hover:text-zinc-100 transition"
                    title="Edit Role / Status / Passphrase"
                  >
                    <Edit3 class="w-3.5 h-3.5" />
                  </button>

                  <button
                    v-if="user.username !== authStore.username"
                    @click="openDeleteModal(user)"
                    class="p-1.5 rounded hover:bg-rose-950/60 text-zinc-400 hover:text-rose-400 transition"
                    title="Delete or Deactivate Operator"
                  >
                    <Trash2 class="w-3.5 h-3.5" />
                  </button>
                  <span v-else class="text-[10px] text-zinc-600 px-1.5">Locked</span>
                </div>
              </td>
            </tr>

            <!-- Empty State -->
            <tr v-if="users.length === 0 && !isLoading">
              <td colspan="6" class="py-12 text-center text-zinc-400">
                <Users class="w-8 h-8 mx-auto mb-2 text-zinc-400" />
                <p class="text-xs font-medium text-zinc-300">No Operator Accounts Found</p>
                <p class="text-[11px] text-zinc-400 mt-1">Click "Provision Operator" above to create an account.</p>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- ================================================================= -->
    <!-- CREATE OPERATOR MODAL                                             -->
    <!-- ================================================================= -->
    <div 
      v-if="isCreateModalOpen" 
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
    >
      <div class="w-full max-w-md rounded-xl bg-zinc-900 border border-zinc-700 shadow-2xl p-6 text-zinc-100 space-y-4">
        <div class="flex items-center justify-between pb-3 border-b border-zinc-800">
          <div class="flex items-center gap-2">
            <UserPlus class="w-4 h-4 text-emerald-400" />
            <h2 class="text-sm font-semibold">Provision New Operator</h2>
          </div>
          <button @click="isCreateModalOpen = false" class="text-zinc-400 hover:text-zinc-200">
            <X class="w-4 h-4" />
          </button>
        </div>

        <div v-if="createError" class="p-2.5 rounded bg-rose-950/60 border border-rose-500/40 text-xs text-rose-300">
          {{ createError }}
        </div>

        <form @submit.prevent="handleCreateUser" class="space-y-3.5 text-xs">
          <div>
            <label class="block text-zinc-300 mb-1 font-medium">Username</label>
            <input 
              v-model="newUsername" 
              type="text" 
              placeholder="e.g. jdoe_engineer"
              class="w-full px-3 py-2 rounded bg-zinc-950 border border-zinc-700 focus:border-emerald-500 focus:outline-none text-zinc-100 font-mono"
              required 
            />
          </div>

          <div>
            <label class="block text-zinc-300 mb-1 font-medium">Initial Passphrase</label>
            <input 
              v-model="newPassword" 
              type="password" 
              placeholder="Strong temporary passphrase"
              class="w-full px-3 py-2 rounded bg-zinc-950 border border-zinc-700 focus:border-emerald-500 focus:outline-none text-zinc-100 font-mono"
              required 
            />
          </div>

          <div>
            <label class="block text-zinc-300 mb-1 font-medium">Role Assignment</label>
            <select 
              v-model="newRoleId"
              class="w-full px-3 py-2 rounded bg-zinc-950 border border-zinc-700 focus:border-emerald-500 focus:outline-none text-zinc-100"
            >
              <option v-for="r in ROLES" :key="r.id" :value="r.id">
                {{ r.name }} ({{ r.desc }})
              </option>
            </select>
          </div>

          <div class="flex items-center justify-end gap-2 pt-3 border-t border-zinc-800">
            <button 
              type="button" 
              @click="isCreateModalOpen = false"
              class="px-3.5 py-1.5 rounded bg-zinc-800 hover:bg-zinc-700 text-zinc-300"
            >
              Cancel
            </button>
            <button 
              type="submit" 
              :disabled="isSubmittingCreate"
              class="px-4 py-1.5 rounded bg-emerald-600 hover:bg-emerald-500 text-white font-medium disabled:opacity-50"
            >
              {{ isSubmittingCreate ? 'Provisioning...' : 'Create Account' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- ================================================================= -->
    <!-- EDIT OPERATOR MODAL                                               -->
    <!-- ================================================================= -->
    <div 
      v-if="isEditModalOpen" 
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
    >
      <div class="w-full max-w-md rounded-xl bg-zinc-900 border border-zinc-700 shadow-2xl p-6 text-zinc-100 space-y-4">
        <div class="flex items-center justify-between pb-3 border-b border-zinc-800">
          <div class="flex items-center gap-2">
            <Edit3 class="w-4 h-4 text-cyan-400" />
            <h2 class="text-sm font-semibold">Edit Operator: {{ userToEdit?.username }}</h2>
          </div>
          <button @click="isEditModalOpen = false" class="text-zinc-400 hover:text-zinc-200">
            <X class="w-4 h-4" />
          </button>
        </div>

        <div v-if="editError" class="p-2.5 rounded bg-rose-950/60 border border-rose-500/40 text-xs text-rose-300">
          {{ editError }}
        </div>

        <form @submit.prevent="handleUpdateUser" class="space-y-3.5 text-xs">
          <div>
            <label class="block text-zinc-300 mb-1 font-medium">Assigned Role</label>
            <select 
              v-model="editRoleId"
              class="w-full px-3 py-2 rounded bg-zinc-950 border border-zinc-700 focus:border-cyan-500 focus:outline-none text-zinc-100"
            >
              <option v-for="r in ROLES" :key="r.id" :value="r.id">
                {{ r.name }} ({{ r.desc }})
              </option>
            </select>
          </div>

          <div class="flex items-center justify-between p-2.5 rounded bg-zinc-950 border border-zinc-800">
            <div>
              <div class="font-medium text-zinc-200">Account Active State</div>
              <div class="text-[11px] text-zinc-400">Disabled accounts cannot authenticate or access APIs.</div>
            </div>
            <input 
              v-model="editIsActive" 
              type="checkbox" 
              class="w-4 h-4 rounded text-emerald-500 bg-zinc-900 border-zinc-700 focus:ring-0" 
            />
          </div>

          <div>
            <label class="block text-zinc-300 mb-1 font-medium">
              Administrative Passphrase Reset <span class="text-zinc-500 font-normal">(Leave blank to keep current)</span>
            </label>
            <input 
              v-model="editResetPassword" 
              type="password" 
              placeholder="Enter new passphrase to reset"
              class="w-full px-3 py-2 rounded bg-zinc-950 border border-zinc-700 focus:border-cyan-500 focus:outline-none text-zinc-100 font-mono"
            />
          </div>

          <div class="flex items-center justify-end gap-2 pt-3 border-t border-zinc-800">
            <button 
              type="button" 
              @click="isEditModalOpen = false"
              class="px-3.5 py-1.5 rounded bg-zinc-800 hover:bg-zinc-700 text-zinc-300"
            >
              Cancel
            </button>
            <button 
              type="submit" 
              :disabled="isSubmittingEdit"
              class="px-4 py-1.5 rounded bg-cyan-600 hover:bg-cyan-500 text-white font-medium disabled:opacity-50"
            >
              {{ isSubmittingEdit ? 'Saving...' : 'Save Changes' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- ================================================================= -->
    <!-- DELETE / DEACTIVATE MODAL                                         -->
    <!-- ================================================================= -->
    <div 
      v-if="isDeleteModalOpen" 
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
    >
      <div class="w-full max-w-sm rounded-xl bg-zinc-900 border border-zinc-700 shadow-2xl p-6 text-zinc-100 space-y-4">
        <div class="flex items-center gap-2 text-rose-400 pb-2 border-b border-zinc-800">
          <Trash2 class="w-4 h-4" />
          <h2 class="text-sm font-semibold">Delete Operator Account</h2>
        </div>

        <p class="text-xs text-zinc-300">
          Are you sure you want to delete or deactivate operator <span class="font-mono text-zinc-100 font-semibold">{{ userToDelete?.username }}</span>?
        </p>

        <div v-if="deleteError" class="p-2.5 rounded bg-rose-950/60 border border-rose-500/40 text-xs text-rose-300">
          {{ deleteError }}
        </div>

        <div class="flex items-center justify-end gap-2 pt-3 border-t border-zinc-800 text-xs">
          <button 
            type="button" 
            @click="isDeleteModalOpen = false"
            class="px-3.5 py-1.5 rounded bg-zinc-800 hover:bg-zinc-700 text-zinc-300"
          >
            Cancel
          </button>
          <button 
            type="button" 
            @click="handleDeleteUser"
            :disabled="isSubmittingDelete"
            class="px-4 py-1.5 rounded bg-rose-600 hover:bg-rose-500 text-white font-medium disabled:opacity-50"
          >
            {{ isSubmittingDelete ? 'Deleting...' : 'Confirm Delete' }}
          </button>
        </div>
      </div>
    </div>

  </div>
</template>
