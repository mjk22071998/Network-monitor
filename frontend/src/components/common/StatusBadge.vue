<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  status?: string | null;
  isMaintenance?: boolean;
}>();

const badgeConfig = computed(() => {
  if (props.isMaintenance) {
    return {
      label: 'MAINTENANCE',
      bgClass: 'bg-purple-950/40 border-purple-500/50 text-purple-300',
      dotClass: 'bg-purple-400'
    };
  }

  const s = props.status?.toUpperCase() || 'UNKNOWN';

  switch (s) {
    case 'REACHABLE':
    case 'ONLINE':
    case 'VALID':
    case 'AVAILABLE':
      return {
        label: 'REACHABLE',
        bgClass: 'bg-emerald-950/40 border-emerald-500/50 text-emerald-300',
        dotClass: 'bg-emerald-400 animate-pulse'
      };
    case 'UNREACHABLE':
    case 'OFFLINE':
    case 'CONFLICT':
    case 'UNAVAILABLE':
    case 'ERROR':
    case 'SUPPRESSED':
      return {
        label: 'UNREACHABLE',
        bgClass: 'bg-rose-950/40 border-rose-500/50 text-rose-300',
        dotClass: 'bg-rose-500 animate-ping'
      };
    case 'UNKNOWN':
    default:
      return {
        label: s,
        bgClass: 'bg-slate-900 border-slate-700 text-slate-400',
        dotClass: 'bg-slate-500'
      };
  }
});
</script>

<template>
  <span 
    class="inline-flex items-center space-x-1.5 px-2 py-0.5 rounded text-[10px] font-mono font-semibold border tracking-wider"
    :class="badgeConfig.bgClass"
  >
    <span class="w-1.5 h-1.5 rounded-full" :class="badgeConfig.dotClass"></span>
    <span>{{ badgeConfig.label }}</span>
  </span>
</template>
