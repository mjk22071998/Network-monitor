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

const isEditMode = computed(() => !!props.deviceToEdit);

const flatGroups = computed(() => groupsStore.flattenGroups(groupsStore.groupsTree));

// Potential parents (Core Routers and Switches)
const candidateParents = computed(() => {
  return devicesStore.devices.filter(d => 
    (d.type_id === 1 || d.type_id === 2 || d.type_id === 3) &&
    (!props.deviceToEdit || d.device_id !== props.deviceToEdit.device_id)
  );
});

// MAC Regex: ^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$
const macRegex = /^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$/;

watch(() => props.isOpen, (newVal) => {
  if (newVal) {
    errorMessage.value = null;
    if (props.deviceToEdit) {
      displayName.value = props.deviceToEdit.display_name;
      primaryIp.value = props.deviceToEdit.primary_ip;
      macAddress.value = props.deviceToEdit.mac_address;
      typeId.value = props.deviceToEdit.type_id;
      groupId.value = props.deviceToEdit.group_id;
      parentDeviceId.value = props.deviceToEdit.parent_device_id;
      zone.value = props.deviceToEdit.zone || '';
      vendor.value = props.deviceToEdit.vendor || '';
      model.value = props.deviceToEdit.model || '';
      isMonitored.value = props.deviceToEdit.is_monitored;
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
});

async function handleSubmit() {
  errorMessage.value = null;

  if (!displayName.value.trim() || !primaryIp.value.trim()) {
    errorMessage.value = 'Display name and Primary IP are required.';
    return;
  }

  if (!isEditMode.value) {
    if (!macAddress.value.trim()) {
      errorMessage.value = 'MAC Address is required for new devices.';
      return;
    }
    if (!macRegex.test(macAddress.value.trim())) {
      errorMessage.value = 'Invalid MAC format. Use standard colon or hyphen format (e.g. 00:1A:2B:3C:4D:5E).';
      return;
    }
  }

  isSubmitting.value = true;
  try {
    if (isEditMode.value && props.deviceToEdit) {
      const payload: UpdateDeviceRequest = {
        display_name: displayName.value.trim(),
        primary_ip: primaryIp.value.trim(),
        group_id: groupId.value,
        parent_device_id: parentDeviceId.value,
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
        type_id: typeId.value,
        group_id: groupId.value,
        parent_device_id: parentDeviceId.value,
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
        <!-- MAC Address (Immutable after creation) -->
        <div>
          <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold flex items-center justify-between">
            <span>MAC Address *</span>
            <span v-if="isEditMode" class="text-[9px] text-slate-500">(Locked)</span>
          </label>
          <input
            v-model="macAddress"
            type="text"
            :disabled="isEditMode"
            required
            placeholder="00:1A:2B:3C:4D:5E"
            class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan disabled:opacity-50"
          />
        </div>

        <!-- Device Type -->
        <div>
          <label class="block text-slate-400 mb-1 uppercase tracking-wider text-[10px] font-bold flex items-center justify-between">
            <span>Device Type *</span>
            <span v-if="isEditMode" class="text-[9px] text-slate-500">(Locked)</span>
          </label>
          <select
            v-model="typeId"
            :disabled="isEditMode"
            class="w-full bg-tactical-panel border border-tactical-border rounded px-3 py-2 text-slate-200 focus:outline-none focus:border-tactical-cyan disabled:opacity-50"
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
