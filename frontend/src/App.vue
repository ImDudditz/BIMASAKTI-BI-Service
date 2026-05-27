<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { RouterView, RouterLink, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'

import { useReportFilterStore } from './stores/reportFilters'
import { useGlobalModalStore } from '@/stores/globalModal'
import { useAuthStore } from '@/stores/auth'

const highriseBg = new URL('./assets/highrise_bg.svg', import.meta.url).href

const router = useRouter()
const authStore = useAuthStore()

const filterStore = useReportFilterStore()
const { companyName, companyId } = storeToRefs(filterStore)

onMounted(async () => {
  if (!document.getElementById('google-font-royale')) {
    const link = document.createElement('link')
    link.id = 'google-font-royale'
    link.rel = 'stylesheet'
    link.href = 'https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;600;800;900&family=Plus+Jakarta+Sans:wght@300;400;500;600;700;800&family=Share+Tech+Mono&display=swap'
    document.head.appendChild(link)
  }
  if (authStore.isAuthenticated) {
    await authStore.fetchPermissions()
  }
})



const isAuthPage = computed(() => {
  return authStore.isAuthenticated && router.currentRoute.value.path !== '/' && router.currentRoute.value.path !== '/login'
})

const activePageTitle = computed(() => {
  const path = router.currentRoute.value.path
  if (path === '/dashboard') return 'Property Management Dashboard'
  if (path === '/dashboard/operation') return 'Properties Operation Overview'
  if (path === '/dashboard/maintenance') return 'Service & Maintenance'
  if (path === '/balance-sheet') return 'Balance Sheet'
  if (path === '/income-statement') return 'Income Statement'
  if (path === '/settings') return 'Account Mapping Wizard'
  return 'Property Management Dashboard'
})

// --- DYNAMIC BROWSER TAB TITLE ---
watch(
  [() => router.currentRoute.value.path, companyName, companyId],
  ([path, name, id]) => {
    if (path === '/login') {
      document.title = 'Bimasakti BI Service - Login'
    } else if (name) {
      document.title = `${name} - Financial Reports`
    } else if (id) {
      document.title = `Financial Reports - ${id}`
    } else {
      document.title = 'Financial Reports'
    }
  },
  { immediate: true }
)

const modalStore = useGlobalModalStore()
const { isOpen, type, title, message, confirmText, cancelText, icon } = storeToRefs(modalStore)

// --- SIDEBAR ACCORDION CONTROLS ---
const expandedMenus = ref({
  financials: true,
  tenancy: false,
  services: false
})

const toggleMenu = (menu) => {
  expandedMenus.value[menu] = !expandedMenus.value[menu]
}

watch(
  () => router.currentRoute.value.path,
  (path) => {
    if (['/dashboard', '/balance-sheet', '/income-statement', '/print'].includes(path)) {
      expandedMenus.value.financials = true
    }
    if (['/dashboard/operation', '/tenancy-report'].includes(path)) {
      expandedMenus.value.tenancy = true
    }
    if (['/dashboard/maintenance', '/services-report'].includes(path)) {
      expandedMenus.value.services = true
    }
  },
  { immediate: true }
)

const logoExt = ref('png')

const activeLogoUrl = computed(() => {
  if (!logoExt.value) return ''
  try {
    return new URL(`../../backend/assets/${companyId.value}/img/${companyId.value}_logo.${logoExt.value}`, import.meta.url).href
  } catch { return '' }
})

const handleLogoError = () => {
  if (logoExt.value === 'png') logoExt.value = 'jpg'
  else logoExt.value = '' 
}

// --- LOGOUT LOGIC ---
const handleLogout = async () => {
  // Await the asynchronous logout call to clear the server-side cookie 
  // and update the Pinia auth state before attempting the redirect.
  await authStore.logout()
  router.push('/')
}
</script>

<template>
  <!-- Decorative Royale Light Blue Metallic Ribbon at Top -->
  <div class="w-full h-1.5 bg-gradient-to-r from-sky-400 via-blue-500 to-sky-300 relative z-30 shrink-0"></div>

  <!-- Main layout container with global styles -->
  <div class="h-screen w-screen flex flex-col font-['Plus_Jakarta_Sans',sans-serif] text-[#1c2e4a] overflow-hidden relative bg-[#f1f6fa] select-none selection:bg-sky-500/30">
    
    <!-- Beautiful Architectural Skyscraper Grayscale Background with 75% Transparency (opacity-20) -->
    <div v-if="isAuthPage" class="absolute inset-x-0 top-0 h-[750px] lg:h-[900px] pointer-events-none opacity-20 overflow-hidden z-0 flex items-end justify-center select-none">
      <img :src="highriseBg" alt="Skyscraper Background" class="w-full h-full object-cover">
    </div>

    <!-- AUTHENTICATED PREMIUM WINDOWS XP ROYALE DASHBOARD SHELL -->
    <div v-if="isAuthPage" class="flex-1 flex gap-6 p-6 overflow-hidden h-full w-full relative z-10">
      
      <!-- LEFT-HAND WINDOWS XP ROYALE SIDEBAR (Explorer Task Pane style) -->
      <aside class="w-[260px] bg-gradient-to-b from-[#d6e5f8] to-[#a2c6ec] border border-[#95bfe9] shadow-xl rounded-2xl flex flex-col p-4 shrink-0 z-40 transition-all duration-500 hover:shadow-sky-100/40 relative select-none">
        
        <!-- Brand section with Company Logo & Name inside an XP Group Box panel -->
        <div class="bg-white/80 rounded-xl border border-[#95bfe9] p-3 flex flex-col items-center justify-center text-center shadow-sm shrink-0 gap-2 mb-4">
          <div v-if="activeLogoUrl" class="flex items-center justify-center w-full max-h-16 px-2">
            <img :src="activeLogoUrl" @error="handleLogoError" alt="Company Logo" class="max-w-full max-h-12 object-contain">
          </div>
          <div class="flex flex-col items-center min-w-0 max-w-full">
            <h1 class="text-[10px] font-black text-[#0c3c8c] tracking-widest uppercase leading-snug break-words whitespace-normal px-1 font-['Outfit']" :title="companyName">
              {{ companyName }}
            </h1>
          </div>
        </div>

        <!-- Sidebar Navigation List -->
        <nav class="flex-grow overflow-y-auto pr-1 space-y-3 custom-scrollbar">
          
          <!-- Panel 1: System Console -->
          <div class="bg-white/70 rounded-xl overflow-hidden border border-[#95bfe9] shadow-sm">
            <div class="px-3.5 py-2 flex items-center justify-between bg-gradient-to-r from-[#215dc6] to-[#6da3f6] text-white text-[10px] font-black uppercase tracking-wider font-['Outfit']">
              <span>System Console</span>
            </div>
            <div class="p-1.5 flex flex-col gap-1">
              <RouterLink 
                to="/overview" 
                class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-bold transition-all duration-200 outline-none border"
                :class="router.currentRoute.value.path === '/overview' 
                  ? 'bg-[#3b82f6]/15 text-[#0c3c8c] border-[#3b82f6]/30' 
                  : 'text-slate-600 border-transparent hover:bg-[#3b82f6]/5 hover:text-[#0c3c8c]'"
              >
                <span class="text-sm">🏠</span>
                <span>Executive Overview</span>
              </RouterLink>
              
              <RouterLink 
                to="/settings" 
                class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-bold transition-all duration-200 outline-none border"
                :class="router.currentRoute.value.path === '/settings' 
                  ? 'bg-[#3b82f6]/15 text-[#0c3c8c] border-[#3b82f6]/30' 
                  : 'text-slate-600 border-transparent hover:bg-[#3b82f6]/5 hover:text-[#0c3c8c]'"
              >
                <span class="text-sm">⚙️</span>
                <span>Settings Wizard</span>
              </RouterLink>
            </div>
          </div>

          <!-- Panel 2: Financial Systems -->
          <div class="bg-white/70 rounded-xl overflow-hidden border border-[#95bfe9] shadow-sm">
            <button 
              @click="toggleMenu('financials')"
              class="w-full px-3.5 py-2 flex items-center justify-between bg-gradient-to-r from-[#215dc6] to-[#6da3f6] text-white text-[10px] font-black uppercase tracking-wider font-['Outfit'] outline-none"
            >
              <div class="flex items-center gap-1.5">
                <span>💳</span>
                <span>Financials</span>
              </div>
              <span class="text-[9px] transition-transform duration-300" :class="{ 'rotate-180': expandedMenus.financials }">▼</span>
            </button>
            
            <div 
              v-show="expandedMenus.financials" 
              class="p-1.5 flex flex-col gap-1 transition-all duration-300"
            >
              <RouterLink 
                to="/dashboard" 
                class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-bold border transition-all duration-200 outline-none"
                :class="router.currentRoute.value.path === '/dashboard'
                  ? 'bg-[#3b82f6]/15 text-[#0c3c8c] border-[#3b82f6]/30'
                  : 'text-slate-600 border-transparent hover:bg-[#3b82f6]/5 hover:text-[#0c3c8c]'"
              >
                <span class="text-[10px]">•</span>
                <span>Dashboard</span>
              </RouterLink>
              
              <RouterLink 
                to="/balance-sheet" 
                class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-bold border transition-all duration-200 outline-none"
                :class="['/balance-sheet', '/income-statement'].includes(router.currentRoute.value.path)
                  ? 'bg-[#3b82f6]/15 text-[#0c3c8c] border-[#3b82f6]/30'
                  : 'text-slate-600 border-transparent hover:bg-[#3b82f6]/5 hover:text-[#0c3c8c]'"
              >
                <span class="text-[10px]">•</span>
                <span>Reports</span>
              </RouterLink>
              
              <RouterLink 
                to="/print" 
                class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-bold border transition-all duration-200 outline-none"
                :class="router.currentRoute.value.path === '/print'
                  ? 'bg-[#3b82f6]/15 text-[#0c3c8c] border-[#3b82f6]/30'
                  : 'text-slate-600 border-transparent hover:bg-[#3b82f6]/5 hover:text-[#0c3c8c]'"
              >
                <span class="text-[10px]">•</span>
                <span>Print</span>
              </RouterLink>
            </div>
          </div>

          <!-- Panel 3: Tenancy Stacks -->
          <div class="bg-white/70 rounded-xl overflow-hidden border border-[#95bfe9] shadow-sm">
            <button 
              @click="toggleMenu('tenancy')"
              class="w-full px-3.5 py-2 flex items-center justify-between bg-gradient-to-r from-[#215dc6] to-[#6da3f6] text-white text-[10px] font-black uppercase tracking-wider font-['Outfit'] outline-none"
            >
              <div class="flex items-center gap-1.5">
                <span>🏢</span>
                <span>Tenancy</span>
              </div>
              <span class="text-[9px] transition-transform duration-300" :class="{ 'rotate-180': expandedMenus.tenancy }">▼</span>
            </button>
            
            <div 
              v-show="expandedMenus.tenancy" 
              class="p-1.5 flex flex-col gap-1 transition-all duration-300"
            >
              <RouterLink 
                to="/dashboard/operation" 
                class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-bold border transition-all duration-200 outline-none"
                :class="router.currentRoute.value.path === '/dashboard/operation'
                  ? 'bg-[#3b82f6]/15 text-[#0c3c8c] border-[#3b82f6]/30'
                  : 'text-slate-600 border-transparent hover:bg-[#3b82f6]/5 hover:text-[#0c3c8c]'"
              >
                <span class="text-[10px]">•</span>
                <span>Dashboard</span>
              </RouterLink>
              
              <RouterLink 
                to="/tenancy-report" 
                class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-bold border transition-all duration-200 outline-none"
                :class="router.currentRoute.value.path === '/tenancy-report'
                  ? 'bg-[#3b82f6]/15 text-[#0c3c8c] border-[#3b82f6]/30'
                  : 'text-slate-600 border-transparent hover:bg-[#3b82f6]/5 hover:text-[#0c3c8c]'"
              >
                <span class="text-[10px]">•</span>
                <span>Reports</span>
              </RouterLink>
            </div>
          </div>

          <!-- Panel 4: Service & Maintenance -->
          <div class="bg-white/70 rounded-xl overflow-hidden border border-[#95bfe9] shadow-sm">
            <button 
              @click="toggleMenu('services')"
              class="w-full px-3.5 py-2 flex items-center justify-between bg-gradient-to-r from-[#215dc6] to-[#6da3f6] text-white text-[10px] font-black uppercase tracking-wider font-['Outfit'] outline-none"
            >
              <div class="flex items-center gap-1.5">
                <span>🔧</span>
                <span>Services</span>
              </div>
              <span class="text-[9px] transition-transform duration-300" :class="{ 'rotate-180': expandedMenus.services }">▼</span>
            </button>
            
            <div 
              v-show="expandedMenus.services" 
              class="p-1.5 flex flex-col gap-1 transition-all duration-300"
            >
              <RouterLink 
                to="/dashboard/maintenance" 
                class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-bold border transition-all duration-200 outline-none"
                :class="router.currentRoute.value.path === '/dashboard/maintenance'
                  ? 'bg-[#3b82f6]/15 text-[#0c3c8c] border-[#3b82f6]/30'
                  : 'text-slate-600 border-transparent hover:bg-[#3b82f6]/5 hover:text-[#0c3c8c]'"
              >
                <span class="text-[10px]">•</span>
                <span>Dashboard</span>
              </RouterLink>
              
              <RouterLink 
                to="/services-report" 
                class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-bold border transition-all duration-200 outline-none"
                :class="router.currentRoute.value.path === '/services-report'
                  ? 'bg-[#3b82f6]/15 text-[#0c3c8c] border-[#3b82f6]/30'
                  : 'text-slate-600 border-transparent hover:bg-[#3b82f6]/5 hover:text-[#0c3c8c]'"
              >
                <span class="text-[10px]">•</span>
                <span>ReportS</span>
              </RouterLink>
            </div>
          </div>



        </nav>

        <div class="flex items-center justify-center gap-1.5 border-t border-[#95bfe9]/50 pt-2 mt-auto">
          <div class="w-5 h-5 rounded bg-[#3b82f6]/10 border border-[#3b82f6]/20 flex items-center justify-center text-[9px] font-black text-[#0c3c8c]">B</div>
          <span class="text-[7.5px] text-[#475569] font-black tracking-widest font-mono">BIMASAKTI BI</span>
        </div>

      </aside>

      <!-- RIGHT-HAND MAIN CONTAINER AREA -->
      <div class="flex-1 flex flex-col overflow-hidden h-full min-w-0">
        
        <!-- Outer window frame representing Windows XP Royale (Energy Blue) window -->
        <main class="flex-1 bg-[#f3f7fd] rounded-2xl border border-[#1f62d4] shadow-[0_20px_50px_-12px_rgba(31,98,212,0.35)] overflow-hidden relative flex flex-col min-h-0">
          
          <!-- Authentic Windows XP Royale Energy Blue Title Bar -->
          <div class="flex items-center justify-between px-4 py-2 border-t border-white/40 bg-gradient-to-b from-[#629ef7] via-[#357ae5] to-[#1b56ca] text-white relative z-20 shrink-0 shadow-[inset_0_1px_0_rgba(255,255,255,0.4)] select-none">
            <div class="flex items-center gap-2">
              <!-- Computer system icon -->
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor" class="w-4 h-4 text-sky-100 drop-shadow-[0_1px_1px_rgba(0,0,0,0.5)]">
                <path stroke-linecap="round" stroke-linejoin="round" d="M9 17.25v1.007a3 3 0 01-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0115 18.257V17.25m6-12V15a2.25 2.25 0 01-2.25 2.25H5.25A2.25 2.25 0 013 15V5.25m18 0A2.25 2.25 0 0018.75 3H5.25A2.25 2.25 0 003 5.25m18 0V12a2.25 2.25 0 01-2.25 2.25H5.25A2.25 2.25 0 013 12V5.25" />
              </svg>
              <!-- Dynamic Title Text -->
              <span class="text-xs font-black tracking-wide text-white drop-shadow-[0_1px_2px_rgba(0,0,0,0.8)] font-['Outfit'] uppercase">
                Bimasakti BI - {{ activePageTitle }}
              </span>
            </div>
            
            <!-- Top Right Action Area (In place of Minimize, Maximize, Close buttons) -->
            <div class="flex items-center gap-4">
              
              <!-- Notification Bell -->
              <button class="w-8 h-8 rounded-full bg-white/60 border border-slate-200/40 hover:bg-white hover:border-slate-200 flex items-center justify-center shadow-sm relative transition-all active:scale-95 group">
                <span class="text-sm group-hover:rotate-12 transition-transform">🔔</span>
                <span class="absolute top-1 right-1 w-1.5 h-1.5 rounded-full bg-[#3c56d6] ring-1 ring-white"></span>
              </button>

              <!-- Divider -->
              <div class="w-px h-5 bg-slate-200/50"></div>

              <!-- Profile Info -->
              <div class="flex items-center gap-2.5 pl-1" v-if="authStore.user">
                <!-- Avatar Circle -->
                <div class="w-7 h-7 rounded-full bg-[#3c56d6]/20 border border-[#3c56d6]/30 text-white flex items-center justify-center text-[11px] font-black uppercase shadow-inner">
                  {{ authStore.user.username.charAt(0) }}
                </div>
                <div class="flex flex-col text-left hidden sm:flex leading-none">
                  <span class="text-[11px] font-black text-white leading-none">{{ authStore.user.username }}</span>
                  <span class="text-[8px] font-bold text-sky-200 leading-none mt-0.5 uppercase tracking-wider">
                    {{ authStore.isAdmin ? 'Admin' : 'User' }}
                  </span>
                </div>
              </div>

              <!-- Logout Button -->
              <button @click="handleLogout" class="flex items-center gap-1.5 text-[11px] font-bold text-slate-500 hover:text-rose-600 hover:bg-rose-50/50 border border-transparent hover:border-rose-200/50 px-2.5 py-1.5 rounded-xl transition-all duration-200 active:scale-95 group shrink-0 bg-white/60 shadow-sm">
                <svg class="w-3.5 h-3.5 text-slate-400 group-hover:text-rose-500 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"></path></svg>
                <span>Logout</span>
              </button>

            </div>
          </div>



          <!-- Main Content Workspace Wrapper -->
          <div class="flex-1 flex flex-col overflow-hidden relative min-h-0 bg-[#f3f7fd]">
            <RouterView class="grow overflow-hidden flex flex-col w-full relative z-10" />
          </div>
        </main>

      </div>

    </div>

    <!-- NON-AUTHENTICATED / DIRECT RENDER LAYOUT (Landing & Login Pages) -->
    <div v-else class="flex-1 w-full h-full flex flex-col overflow-hidden relative z-10 bg-[#f1f6fa]">
      <RouterView class="grow overflow-hidden flex flex-col w-full relative z-10" />
    </div>

    <!-- GLOBAL CONFIRMATION / PROMPT MODAL MANAGER -->
    <Teleport to="body">
      <div v-if="isOpen" class="fixed inset-0 z-[999] flex items-center justify-center p-4">
        <!-- Backdrop -->
        <div class="absolute inset-0 bg-slate-900/40 backdrop-blur-sm" @click="modalStore.closeModal"></div>
        <!-- Themed XP Program Window Dialog Card -->
        <div class="relative bg-[#f3f7fd] rounded-xl shadow-2xl max-w-md w-full overflow-hidden border border-[#1f62d4] animate-slide-up flex flex-col font-['Plus_Jakarta_Sans',sans-serif]">
          <!-- Dialog Title Bar -->
          <div class="flex items-center justify-between px-3 py-1.5 bg-gradient-to-b from-[#629ef7] via-[#357ae5] to-[#1b56ca] text-white select-none">
            <span class="text-[10px] font-black uppercase tracking-wider font-['Outfit']">{{ title || 'Bimasakti BI System Message' }}</span>
            <button @click="modalStore.closeModal" class="w-4 h-4 rounded-full flex items-center justify-center border border-[#9b1708] bg-gradient-to-b from-[#fbae97] via-[#f3664d] to-[#bf2613] hover:from-[#ffbfad] hover:to-[#df301b] transition-all">
              <span class="text-[8px] font-bold text-white">✕</span>
            </button>
          </div>
          
          <!-- Dialog Body -->
          <div class="p-5 flex gap-4">
            <div class="text-3xl bg-white border border-[#95bfe9] p-3 rounded-lg shadow-sm shrink-0 flex items-center justify-center">{{ icon }}</div>
            <div class="w-full flex flex-col text-left">
              <h3 class="text-sm font-black text-[#0f2240] tracking-tight leading-tight font-['Outfit']">{{ title }}</h3>
              <p class="text-xs text-slate-600 font-bold mt-1 leading-relaxed">{{ message }}</p>
              <input 
                v-if="type === 'prompt'" 
                v-model="modalStore.inputValue" 
                type="text" 
                class="mt-3 w-full border border-[#95bfe9] bg-white rounded-lg p-2 text-xs font-bold text-slate-800 focus:border-[#3b82f6] outline-none transition-all" 
                @keyup.enter="modalStore.confirmModal" 
                autofocus
              >
            </div>
          </div>
          
          <!-- Dialog Actions Bar -->
          <div class="bg-[#ebf3fd] px-4 py-2.5 flex items-center justify-end gap-2 border-t border-[#a8cbf3]">
            <button 
              v-if="type !== 'alert'" 
              @click="modalStore.closeModal" 
              class="px-4 py-1.5 text-[10px] font-black text-[#40567a] hover:text-slate-900 border border-[#95bfe9] hover:bg-slate-50 bg-white rounded-md shadow-sm active:scale-95 transition-all"
            >
              {{ cancelText }}
            </button>
            <!-- XP Start-style confirm button (Green) -->
            <button 
              @click="modalStore.confirmModal" 
              class="px-5 py-1.5 text-[10px] font-black text-white bg-gradient-to-b from-[#8fd878] via-[#5cae46] to-[#257317] hover:brightness-110 border border-[#1b5011] rounded-md shadow-md active:scale-95 transition-all relative overflow-hidden"
            >
              <!-- Green glare -->
              <div class="absolute inset-x-0 top-0 h-[40%] bg-white/20"></div>
              <span class="relative z-10">{{ confirmText }}</span>
            </button>
          </div>
        </div>
      </div>
    </Teleport>

  </div>
</template>

<style>
.animate-slide-up { animation: slideUp 0.3s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes slideUp { 
  from { opacity: 0; transform: translateY(15px) scale(0.98); } 
  to { opacity: 1; transform: translateY(0) scale(1); } 
}

/* Custom premium slim scrollbar matching Royale mockup */
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

/* Shine effect utility for Royale premium buttons */
@keyframes shine {
  100% {
    left: 125%;
  }
}
.animate-shine {
  animation: shine 0.85s ease-in-out;
}
</style>