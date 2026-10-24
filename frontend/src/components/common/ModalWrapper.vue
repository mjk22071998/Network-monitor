<script setup lang="ts">
import { onMounted, onBeforeUnmount } from 'vue';
import { X } from 'lucide-vue-next';

const props = defineProps<{
  isOpen: boolean;
  title: string;
  subtitle?: string;
  maxWidthClass?: string;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();

// Prevents accidental modal closing when selecting text inside an input
// and releasing the mouse outside the dialog on the backdrop.
let isBackdropMouseDown = false;

function handleBackdropMouseDown(event: MouseEvent) {
  // Only register if mousedown actually started on the backdrop overlay itself
  isBackdropMouseDown = event.target === event.currentTarget;
}

function handleBackdropMouseUp(event: MouseEvent) {
  // Only close if BOTH mousedown AND mouseup occurred directly on the backdrop overlay
  if (isBackdropMouseDown && event.target === event.currentTarget) {
    emit('close');
  }
  isBackdropMouseDown = false;
}

function handleKeyDown(event: KeyboardEvent) {
  if (props.isOpen && event.key === 'Escape') {
    emit('close');
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleKeyDown);
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleKeyDown);
});
</script>

<template>
  <div 
    v-if="isOpen" 
    class="fixed inset-0 z-50 bg-black/85 backdrop-blur-sm flex items-center justify-center p-4 select-none animate-in fade-in duration-200"
    @mousedown="handleBackdropMouseDown"
    @mouseup="handleBackdropMouseUp"
  >
    <div 
      class="w-full bg-[#080d1a] border-2 border-tactical-cyan/40 rounded-xl shadow-2xl shadow-cyan-950/50 flex flex-col overflow-hidden max-h-[90vh] select-text"
      :class="maxWidthClass || 'max-w-xl'"
      @mousedown.stop
      @click.stop
    >
      <!-- Header -->
      <div class="h-12 border-b border-tactical-border bg-tactical-surface px-5 flex items-center justify-between select-none">
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
      <div v-if="$slots.footer" class="h-14 border-t border-tactical-border bg-tactical-surface/80 px-5 flex items-center justify-end space-x-3 select-none">
        <slot name="footer" />
      </div>
    </div>
  </div>
</template>
