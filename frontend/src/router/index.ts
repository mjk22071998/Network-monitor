import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router';
import TopologyView from '../features/topology/TopologyView.vue';
import DeviceInventoryView from '../features/inventory/DeviceInventoryView.vue';
import GroupHierarchyView from '../features/settings/GroupHierarchyView.vue';
import SubnetDiscoveryView from '../features/discovery/SubnetDiscoveryView.vue';
import BulkIngestView from '../features/inventory/BulkIngestView.vue';
import AuditTrailView from '../features/audit/AuditTrailView.vue';
import UserManagementView from '../features/users/UserManagementView.vue';
import EnclaveSettingsView from '../features/settings/EnclaveSettingsView.vue';
import LoginView from '../features/auth/LoginView.vue';
import { useAuthStore } from '../stores/auth';

const routes: RouteRecordRaw[] = [
  { 
    path: '/', 
    redirect: '/topology' 
  },
  { 
    path: '/topology', 
    name: 'topology', 
    component: TopologyView, 
    meta: { title: 'Topology Canvas', requiresAuth: true } 
  },
  { 
    path: '/inventory', 
    name: 'inventory', 
    component: DeviceInventoryView, 
    meta: { title: 'Device Inventory', requiresAuth: true } 
  },
  { 
    path: '/lab-groups', 
    name: 'lab-groups', 
    component: GroupHierarchyView, 
    meta: { title: 'Computer Lab Groups', requiresAuth: true } 
  },
  { 
    path: '/subnet-sweep', 
    name: 'subnet-sweep', 
    component: SubnetDiscoveryView, 
    meta: { title: 'Subnet Discovery', requiresAuth: true } 
  },
  { 
    path: '/bulk-ingest', 
    name: 'bulk-ingest', 
    component: BulkIngestView, 
    meta: { title: 'Staging & Bulk Ingest', requiresAuth: true } 
  },
  { 
    path: '/audit-trail', 
    name: 'audit-trail', 
    component: AuditTrailView, 
    meta: { title: 'Audit Trail & History', requiresAuth: true } 
  },
  { 
    path: '/users', 
    name: 'users', 
    component: UserManagementView, 
    meta: { title: 'User Management & RBAC', requiresAuth: true } 
  },
  { 
    path: '/settings', 
    name: 'settings', 
    component: EnclaveSettingsView, 
    meta: { title: 'Enclave Settings', requiresAuth: true } 
  },
  { 
    path: '/login', 
    name: 'login', 
    component: LoginView, 
    meta: { title: 'Operator Login' } 
  },
  { 
    path: '/:pathMatch(.*)*', 
    redirect: '/topology' 
  }
];

export const router = createRouter({
  history: createWebHistory(),
  routes
});

// Navigation guard for authenticated operations
router.beforeEach(async (to, _from, next) => {
  const authStore = useAuthStore();
  
  if (!authStore.isInitialized) {
    await authStore.checkAuth();
  }

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    next({ name: 'login', query: { redirect: to.fullPath } });
  } else if (to.name === 'login' && authStore.isAuthenticated) {
    next({ name: 'topology' });
  } else {
    next();
  }
});
