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
    class="absolute inset-0 flex flex-col w-full bg-linear-to-br from-[#f8fafc] via-[#f1f5f9] to-[#e2e8f0] font-sans antialiased text-slate-800 overflow-hidden animate-fade-in"
  >
    
    <header class="shrink-0 bg-white/50 backdrop-blur-md border-b border-slate-200/40 shadow-sm z-[100] w-full relative">
      <div class="max-w-screen-2xl mx-auto px-6 py-3 flex items-center justify-between gap-4">
        
        <!-- LEFT SIDE: Title & Subtitle -->
        <div class="flex items-center gap-4 shrink-0">
          <div class="flex flex-col text-left shrink-0">
            <h2 class="text-sm md:text-base font-black text-slate-900 tracking-tight leading-tight whitespace-nowrap">{{ title }}</h2>
            <p v-if="subtitle" class="text-[10px] font-bold text-slate-400 uppercase tracking-widest mt-0.5 leading-none">{{ subtitle }}</p>
          </div>
          
          <!-- Dropdown Report Switcher (Only on balance-sheet or income-statement) -->
          <div v-if="['/balance-sheet', '/income-statement'].includes(router.currentRoute.value.path)" class="ml-2">
            <select 
              :value="router.currentRoute.value.path" 
              @change="event => router.push(event.target.value)"
              class="bg-white border border-slate-200/80 rounded-xl px-3 py-1.5 text-xs font-black text-slate-700 outline-none cursor-pointer focus:ring-2 focus:ring-cyan-500 shadow-sm transition-all"
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

        <!-- RIGHT SIDE: Actions Slot + Search + Notifications + User Profile + Logout -->
        <div class="flex items-center gap-4 shrink-0 pl-4 border-l border-slate-200/50">
          
          <slot name="actions"></slot>


          <!-- Notification Bell -->
          <button class="w-8 h-8 rounded-full bg-white/60 border border-slate-200/40 hover:bg-white hover:border-slate-200 flex items-center justify-center shadow-sm relative transition-all active:scale-95 group">
            <span class="text-sm group-hover:rotate-12 transition-transform">🔔</span>
            <span class="absolute top-1 right-1 w-1.5 h-1.5 rounded-full bg-[#00b0ff] ring-1 ring-white"></span>
          </button>

          <!-- Divider -->
          <div class="w-px h-5 bg-slate-200/50"></div>

          <!-- Profile Info & Logout -->
          <div class="flex items-center gap-2.5 pl-1" v-if="authStore.user">
            <!-- Avatar Circle -->
            <div class="w-7 h-7 rounded-full bg-[#d2f3ee]/60 border border-[#a2e8da] text-[#0d5952] flex items-center justify-center text-[11px] font-black uppercase shadow-inner">
              {{ authStore.user.username.charAt(0) }}
            </div>
            <div class="flex flex-col text-left hidden lg:block leading-none">
              <span class="text-[11px] font-black text-slate-800 leading-none">{{ authStore.user.username }}</span>
              <span class="text-[8px] font-bold text-slate-400 leading-none mt-0.5 uppercase tracking-wider">
                {{ authStore.isAdmin ? 'Admin' : 'User' }}
              </span>
            </div>
          </div>

          <!-- Premium Logout Button -->
          <button @click="handleLogout" class="flex items-center gap-1.5 text-[11px] font-bold text-slate-500 hover:text-rose-600 hover:bg-rose-50/50 border border-transparent hover:border-rose-200/50 px-2.5 py-1.5 rounded-xl transition-all duration-200 active:scale-95 group shrink-0">
            <svg class="w-3.5 h-3.5 text-slate-400 group-hover:text-rose-500 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"></path></svg>
            <span>Logout</span>
          </button>

        </div>

      </div>
    </header>

    <main class="grow flex flex-col min-h-0 w-full relative z-10">
      <slot></slot>
    </main>
  </div>
</template>

<style>

.custom-scrollbar::-webkit-scrollbar { width: 10px; height: 10px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background-color: #cbd5e1; border-radius: 12px; }
.custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: #94a3b8; }

.animate-fade-in {
  animation: fadeIn 0.4s ease-out;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(5px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>