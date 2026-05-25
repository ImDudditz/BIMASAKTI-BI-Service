<script setup>
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

defineProps({
  title: { type: String, required: true },
  subtitle: { type: String, default: '' }
})

const router = useRouter()
const authStore = useAuthStore()

const handleLogout = async () => {
  await authStore.logout()
  router.push('/')
}
</script>

<template>
  <div 
    class="absolute inset-0 flex flex-col w-full bg-[#f3f7fd] font-['Plus_Jakarta_Sans',sans-serif] text-[#1c2e4a] overflow-hidden selection:bg-sky-500/30"
  >
    
    <header class="shrink-0 bg-[#ebf3fd] border-b border-[#a8cbf3] shadow-sm z-[100] w-full relative">
      <div class="max-w-screen-2xl mx-auto px-6 py-3 flex items-center justify-between gap-4">
        
        <!-- LEFT SIDE: Title & Subtitle -->
        <div class="flex items-center gap-4 shrink-0">
          <div class="flex flex-col text-left shrink-0">
            <h2 class="text-sm md:text-base font-black text-[#0f2240] tracking-tight leading-tight whitespace-nowrap font-['Outfit'] uppercase">{{ title }}</h2>
            <p v-if="subtitle" class="text-[9px] font-black text-sky-500 uppercase tracking-widest mt-0.5 leading-none">{{ subtitle }}</p>
          </div>
          
          <!-- Dropdown Report Switcher (Only on balance-sheet or income-statement) -->
          <div v-if="['/balance-sheet', '/income-statement'].includes(router.currentRoute.value.path)" class="ml-2">
            <select 
              :value="router.currentRoute.value.path" 
              @change="event => router.push(event.target.value)"
              class="bg-white border border-[#95bfe9] rounded-lg px-3 py-1.5 text-xs font-black text-[#0c3c8c] focus:border-[#3b82f6] outline-none cursor-pointer shadow-sm transition-all"
            >
              <option value="/balance-sheet">📑 Balance Sheet</option>
              <option value="/income-statement">📊 Income Statement</option>
            </select>
          </div>
        </div>

        <!-- MIDDLE SECTION: Date Selects & Page Controls -->
        <div class="flex-grow flex items-center justify-start pl-6 min-w-0">
          <slot name="controls"></slot>
        </div>

        <!-- RIGHT SIDE: Actions Slot -->
        <div class="flex items-center gap-4 shrink-0" v-if="$slots.actions">
          <slot name="actions"></slot>
        </div>

      </div>
    </header>

    <main class="grow flex flex-col min-h-0 w-full relative z-10 bg-[#f3f7fd]">
      <slot></slot>
    </main>
  </div>
</template>

<style>
.custom-scrollbar::-webkit-scrollbar,
.custom-scroll::-webkit-scrollbar {
  width: 4px;
  height: 4px;
}
.custom-scrollbar::-webkit-scrollbar-track,
.custom-scroll::-webkit-scrollbar-track {
  background: rgba(56, 189, 248, 0.02);
}
.custom-scrollbar::-webkit-scrollbar-thumb,
.custom-scroll::-webkit-scrollbar-thumb {
  background: rgba(56, 189, 248, 0.25);
  border-radius: 4px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover,
.custom-scroll::-webkit-scrollbar-thumb:hover {
  background: rgba(56, 189, 248, 0.45);
}

.animate-fade-in {
  animation: fadeIn 0.4s ease-out;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(5px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>