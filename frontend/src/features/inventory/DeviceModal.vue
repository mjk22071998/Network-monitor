<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import ModalWrapper from '../../components/common/ModalWrapper.vue';
import { useGroupsStore } from '../../stores/groups';
import { useDevicesStore } from '../../stores/devices';
import type { Device, CreateDeviceRequest, UpdateDeviceRequest } from '../../types/device';

const props = defineProps<{
  isOpen: boolean;
  deviceToEdit?: Device | null;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'saved'): void;
}>();

const groupsStore = useGroupsStore();
const devicesStore = useDevicesStore();

const isSubmitting = ref(false);
const errorMessage = ref<string | null>(null);

// Form fields
const displayName = ref('');
const primaryIp = ref('');
const macAddress = ref('');
const typeId = ref<number>(5); // default PC
const groupId = ref<number>(1); // default Unassigned
const parentDeviceId = ref<number | null>(null);
const zone = ref('');
const vendor = ref('');
const model = ref('');
const isMonitored = ref(true);

const isEditMode = computed(() => !!props.deviceToEdit && Number(props.deviceToEdit.device_id) > 0);

const flatGroups = computed(() => groupsStore.flattenGroups(groupsStore.groupsTree));

// Potential parents (Core Routers and Switches)
const candidateParents = computed(() => {
  const all = Array.isArray(devicesStore.devices) ? devicesStore.devices : [];
  const currentId = props.deviceToEdit ? Number(props.deviceToEdit.device_id) : 0;
  return all.filter(d => 
    (d.type_id === 1 || d.type_id === 2 || d.type_id === 3) &&
    (currentId <= 0 || d.device_id !== currentId)
  );
});

// MAC Regex: ^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$
const macRegex = /^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$/;

watch(
  () => [props.isOpen, props.deviceToEdit] as const,
  ([newOpen, newDevice]) => {
    if (newOpen) {
      errorMessage.value = null;
      if (newDevice) {
        displayName.value = newDevice.display_name || '';
        primaryIp.value = newDevice.primary_ip || '';
        macAddress.value = newDevice.mac_address || '';
        typeId.value = newDevice.type_id !== undefined && newDevice.type_id !== null ? Number(newDevice.type_id) : 5;
        groupId.value = newDevice.group_id !== undefined && newDevice.group_id !== null ? Number(newDevice.group_id) : 1;
        parentDeviceId.value = newDevice.parent_device_id !== undefined && newDevice.parent_device_id !== null ? Number(newDevice.parent_device_id) : null;
        zone.value = newDevice.zone || '';
        vendor.value = newDevice.vendor || '';
        model.value = newDevice.model || '';
        isMonitored.value = newDevice.is_monitored ?? true;
      } else {
        displayName.value = '';
        primaryIp.value = '';
        macAddress.value = '';
        typeId.value = 5;
        groupId.value = flatGroups.value.length > 0 ? flatGroups.value[0].id : 1;
        parentDeviceId.value = null;
        zone.value = 'Computer Lab A';
        vendor.value = '';
        model.value = '';
        isMonitored.value = true;
      }
    }
  },
  { immediate: true }
);

async function handleSubmit() {
  errorMessage.value = null;

  if (!displayName.value.trim() || !primaryIp.value.trim()) {
    errorMessage.value = 'Display name and Primary IP are required.';
    return;
  }

  if (!macAddress.value.trim()) {
    errorMessage.value = 'MAC Address is required.';
    return;
  }
  if (!macRegex.test(macAddress.value.trim())) {
    errorMessage.value = 'Invalid MAC format. Use standard colon or hyphen format (e.g. 00:1A:2B:3C:4D:5E).';
    return;
  }

  isSubmitting.value = true;
  try {
    if (isEditMode.value && props.deviceToEdit && Number(props.deviceToEdit.device_id) > 0) {
      const payload: UpdateDeviceRequest = {
        display_name: displayName.value.trim(),
        primary_ip: primaryIp.value.trim(),
        mac_address: macAddress.value.trim().toUpperCase(),
        type_id: Number(typeId.value),
        group_id: Number(groupId.value),
        parent_device_id: parentDeviceId.value ? Number(parentDeviceId.value) : null,
        zone: zone.value.trim() || null,
        vendor: vendor.value.trim() || null,
        model: model.value.trim() || null,
        is_monitored: isMonitored.value
      };
      await devicesStore.updateDevice(props.deviceToEdit.device_id, payload);
    } else {
      const payload: CreateDeviceRequest = {
        display_name: displayName.value.trim(),
        primary_ip: primaryIp.value.trim(),
        mac_address: macAddress.value.trim().toUpperCase(),
        type_id: Number(typeId.value),
        group_id: Number(groupId.value),
        parent_device_id: parentDeviceId.value ? Number(parentDeviceId.value) : null,
        zone: zone.value.trim() || null,
        vendor: vendor.value.trim() || null,
        model: model.value.trim() || null,
        is_monitored: isMonitored.value
      };
      await devicesStore.createDevice(payload);
    }
    emit('saved');
    emit('close');
  } catch (err: any) {
    errorMessage.value = err.response?.data?.error || err.message || 'Error saving device.';
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <ModalWrapper 
    :is-open="isOpen" 
    :title="isEditMode ? `Edit Device — ${deviceToEdit?.display_name}` : 'Provision New LAN Endpoint / Switch'"
    subtitle="1:1 Relational Schema Mappings with Orphan-Safe Hierarchy"
    @close="emit('close')"
  >
    <div v-if="errorMessage" class="mb-4 p-3 rounded bg-rose-950/40 border border-rose-500/50 text-rose-300 text-xs font-mono">
      {{ errorMessage }}
    </div>

    <form @submit.prevent="handleSubmit" class="space-y-4 text-xs font-mono">
      <div class="grid grid-cols-2 gap-4">
        <!-- Display Name -->
        <div>
          <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Display Name *</label>
          <input
            v-model="displayName"
            type="text"
            required
            placeholder="e.g. LAB1-WORKSTATION-01"
            class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          />
        </div>

        <!-- Primary IP -->
        <div>
          <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Primary IPv4 *</label>
          <input
            v-model="primaryIp"
            type="text"
            required
            placeholder="192.168.1.50"
            class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          />
        </div>
      </div>

      <div class="grid grid-cols-2 gap-4">
        <!-- MAC Address -->
        <div>
          <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">
            MAC Address *
          </label>
          <input
            v-model="macAddress"
            type="text"
            required
            placeholder="00:1A:2B:3C:4D:5E"
            class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          />
        </div>

        <!-- Device Type -->
        <div>
          <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">
            Device Type *
          </label>
          <select
            v-model="typeId"
            class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          >
            <option :value="1">CORE_ROUTER (Core Router / Gateway)</option>
            <option :value="2">AGGREGATE_SWITCH (Distribution Switch)</option>
            <option :value="3">ACCESS_SWITCH (Lab Access Switch)</option>
            <option :value="4">SERVER (Campus Lab Server)</option>
            <option :value="5">PC (Lab Workstation / PC)</option>
          </select>
        </div>
      </div>

      <div class="grid grid-cols-2 gap-4">
        <!-- Lab Group Assignment -->
        <div>
          <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Lab Group Hierarchy *</label>
          <select
            v-model="groupId"
            class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          >
            <option v-for="g in flatGroups" :key="g.id" :value="g.id">
              {{ g.name }}
            </option>
          </select>
        </div>

        <!-- Parent Switch / Uplink -->
        <div>
          <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Upstream Switch / Parent</label>
          <select
            v-model="parentDeviceId"
            class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          >
            <option :value="null">-- Root Backbone (No Parent) --</option>
            <option v-for="p in candidateParents" :key="p.device_id" :value="p.device_id">
              [{{ p.type_code }}] {{ p.display_name }} ({{ p.primary_ip }})
            </option>
          </select>
        </div>
      </div>

      <div class="grid grid-cols-3 gap-3">
        <!-- Zone -->
        <div>
          <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Location / Zone</label>
          <input
            v-model="zone"
            type="text"
            placeholder="e.g. Lab 4B - CS Wing"
            class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          />
        </div>

        <!-- Vendor -->
        <div>
          <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Hardware Vendor</label>
          <input
            v-model="vendor"
            type="text"
            placeholder="Dell, HP, Cisco"
            class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          />
        </div>

        <!-- Model -->
        <div>
          <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold">Device Model</label>
          <input
            v-model="model"
            type="text"
            placeholder="OptiPlex 7090"
            class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan"
          />
        </div>
      </div>

      <!-- Monitoring Active Checkbox -->
      <div class="flex items-center space-x-2 pt-2">
        <input
          v-model="isMonitored"
          type="checkbox"
          id="is-monitored-chk"
          class="rounded bg-tactical-panel border-tactical-border text-tactical-cyan focus:ring-0"
        />
        <label for="is-monitored-chk" class="text-slate-300 text-xs">
          Enable Active Telemetry Monitoring (5s ICMP Ping & Periodic WMI/SNMP Host Health)
        </label>
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
        @click="handleSubmit"
        :disabled="isSubmitting"
        type="button"
        class="px-5 py-2 rounded text-xs font-mono font-bold bg-tactical-cyan text-slate-950 hover:bg-cyan-400 transition shadow-[0_0_12px_rgba(0,240,255,0.3)] disabled:opacity-50"
      >
        {{ isSubmitting ? 'Saving...' : (isEditMode ? 'Update Device' : 'Provision Device') }}
      </button>
    </template>
  </ModalWrapper>
</template>
