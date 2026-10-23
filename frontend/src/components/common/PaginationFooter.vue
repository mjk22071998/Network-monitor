<script setup lang="ts">
import { computed } from 'vue';
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-vue-next';

const props = defineProps<{
  currentPage: number;
  pageSize: number;
  totalRecords: number;
  pageSizeOptions?: number[];
}>();

const emit = defineEmits<{
  (e: 'update:page', page: number): void;
  (e: 'update:pageSize', size: number): void;
}>();

const options = props.pageSizeOptions || [25, 50, 100, 250];

const totalPages = computed(() => Math.max(1, Math.ceil(props.totalRecords / props.pageSize)));

const startRecord = computed(() => {
  if (props.totalRecords === 0) return 0;
  return (props.currentPage - 1) * props.pageSize + 1;
});

const endRecord = computed(() => {
  return Math.min(props.totalRecords, props.currentPage * props.pageSize);
});

const visiblePages = computed(() => {
  const pages: number[] = [];
  const total = totalPages.value;
  const current = props.currentPage;

  let start = Math.max(1, current - 2);
  let end = Math.min(total, current + 2);

  if (current <= 3) {
    end = Math.min(total, 5);
  }
  if (current >= total - 2) {
    start = Math.max(1, total - 4);
  }

  for (let i = start; i <= end; i++) {
    pages.push(i);
  }
  return pages;
});

function goToPage(page: number) {
  if (page < 1 || page > totalPages.value || page === props.currentPage) return;
  emit('update:page', page);
}

function changePageSize(size: number) {
  if (size === props.pageSize) return;
  emit('update:pageSize', size);
  emit('update:page', 1);
}
</script>

<template>
  <div class="h-12 border-t border-tactical-border bg-tactical-surface/80 px-4 flex items-center justify-between text-xs font-mono select-none">
    <!-- Left: Page Size Selector -->
    <div class="flex items-center space-x-2">
      <span class="text-slate-500 uppercase text-[10px] tracking-wider">Per Page:</span>
      <div class="flex items-center space-x-1">
        <button
          v-for="opt in options"
          :key="opt"
          @click="changePageSize(opt)"
          class="px-2 py-0.5 rounded text-[11px] transition"
          :class="opt === pageSize 
            ? 'bg-tactical-cyan/20 border border-tactical-cyan text-tactical-cyan font-bold' 
            : 'bg-tactical-panel border border-tactical-border text-slate-400 hover:text-slate-200'"
        >
          {{ opt }}
        </button>
      </div>
    </div>

    <!-- Center: Range Counter -->
    <div class="text-slate-400 text-[11px]">
      Showing <span class="text-tactical-cyan font-semibold">{{ startRecord }}</span> - 
      <span class="text-tactical-cyan font-semibold">{{ endRecord }}</span> of 
      <span class="text-slate-200 font-semibold">{{ totalRecords }}</span> records
    </div>

    <!-- Right: Page Navigation Pills -->
    <div class="flex items-center space-x-1">
      <button
        @click="goToPage(1)"
        :disabled="currentPage <= 1"
        class="p-1 rounded bg-tactical-panel border border-tactical-border hover:border-tactical-cyan/60 text-slate-400 hover:text-tactical-cyan disabled:opacity-30 disabled:pointer-events-none transition"
        title="First Page"
      >
        <ChevronsLeft class="w-3.5 h-3.5" />
      </button>

      <button
        @click="goToPage(currentPage - 1)"
        :disabled="currentPage <= 1"
        class="p-1 rounded bg-tactical-panel border border-tactical-border hover:border-tactical-cyan/60 text-slate-400 hover:text-tactical-cyan disabled:opacity-30 disabled:pointer-events-none transition"
        title="Previous Page"
      >
        <ChevronLeft class="w-3.5 h-3.5" />
      </button>

      <div class="flex items-center space-x-1 px-1">
        <button
          v-for="p in visiblePages"
          :key="p"
          @click="goToPage(p)"
          class="w-7 h-7 rounded text-[11px] font-semibold flex items-center justify-center transition"
          :class="p === currentPage
            ? 'bg-tactical-cyan text-slate-950 font-bold shadow-[0_0_10px_rgba(0,240,255,0.4)]'
            : 'bg-tactical-panel border border-tactical-border text-slate-400 hover:text-slate-200'"
        >
          {{ p }}
        </button>
      </div>

      <button
        @click="goToPage(currentPage + 1)"
        :disabled="currentPage >= totalPages"
        class="p-1 rounded bg-tactical-panel border border-tactical-border hover:border-tactical-cyan/60 text-slate-400 hover:text-tactical-cyan disabled:opacity-30 disabled:pointer-events-none transition"
        title="Next Page"
      >
        <ChevronRight class="w-3.5 h-3.5" />
      </button>

      <button
        @click="goToPage(totalPages)"
        :disabled="currentPage >= totalPages"
        class="p-1 rounded bg-tactical-panel border border-tactical-border hover:border-tactical-cyan/60 text-slate-400 hover:text-tactical-cyan disabled:opacity-30 disabled:pointer-events-none transition"
        title="Last Page"
      >
        <ChevronsRight class="w-3.5 h-3.5" />
      </button>
    </div>
  </div>
</template>
