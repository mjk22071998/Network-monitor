<script setup lang="ts">
import { X } from 'lucide-vue-next';

defineProps<{
  isOpen: boolean;
  title: string;
  subtitle?: string;
  maxWidthClass?: string;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();
</script>

<template>
  <div 
    v-if="isOpen" 
    class="fixed inset-0 z-50 bg-black/85 backdrop-blur-sm flex items-center justify-center p-4 select-none animate-in fade-in duration-200"
    @click.self="emit('close')"
  >
    <div 
      class="w-full bg-[#080d1a] border-2 border-tactical-cyan/40 rounded-xl shadow-2xl shadow-cyan-950/50 flex flex-col overflow-hidden max-h-[90vh]"
      :class="maxWidthClass || 'max-w-xl'"
    >
      <!-- Header -->
      <div class="h-12 border-b border-tactical-border bg-tactical-surface px-5 flex items-center justify-between">
        <div class="flex items-center space-x-2">
          <div class="w-2.5 h-2.5 rounded-full bg-tactical-cyan animate-pulse"></div>
          <div>
            <h3 class="font-mono text-xs font-bold uppercase tracking-wider text-slate-100">{{ title }}</h3>
            <p v-if="subtitle" class="font-mono text-[9px] text-slate-500">{{ subtitle }}</p>
          </div>
        </div>

        <button 
          @click="emit('close')"
          class="p-1 rounded text-slate-400 hover:text-white hover:bg-slate-800 transition"
        >
          <X class="w-4 h-4" />
        </button>
      </div>

      <!-- Body Slot -->
      <div class="p-5 overflow-y-auto font-sans text-slate-200">
        <slot />
      </div>

      <!-- Optional Footer Slot -->
      <div v-if="$slots.footer" class="h-14 border-t border-tactical-border bg-tactical-surface/80 px-5 flex items-center justify-end space-x-3">
        <slot name="footer" />
      </div>
    </div>
  </div>
</template>
