<script setup>
defineOptions({ name: 'LandingPage' })
import { ref, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import api from '@/services/api'

// Core assets
const bimasaktiLogo = new URL('../../../BI-API/assets/BMS/img/BMS_logo.png', import.meta.url).href
const propertyOperations = new URL('../assets/property_operations.png', import.meta.url).href
const tenantMobileApp = new URL('../assets/tenant_mobile_app.png', import.meta.url).href
const highriseBg = new URL('../assets/highrise_bg.svg', import.meta.url).href

const router = useRouter()
const authStore = useAuthStore()

// State for interactive SaaS Dashboard
const activeTab = ref('accounting') // 'accounting', 'tenant'
const syncing = ref(false)
const syncProgress = ref(0)
const syncCount = ref(1482)


// High-fidelity Accounting Mockup States
const totalInvoiced = ref(842.1)
const vatInvoiced = ref(84.21)
const invoiceStatus = ref('PROCESSING')

const initialLogs = [
  'SYSTEM: Initializing BMS_BI_SERVICE cluster...',
  'SECURE: TLS handshake established with Microsoft Azure (Southeast Asia)',
  'SQLITE: SQLite database stream connected on local port 8001',
  'HEALTH: All background nodes operational. Sync rate 99.98%',
  'MONITOR: Ready for manual or automated cron executions.'
]

const terminalLogs = ref([...initialLogs])
const terminalRef = ref(null)



// Simulated continuous background activities
let backgroundLogInterval = null
const logTemplates = [
  'INFO: Queried General Ledger balances for Company BMS_CORP',
  'DEBUG: Cached 45 tenant utility requests in memory store',
  'AZURE: Health check ping returned 200 OK from az-server-sea',
  'INFO: Compiled monthly e-Faktur tax ledger output for auditing',
  'SYSTEM: Vacuumed SQLite journal buffers to preserve memory'
]

// Scroll terminal helper
const scrollToBottom = () => {
  nextTick(() => {
    if (terminalRef.value) {
      terminalRef.value.scrollTop = terminalRef.value.scrollHeight
    }
  })
}

// Watch logs to auto-scroll
watch(terminalLogs, () => {
  scrollToBottom()
}, { deep: true })

const backendPort = ref(8001)
const frontendPort = ref(8002)

const runSimulatedSync = () => {
  if (syncing.value) return
  
  syncing.value = true
  syncProgress.value = 0
  invoiceStatus.value = 'PROCESSING'
  
  terminalLogs.value.push('SYSTEM: Starting manual sync command from admin UI...')
  
  const steps = [
    { progress: 15, log: 'SQLITE: Accessing secure SQLite binary tables...' },
    { progress: 35, log: `PROXY: Routing sync traffic via local gateway port ${backendPort.value}...` },
    { progress: 55, log: 'TAX: Formatting VAT declarations for direct e-Faktur linking...' },
    { progress: 75, log: 'SQLITE: Reconciling ledger for Invoice #INV-2026-083 (Rp 62.0M)...' },
    { progress: 92, log: 'SYNC: Pushing billing records and e-Faktur outputs to Azure DB...' },
    { progress: 100, log: 'SUCCESS: Synchronization confirmed. e-Faktur generated.' }
  ]
  
  let currentStep = 0
  const interval = setInterval(() => {
    if (currentStep < steps.length) {
      const step = steps[currentStep]
      syncProgress.value = step.progress
      terminalLogs.value.push(step.log)
      currentStep++
    } else {
      clearInterval(interval)
      syncing.value = false
      syncCount.value += 1
      totalInvoiced.value = 904.1
      vatInvoiced.value = 90.41
      invoiceStatus.value = 'PAID'
      terminalLogs.value.push(`MONITOR: Sync transaction #${syncCount.value} completed in 1.8s. All buffers green.`)
    }
  }, 300)
}

const handleActionClick = () => {
  if (authStore.isAuthenticated) {
    router.push('/overview')
  } else {
    router.push('/login')
  }
}

// Scroll detection for animations and Google Fonts loading
onMounted(async () => {
  document.title = 'BIMASAKTI BI'

  try {
    const res = await api.get('/auth/config')
    if (res.data) {
      backendPort.value = res.data.backendPort || 8001
      frontendPort.value = res.data.frontendPort || 8002
      
      // Update initial terminal logs with correct port
      terminalLogs.value = terminalLogs.value.map(log => 
        log.replace('8001', backendPort.value.toString())
      )
    }
  } catch (e) {
    console.warn('Failed to load dynamic port configuration, using defaults:', e)
  }

  // Load Outfit and Plus Jakarta Sans Google Fonts
  if (!document.getElementById('google-font-royale')) {
    const link = document.createElement('link')
    link.id = 'google-font-royale'
    link.rel = 'stylesheet'
    link.href = 'https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;600;800;900&family=Plus+Jakarta+Sans:wght@300;400;500;600;700;800&family=Share+Tech+Mono&display=swap'
    document.head.appendChild(link)
  }

  // Set up dynamic log updates to keep terminal looking live
  backgroundLogInterval = setInterval(() => {
    if (!syncing.value) {
      const randomLog = logTemplates[Math.floor(Math.random() * logTemplates.length)]
      const timestamp = new Date().toLocaleTimeString()
      terminalLogs.value.push(`[${timestamp}] ${randomLog}`)
      if (terminalLogs.value.length > 50) {
        terminalLogs.value.shift()
      }
    }
  }, 8000)

  const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        entry.target.classList.add('in-view')
      }
    })
  }, { threshold: 0.05 })

  document.querySelectorAll('.scroll-animate').forEach(el => {
    observer.observe(el)
  })
  
  scrollToBottom()
})

onUnmounted(() => {
  if (backgroundLogInterval) {
    clearInterval(backgroundLogInterval)
  }
})

const scrollToSection = (id) => {
  const element = document.getElementById(id)
  if (element) {
    element.scrollIntoView({ behavior: 'smooth' })
  }
}
</script>

<template>
  <div class="min-h-screen w-full bg-[#f1f6fa] text-[#1c2e4a] font-['Plus_Jakarta_Sans',sans-serif] overflow-y-auto overflow-x-hidden scroll-smooth relative custom-scroll selection:bg-sky-500/30">
    
    <!-- Beautiful Architectural Skyscraper Grayscale Background with 75% Transparency (opacity-25) -->
    <div class="absolute inset-x-0 top-0 h-[60vh] lg:h-[80vh] min-h-[600px] max-h-[1000px] pointer-events-none opacity-25 overflow-hidden z-0 flex items-end justify-center select-none">
      <img :src="highriseBg" alt="Skyscraper Background" class="w-full h-full object-cover">
    </div>

    <!-- Decorative Royale Light Blue Metallic Ribbon at Top -->
    <div class="w-full h-1.5 bg-gradient-to-r from-sky-400 via-blue-500 to-sky-300 relative z-30"></div>

    <!-- --- PREVENT BLUR NAVBAR --- -->
    <header class="w-full sticky top-0 z-50 bg-[#f1f6fa]/85 backdrop-blur-md border-b border-sky-100 shadow-[0_2px_15px_-3px_rgba(56,189,248,0.1)]">
      <div class="max-w-7xl 2xl:max-w-[1440px] mx-auto px-6 py-4 flex items-center justify-between">
        <!-- Brand/Logo with Windows XP Royale gloss effect -->
        <div class="flex items-center gap-3 group cursor-pointer" @click="scrollToSection('hero')">
          <div class="relative w-8 h-8 rounded-lg bg-gradient-to-tr from-sky-500 to-blue-600 flex items-center justify-center shadow-[0_2px_8px_rgba(56,189,248,0.4)] overflow-hidden">
            <div class="absolute inset-0 bg-gradient-to-b from-white/20 to-transparent"></div>
            <span class="text-white font-['Outfit'] font-black text-sm tracking-wider">B</span>
          </div>
          <div>
            <span class="font-['Outfit'] font-black text-sm tracking-widest text-[#0f2240] uppercase">Bimasakti</span>
            <span class="text-[10px] block font-bold text-sky-500 uppercase tracking-widest leading-none font-mono">BI Service</span>
          </div>
        </div>

        <!-- Sleek Riot-style Nav Items -->
        <nav class="hidden md:flex items-center gap-10">
          <button @click="scrollToSection('dashboard-demo')" class="relative text-[11px] font-extrabold uppercase tracking-widest text-[#40567a] hover:text-[#0f2240] transition-colors py-2 group">
            Live Dashboard
            <span class="absolute bottom-0 left-0 w-0 h-0.5 bg-sky-500 transition-all duration-300 group-hover:w-full"></span>
          </button>
          <button @click="scrollToSection('about')" class="relative text-[11px] font-extrabold uppercase tracking-widest text-[#40567a] hover:text-[#0f2240] transition-colors py-2 group">
            Overview
            <span class="absolute bottom-0 left-0 w-0 h-0.5 bg-sky-500 transition-all duration-300 group-hover:w-full"></span>
          </button>
          <button @click="scrollToSection('features')" class="relative text-[11px] font-extrabold uppercase tracking-widest text-[#40567a] hover:text-[#0f2240] transition-colors py-2 group">
            Core Modules
            <span class="absolute bottom-0 left-0 w-0 h-0.5 bg-sky-500 transition-all duration-300 group-hover:w-full"></span>
          </button>

        </nav>

        <!-- Dynamic Royale Gloss Button -->
        <button 
          @click="handleActionClick" 
          class="relative overflow-hidden bg-gradient-to-r from-sky-500 to-blue-600 hover:from-sky-400 hover:to-blue-500 text-white font-['Outfit'] font-extrabold text-[10px] uppercase tracking-widest px-6 py-3 rounded-md shadow-[0_4px_14px_rgba(14,165,233,0.3)] active:scale-[0.98] transition-all duration-300 border border-sky-400/30 group"
        >
          <!-- Shiny light overlay -->
          <div class="absolute -inset-full h-full w-1/2 z-5 block transform -skew-x-12 bg-gradient-to-r from-transparent to-white/25 opacity-40 group-hover:animate-shine"></div>
          <span class="relative z-10 flex items-center gap-2">
            {{ authStore.isAuthenticated ? 'Access Dashboard' : 'Portal Sign In' }}
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor" class="w-3.5 h-3.5 group-hover:translate-x-0.5 transition-transform">
              <path stroke-linecap="round" stroke-linejoin="round" d="M13.5 4.5L21 12m0 0l-7.5 7.5M21 12H3" />
            </svg>
          </span>
        </button>
      </div>
    </header>

    <!-- --- HERO SECTION (Riot Layout Grid) --- -->
    <section id="hero" class="relative max-w-7xl 2xl:max-w-[1440px] mx-auto px-6 pt-6 lg:pt-8 pb-12 sm:pb-16 md:pb-20 lg:pb-24 z-10">
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 lg:gap-12 xl:gap-16 items-center">
        
        <!-- Left Side: Copy and Actions (Riot scale display headers) -->
        <div class="lg:col-span-5 flex flex-col items-start text-left scroll-animate slide-up">
          <!-- Centered Hero Logo matching Title Scale -->
          <img :src="bimasaktiLogo" alt="Bimasakti Logo" class="h-12 sm:h-14 w-auto object-contain mb-6 opacity-95 select-none">


          <h1 class="text-3xl sm:text-4xl md:text-5xl lg:text-6xl xl:text-7xl font-['Outfit'] font-black text-[#0f2240] tracking-tight leading-[1.08] mb-6">
            <span class="bg-gradient-to-r from-sky-500 to-blue-600 bg-clip-text text-transparent">Business Intelligence</span> <br/>
            For Properties
          </h1>

          <p class="text-xs sm:text-sm md:text-base text-[#40567a]/90 font-medium leading-relaxed max-w-lg mb-8">
            Engineered by PT Realta Chakradarma, Bimasakti unifies operational property data, automated tax systems, and real-time tenant communications on a premium cloud dashboard.
          </p>

          <!-- Dual Royale Action Buttons -->
          <div class="flex flex-col sm:flex-row gap-4 w-full sm:w-auto">
            <button 
              @click="handleActionClick" 
              class="relative overflow-hidden bg-gradient-to-r from-[#0066cc] to-[#0ea5e9] hover:from-[#0ea5e9] hover:to-[#38bdf8] text-white font-['Outfit'] font-black text-[11px] uppercase tracking-widest px-8 py-4 rounded-lg shadow-[0_6px_20px_rgba(14,165,233,0.35)] active:scale-[0.98] transition-all duration-300 border border-sky-400/40 group flex items-center justify-center gap-2"
            >
              <div class="absolute -inset-full h-full w-1/2 z-5 block transform -skew-x-12 bg-gradient-to-r from-transparent to-white/20 opacity-30 group-hover:animate-shine"></div>
              <span>{{ authStore.isAuthenticated ? 'Launch Console' : 'Sign In to Portal' }}</span>
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor" class="w-3.5 h-3.5 group-hover:translate-x-0.5 transition-transform">
                <path stroke-linecap="round" stroke-linejoin="round" d="M13.5 4.5L21 12m0 0l-7.5 7.5M21 12H3" />
              </svg>
            </button>
            <button 
              @click="scrollToSection('dashboard-demo')" 
              class="bg-white hover:bg-sky-50 text-[#40567a] hover:text-[#0f2240] border border-sky-200/80 font-['Outfit'] font-black text-[11px] uppercase tracking-widest px-8 py-4 rounded-lg shadow-sm hover:shadow active:scale-[0.98] transition-all duration-300 flex items-center justify-center gap-2"
            >
              <span>Explore Live Dashboard</span>
              <span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
            </button>
          </div>

          <!-- Quick Statistics Bar in Royale Light Blue style -->
          <div class="grid grid-cols-3 gap-4 sm:gap-6 border-t border-sky-200/60 pt-4 mt-6 w-full max-w-md">
            <div>
              <span class="block text-2xl font-black text-[#0f2240] font-['Outfit']">99.98%</span>
              <span class="text-[9px] uppercase tracking-wider font-extrabold text-[#7487a3]">Azure Uptime</span>
            </div>
            <div>
              <span class="block text-2xl font-black text-[#0f2240] font-['Outfit']">&lt;100ms</span>
              <span class="text-[9px] uppercase tracking-wider font-extrabold text-[#7487a3]">SQL Response</span>
            </div>
            <div>
              <span class="block text-2xl font-black text-[#0f2240] font-['Outfit']">1.4k+</span>
              <span class="text-[9px] uppercase tracking-wider font-extrabold text-[#7487a3]">Active Syncs</span>
            </div>
          </div>
        </div>

        <!-- Right Side: The Professional High-Fidelity Mockup Dashboard (The Star piece) -->
        <div id="dashboard-demo" class="lg:col-span-7 scroll-animate fade-in">
          <!-- Outer window frame representing Windows XP Royale (Energy Blue) window -->
          <div class="w-full bg-[#f3f7fd] rounded-t-xl border-t border-x border-b border-[#1f62d4] shadow-[0_20px_50px_-12px_rgba(31,98,212,0.35)] overflow-hidden relative flex flex-col h-[480px] sm:h-[500px] md:h-[520px] lg:h-[480px] xl:h-[540px] 2xl:h-[600px]">
            
            <!-- Authentic Windows XP Royale Energy Blue Title Bar -->
            <div class="flex items-center justify-between px-3 py-1.5 border-t border-white/40 bg-gradient-to-b from-[#629ef7] via-[#357ae5] to-[#1b56ca] text-white relative z-20 shrink-0 shadow-[inset_0_1px_0_rgba(255,255,255,0.4)] select-none">
              <div class="flex items-center gap-2">
                <!-- Computer system icon -->
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor" class="w-4 h-4 text-sky-100 drop-shadow-[0_1px_1px_rgba(0,0,0,0.5)]">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M9 17.25v1.007a3 3 0 01-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0115 18.257V17.25m6-12V15a2.25 2.25 0 01-2.25 2.25H5.25A2.25 2.25 0 013 15V5.25m18 0A2.25 2.25 0 0018.75 3H5.25A2.25 2.25 0 003 5.25m18 0V12a2.25 2.25 0 01-2.25 2.25H5.25A2.25 2.25 0 013 12V5.25" />
                </svg>
                <!-- Title Text with shadow for 3D metallic feel -->
                <span class="text-[11px] font-bold font-sans tracking-wide text-white drop-shadow-[0_1px_2px_rgba(0,0,0,0.8)]">
                  Bimasakti Core Systems Console [Active Cluster: PROD-SEA-01]
                </span>
              </div>
              
              <!-- Windows XP Royale Window Control Buttons -->
              <div class="flex items-center gap-1.5">
                <!-- Minimize Button -->
                <button class="w-[21px] h-[21px] rounded-full flex items-center justify-center border border-[#1b56ca] bg-gradient-to-b from-[#e3efff] via-[#a4c9fa] to-[#5192f5] hover:brightness-110 active:brightness-90 shadow-sm transition-all relative group" title="Minimize">
                  <span class="w-2.5 h-[3px] bg-[#163a8a] rounded-sm mt-[6px]"></span>
                </button>
                <!-- Maximize Button -->
                <button class="w-[21px] h-[21px] rounded-full flex items-center justify-center border border-[#1b56ca] bg-gradient-to-b from-[#e3efff] via-[#a4c9fa] to-[#5192f5] hover:brightness-110 active:brightness-90 shadow-sm transition-all relative group" title="Maximize">
                  <span class="w-[9px] h-[7px] border-[2px] border-[#163a8a] rounded-sm"></span>
                </button>
                <!-- Close Button (Royale Orange/Red gradient) -->
                <button class="w-[21px] h-[21px] rounded-full flex items-center justify-center border border-[#9b1708] bg-gradient-to-b from-[#fbae97] via-[#f3664d] to-[#bf2613] hover:from-[#ffbfad] hover:to-[#df301b] active:brightness-95 shadow-[0_1px_2px_rgba(0,0,0,0.25)] transition-all group" title="Close">
                  <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="3" stroke="white" class="w-2.5 h-2.5 drop-shadow-[0_1px_1px_rgba(0,0,0,0.3)]">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
            </div>

            <!-- Classic File Menu Bar -->
            <div class="flex items-center gap-1.5 px-3 py-1 bg-[#ebf3fd] border-b border-[#a8cbf3] text-[11px] font-medium text-[#1c2e4a] select-none shrink-0 relative z-10">
              <button class="px-2 py-0.5 rounded hover:bg-[#3b82f6]/10 hover:outline hover:outline-1 hover:outline-[#3b82f6]/40 cursor-default">File</button>
              <button class="px-2 py-0.5 rounded hover:bg-[#3b82f6]/10 hover:outline hover:outline-1 hover:outline-[#3b82f6]/40 cursor-default">Edit</button>
              <button class="px-2 py-0.5 rounded hover:bg-[#3b82f6]/10 hover:outline hover:outline-1 hover:outline-[#3b82f6]/40 cursor-default">View</button>
              <button class="px-2 py-0.5 rounded hover:bg-[#3b82f6]/10 hover:outline hover:outline-1 hover:outline-[#3b82f6]/40 cursor-default">Tools</button>
              <button class="px-2 py-0.5 rounded hover:bg-[#3b82f6]/10 hover:outline hover:outline-1 hover:outline-[#3b82f6]/40 cursor-default">Help</button>
              
              <div class="ml-auto flex items-center gap-2 pr-1">
                <span class="w-1.5 h-1.5 rounded-full bg-[#10b981] animate-pulse"></span>
                <span class="text-[9px] font-bold text-sky-600 font-mono tracking-wider uppercase">SQLITE CONNECTED &bull; PORT {{ backendPort }}</span>
              </div>
            </div>

            <!-- Inner Split Pane: Left Explorer Pane vs. Right Workspace -->
            <div class="flex-1 flex min-h-0 relative">
              
              <!-- --- COLLAPSIBLE LEFT EXPLORER PANE (XP Task Pane) --- -->
              <div class="hidden md:flex w-40 lg:w-44 border-r border-[#95bfe9] bg-gradient-to-b from-[#d6e5f8] to-[#a2c6ec] p-2 flex flex-col justify-between shrink-0 overflow-y-auto custom-scroll relative z-10 select-none">
                <div class="space-y-3">
                  
                  <!-- Panel 1: System Tasks -->
                  <div class="bg-white/70 rounded-lg overflow-hidden border border-[#95bfe9] shadow-sm">
                    <div class="px-2.5 py-1.5 flex items-center justify-between bg-gradient-to-r from-[#215dc6] to-[#6da3f6] text-white text-[9px] font-bold uppercase tracking-wider">
                      <span>System Tasks</span>
                      <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="3" stroke="currentColor" class="w-2 h-2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
                      </svg>
                    </div>
                    <div class="p-1.5 space-y-1.5">
                      <!-- Start-style Sync Button -->
                      <button 
                        @click="runSimulatedSync" 
                        :disabled="syncing"
                        class="w-full relative overflow-hidden bg-gradient-to-b from-[#8fd878] via-[#5cae46] to-[#257317] hover:brightness-110 active:brightness-95 border border-[#1b5011] text-white text-[9px] font-black uppercase tracking-widest py-1.5 rounded-md shadow-md flex items-center justify-center gap-1 group transition-all active:scale-[0.98]"
                      >
                        <!-- Green start gloss glare -->
                        <div class="absolute inset-x-0 top-0 h-[40%] bg-white/25"></div>
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor" :class="['w-3 h-3 drop-shadow-[0_1px_1px_rgba(0,0,0,0.4)]', syncing ? 'animate-spin' : '']">
                          <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99" />
                        </svg>
                        <span class="drop-shadow-[0_1px_1px_rgba(0,0,0,0.5)]">{{ syncing ? 'SYNC...' : 'RUN SYNC' }}</span>
                      </button>
                      
                      <!-- Simulated links -->
                      <a href="#" @click.prevent="runSimulatedSync" class="flex items-center gap-1 text-[8.5px] font-bold text-[#0c3c8c] hover:underline">
                        <span class="w-1 h-1 rounded-full bg-[#0c3c8c]"></span>
                        <span>Verify ledger</span>
                      </a>
                      <a href="#" @click.prevent class="flex items-center gap-1 text-[8.5px] font-bold text-[#0c3c8c] hover:underline">
                        <span class="w-1 h-1 rounded-full bg-[#0c3c8c]"></span>
                        <span>e-Faktur form</span>
                      </a>
                    </div>
                  </div>
                  
                  <!-- Panel 2: Navigation Nodes -->
                  <div class="bg-white/70 rounded-lg overflow-hidden border border-[#95bfe9] shadow-sm">
                    <div class="px-2.5 py-1.5 flex items-center justify-between bg-gradient-to-r from-[#215dc6] to-[#6da3f6] text-white text-[9px] font-bold uppercase tracking-wider">
                      <span>Navigation Nodes</span>
                      <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="3" stroke="currentColor" class="w-2 h-2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
                      </svg>
                    </div>
                    <div class="p-1.5 flex flex-col gap-1">
                      <button 
                        @click="activeTab = 'accounting'" 
                        :class="['w-full text-left px-1.5 py-1 rounded text-[9px] font-bold flex items-center gap-1.5 border transition-all outline-none', activeTab === 'accounting' ? 'bg-[#3b82f6]/15 text-[#0c3c8c] border-[#3b82f6]/30' : 'text-[#475569] border-transparent hover:bg-[#3b82f6]/5']"
                      >
                        <span :class="['w-1.5 h-1.5 rounded-full border shrink-0', activeTab === 'accounting' ? 'bg-sky-500 border-[#0c3c8c] ring-1 ring-sky-300' : 'bg-white border-slate-300']"></span>
                        <span class="truncate">Accounting & VAT</span>
                      </button>
                      <button 
                        @click="activeTab = 'tenant'" 
                        :class="['w-full text-left px-1.5 py-1 rounded text-[9px] font-bold flex items-center gap-1.5 border transition-all outline-none', activeTab === 'tenant' ? 'bg-[#3b82f6]/15 text-[#0c3c8c] border-[#3b82f6]/30' : 'text-[#475569] border-transparent hover:bg-[#3b82f6]/5']"
                      >
                        <span :class="['w-1.5 h-1.5 rounded-full border shrink-0', activeTab === 'tenant' ? 'bg-sky-500 border-[#0c3c8c] ring-1 ring-sky-300' : 'bg-white border-slate-300']"></span>
                        <span class="truncate">Tenant API Stacks</span>
                      </button>
                    </div>
                  </div>
                  
                  <!-- Panel 3: Connection Details -->
                  <div class="bg-white/70 rounded-lg overflow-hidden border border-[#95bfe9] shadow-sm">
                    <div class="px-2.5 py-1.5 flex items-center justify-between bg-gradient-to-r from-[#215dc6] to-[#6da3f6] text-white text-[9px] font-bold uppercase tracking-wider">
                      <span>System Details</span>
                      <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="3" stroke="currentColor" class="w-2 h-2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
                      </svg>
                    </div>
                    <div class="p-1.5 space-y-1 font-sans text-[8px] font-bold text-[#475569] uppercase">
                      <div><span class="text-[#0c3c8c]">Client:</span> BMS_CORP</div>
                      <div><span class="text-[#0c3c8c]">SLA Sync:</span> 99.98%</div>
                      <div><span class="text-[#0c3c8c]">Azure SEA:</span> Online</div>
                      <div><span class="text-[#0c3c8c]">Node:</span> CLUSTER-01</div>
                    </div>
                  </div>
                  
                </div>
                
                <div class="flex items-center justify-center gap-1.5 border-t border-[#95bfe9]/50 pt-2 mt-2">
                  <div class="w-5 h-5 rounded bg-[#3b82f6]/10 border border-[#3b82f6]/20 flex items-center justify-center text-[9px] font-black text-[#0c3c8c]">B</div>
                  <span class="text-[7.5px] text-[#475569] font-black tracking-widest font-mono">BIMASAKTI BI</span>
                </div>
              </div>

              <!-- --- WORKSPACE WORK WINDOW --- -->
              <div class="flex-1 bg-[#f3f7fd] p-3 flex flex-col justify-between overflow-hidden relative">
                
                <!-- Sheen top watermark -->
                <div class="absolute top-0 right-0 w-32 h-32 bg-sky-200/10 rounded-full blur-2xl pointer-events-none"></div>
                
                <!-- Mobile Tab Switcher inside workspace (only visible on mobile/tablet) -->
                <div class="flex md:hidden gap-1.5 mb-3 shrink-0 select-none">
                  <button 
                    @click="activeTab = 'accounting'"
                    :class="['flex-1 py-1 px-2 text-center text-[10px] font-bold rounded border transition-all shadow-sm outline-none', activeTab === 'accounting' ? 'bg-[#3b82f6] text-white border-[#1f62d4]' : 'bg-white text-[#475569] border-[#95bfe9]/50']"
                  >
                    Accounting & VAT
                  </button>
                  <button 
                    @click="activeTab = 'tenant'"
                    :class="['flex-1 py-1 px-2 text-center text-[10px] font-bold rounded border transition-all shadow-sm outline-none', activeTab === 'tenant' ? 'bg-[#3b82f6] text-white border-[#1f62d4]' : 'bg-white text-[#475569] border-[#95bfe9]/50']"
                  >
                    Tenant API Stacks
                  </button>
                </div>
                
                <!-- Tab 1: Accounting Node -->
                <div v-if="activeTab === 'accounting'" class="flex-1 flex flex-col justify-between min-h-0 space-y-3">
                  
                  <!-- Metrics Row (XP Group Box style) -->
                  <div class="grid grid-cols-3 gap-2 shrink-0">
                    <!-- Metric 1: Total Invoiced -->
                    <div class="relative bg-white border border-[#95bfe9]/70 rounded-md p-2 shadow-[0_1px_3px_rgba(31,98,212,0.05)] hover:border-[#3b82f6]/40 transition-colors">
                      <div class="absolute -top-1.5 left-2 px-1 bg-[#f3f7fd] text-[7px] font-black uppercase text-[#0c3c8c] tracking-wider select-none">Total Invoiced</div>
                      <div class="mt-0.5">
                        <span class="text-sm font-black font-mono text-[#0f2240] tracking-tight">Rp {{ totalInvoiced }}M</span>
                        <span class="text-[7px] font-black text-[#10b981] ml-1 bg-emerald-100 px-1 py-0.2 rounded border border-emerald-200/50">+18.4%</span>
                      </div>
                      <span class="text-[7px] font-bold text-[#64748b] block mt-0.5 font-mono uppercase">BUFFERS RECONCILED</span>
                    </div>

                    <!-- Metric 2: VAT e-Faktur -->
                    <div class="relative bg-white border border-[#95bfe9]/70 rounded-md p-2 shadow-[0_1px_3px_rgba(31,98,212,0.05)] hover:border-[#3b82f6]/40 transition-colors">
                      <div class="absolute -top-1.5 left-2 px-1 bg-[#f3f7fd] text-[7px] font-black uppercase text-[#0c3c8c] tracking-wider select-none">VAT e-Faktur</div>
                      <div class="mt-0.5">
                        <span class="text-sm font-black font-mono text-[#0f2240] tracking-tight">Rp {{ vatInvoiced }}M</span>
                        <span class="text-[7px] font-bold text-sky-600 ml-1 font-mono">10% COMP</span>
                      </div>
                      <span class="text-[7px] font-bold text-[#64748b] block mt-0.5 font-mono uppercase">e-Faktur Sync</span>
                    </div>

                    <!-- Metric 3: Active Sync -->
                    <div class="relative bg-white border border-[#95bfe9]/70 rounded-md p-2 shadow-[0_1px_3px_rgba(31,98,212,0.05)] hover:border-[#3b82f6]/40 transition-colors">
                      <div class="absolute -top-1.5 left-2 px-1 bg-[#f3f7fd] text-[7px] font-black uppercase text-[#0c3c8c] tracking-wider select-none">Azure replication</div>
                      <div class="mt-0.5 flex items-center justify-between">
                        <span class="text-sm font-black font-mono text-[#0f2240] tracking-tight">99.98%</span>
                        <span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse border border-emerald-300"></span>
                      </div>
                      <span class="text-[7px] font-bold text-[#64748b] block mt-0.5 font-mono uppercase">SECURE SYNC SLA</span>
                    </div>
                  </div>
                  
                  <!-- Workspace Split Row (Ledger Grid + Mini Card) -->
                  <div class="grid grid-cols-12 gap-2.5 flex-1 min-h-0 overflow-hidden">
                    
                    <!-- Ledger Grid (col-span-7) -->
                    <div class="col-span-7 bg-white rounded-md border border-[#95bfe9]/70 flex flex-col overflow-hidden shadow-sm">
                      <div class="bg-gradient-to-r from-[#ebf3fd] to-[#cfdffd] px-2 py-1 border-b border-[#a8cbf3] flex items-center justify-between select-none">
                        <span class="text-[8px] font-black text-[#0c3c8c] uppercase tracking-wider">Billing Ledger Registry</span>
                        <span class="text-[6.5px] text-slate-500 font-bold uppercase font-mono">SQLite Stream</span>
                      </div>
                      <div class="p-1 flex-1 overflow-y-auto custom-scroll space-y-1 text-[9px]">
                        
                        <!-- Invoice Item 1 -->
                        <div class="flex items-center justify-between p-1.5 rounded bg-[#f3f7fd]/70 hover:bg-[#3b82f6]/5 border border-slate-100 hover:border-[#95bfe9]/30 transition-all">
                          <div>
                            <span class="font-black font-mono text-[#0c3c8c] block">INV-2026-081</span>
                            <span class="text-[7px] font-semibold text-slate-500">PT REALTA CHAKRADARMA</span>
                          </div>
                          <div class="flex items-center gap-2">
                            <span class="font-bold font-mono text-[#0f2240]">Rp 145.0M</span>
                            <span class="px-1.5 py-0.2 rounded text-[7px] bg-emerald-100 text-emerald-600 border border-emerald-200/50 font-black font-mono">PAID</span>
                          </div>
                        </div>

                        <!-- Invoice Item 2 -->
                        <div class="flex items-center justify-between p-1.5 rounded bg-[#f3f7fd]/70 hover:bg-[#3b82f6]/5 border border-slate-100 hover:border-[#95bfe9]/30 transition-all">
                          <div>
                            <span class="font-black font-mono text-[#0c3c8c] block">INV-2026-082</span>
                            <span class="text-[7px] font-semibold text-slate-500">BIMASAKTI RESIDENCES</span>
                          </div>
                          <div class="flex items-center gap-2">
                            <span class="font-bold font-mono text-[#0f2240]">Rp 28.5M</span>
                            <span class="px-1.5 py-0.2 rounded text-[7px] bg-emerald-100 text-emerald-600 border border-emerald-200/50 font-black font-mono">PAID</span>
                          </div>
                        </div>

                        <!-- Invoice Item 3 (Interactive Ledger) -->
                        <div class="flex items-center justify-between p-1.5 rounded bg-[#f3f7fd]/70 hover:bg-[#3b82f6]/5 border border-slate-100 hover:border-[#95bfe9]/30 transition-all">
                          <div>
                            <span class="font-black font-mono text-[#0c3c8c] block">INV-2026-083</span>
                            <span class="text-[7px] font-semibold text-slate-500">GRAND LEASING MALL</span>
                          </div>
                          <div class="flex items-center gap-2">
                            <span class="font-bold font-mono text-[#0f2240]">Rp 62.0M</span>
                            <span :class="['px-1.5 py-0.2 rounded text-[7px] border font-black font-mono transition-colors', invoiceStatus === 'PAID' ? 'bg-emerald-100 text-emerald-600 border-emerald-200' : 'bg-blue-50 text-[#0c3c8c] border-blue-200 animate-pulse']">
                              {{ invoiceStatus }}
                            </span>
                          </div>
                        </div>
                        
                      </div>
                    </div>
                    
                    <!-- Right Column: Flow Chart & Card (col-span-5) -->
                    <div class="col-span-5 flex flex-col justify-between gap-2 overflow-hidden">
                      
                      <!-- Flow Chart -->
                      <div class="bg-white rounded-md border border-[#95bfe9]/70 p-2 flex flex-col justify-between flex-1 shadow-sm overflow-hidden">
                        <div class="flex items-center justify-between border-b border-slate-100 pb-1 mb-1 select-none">
                          <span class="text-[8px] font-black text-[#0c3c8c] uppercase tracking-wider">Flow Ledger</span>
                          <span class="text-[6px] text-slate-500 font-bold font-mono">IN/OUT</span>
                        </div>
                        
                        <!-- SVG graph -->
                        <div class="h-10 w-full flex items-end justify-around select-none">
                          <div class="flex flex-col items-center gap-0.5">
                            <div class="flex gap-0.5 items-end">
                              <div class="w-1.5 h-5 bg-[#3b82f6] rounded-t-sm"></div>
                              <div class="w-1.5 h-2 bg-[#93c5fd] rounded-t-sm"></div>
                            </div>
                            <span class="text-[5px] font-mono font-bold text-slate-500">FEB</span>
                          </div>
                          <div class="flex flex-col items-center gap-0.5">
                            <div class="flex gap-0.5 items-end">
                              <div class="w-1.5 h-7 bg-[#3b82f6] rounded-t-sm"></div>
                              <div class="w-1.5 h-3.5 bg-[#93c5fd] rounded-t-sm"></div>
                            </div>
                            <span class="text-[5px] font-mono font-bold text-slate-500">MAR</span>
                          </div>
                          <div class="flex flex-col items-center gap-0.5">
                            <div class="flex gap-0.5 items-end">
                              <div class="w-1.5 h-4 bg-[#60a5fa] rounded-t-sm"></div>
                              <div class="w-1.5 h-1.5 bg-[#93c5fd] rounded-t-sm"></div>
                            </div>
                            <span class="text-[5px] font-mono font-bold text-slate-500">APR</span>
                          </div>
                          <div class="flex flex-col items-center gap-0.5">
                            <div class="flex gap-0.5 items-end">
                              <div class="w-1.5 h-9 bg-gradient-to-t from-[#60a5fa] to-[#10b981] rounded-t-sm"></div>
                              <div class="w-1.5 h-4.5 bg-[#93c5fd] rounded-t-sm"></div>
                            </div>
                            <span class="text-[5px] font-mono font-bold text-slate-500">MAY</span>
                          </div>
                        </div>
                      </div>
                      
                      <!-- Access Credential Card -->
                      <div class="bg-gradient-to-br from-[#1b56ca] to-[#3a7ad4] border border-[#a8cbf3]/30 rounded-md p-2 relative overflow-hidden shrink-0 flex flex-col justify-between h-[60px] shadow-sm select-none">
                        <div class="absolute -top-8 -left-8 w-16 h-16 bg-white/10 rounded-full blur-lg pointer-events-none"></div>
                        <div class="flex items-start justify-between">
                          <div>
                            <span class="text-[5px] font-mono font-black text-sky-200 uppercase tracking-widest leading-none block">SECURE CREDENTIAL KEY</span>
                            <span class="text-[8px] font-black text-white font-sans uppercase tracking-wider block mt-0.5 drop-shadow-[0_1px_1px_rgba(0,0,0,0.3)]">BIMASAKTI BI CARD</span>
                          </div>
                          <div class="w-4 h-3.5 bg-gradient-to-r from-amber-400 to-yellow-500 rounded border border-amber-300/40 relative flex flex-col justify-around p-0.5 shrink-0 shadow">
                            <div class="w-full h-[0.5px] bg-black/25"></div>
                            <div class="w-full h-[0.5px] bg-black/25"></div>
                          </div>
                        </div>
                        <div class="flex items-end justify-between mt-1">
                          <span class="text-[8px] font-black font-mono text-white tracking-widest leading-none drop-shadow-[0_1px_1px_rgba(0,0,0,0.3)]">•••• •••• •••• 0801</span>
                          <span class="text-[5px] font-mono font-black text-sky-200 leading-none">NODE-01</span>
                        </div>
                      </div>
                      
                    </div>
                  </div>
                </div>

                <!-- Tab 2: Tenant App API Stacks -->
                <div v-else class="flex-1 flex flex-col justify-between min-h-0 space-y-3">
                  
                  <!-- Metrics Row -->
                  <div class="grid grid-cols-3 gap-2 shrink-0">
                    <div class="relative bg-white border border-[#95bfe9]/70 rounded-md p-2 shadow-[0_1px_3px_rgba(31,98,212,0.05)] hover:border-[#3b82f6]/40 transition-colors">
                      <div class="absolute -top-1.5 left-2 px-1 bg-[#f3f7fd] text-[7px] font-black uppercase text-[#0c3c8c] tracking-wider select-none">API Request Latency</div>
                      <div class="mt-0.5">
                        <span class="text-sm font-black font-mono text-[#0f2240] tracking-tight">94ms</span>
                        <span class="text-[7px] font-black text-[#10b981] ml-1 bg-emerald-100 px-1 py-0.2 rounded border border-emerald-200/50">99% SLA</span>
                      </div>
                      <span class="text-[7px] font-bold text-[#64748b] block mt-0.5 font-mono uppercase">Avg Response Time</span>
                    </div>

                    <div class="relative bg-white border border-[#95bfe9]/70 rounded-md p-2 shadow-[0_1px_3px_rgba(31,98,212,0.05)] hover:border-[#3b82f6]/40 transition-colors">
                      <div class="absolute -top-1.5 left-2 px-1 bg-[#f3f7fd] text-[7px] font-black uppercase text-[#0c3c8c] tracking-wider select-none">API Requests Today</div>
                      <div class="mt-0.5">
                        <span class="text-sm font-black font-mono text-[#0f2240] tracking-tight">24,819</span>
                        <span class="text-[7px] font-bold text-[#0c3c8c] ml-1 font-mono">ACTIVE</span>
                      </div>
                      <span class="text-[7px] font-bold text-[#64748b] block mt-0.5 font-mono uppercase">200 OK: 99.96%</span>
                    </div>

                    <div class="relative bg-white border border-[#95bfe9]/70 rounded-md p-2 shadow-[0_1px_3px_rgba(31,98,212,0.05)] hover:border-[#3b82f6]/40 transition-colors">
                      <div class="absolute -top-1.5 left-2 px-1 bg-[#f3f7fd] text-[7px] font-black uppercase text-[#0c3c8c] tracking-wider select-none">App Gateway</div>
                      <div class="mt-0.5 flex items-center justify-between">
                        <span class="text-sm font-black font-mono text-[#0f2240] tracking-tight">ONLINE</span>
                        <span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse border border-emerald-300"></span>
                      </div>
                      <span class="text-[7px] font-bold text-[#64748b] block mt-0.5 font-mono uppercase">SSL PROXY BUFFERS</span>
                    </div>
                  </div>
                  
                  <div class="grid grid-cols-12 gap-2.5 flex-1 min-h-0 overflow-hidden">
                    <!-- Endpoint router list -->
                    <div class="col-span-7 bg-white rounded-md border border-[#95bfe9]/70 flex flex-col overflow-hidden shadow-sm">
                      <div class="bg-gradient-to-r from-[#ebf3fd] to-[#cfdffd] px-2 py-1 border-b border-[#a8cbf3] flex items-center justify-between select-none">
                        <span class="text-[8px] font-black text-[#0c3c8c] uppercase tracking-wider">Tenant API Router Live Stream</span>
                        <span class="text-[6.5px] text-slate-500 font-bold uppercase font-mono">DAEMON</span>
                      </div>
                      <div class="p-1.5 flex-1 overflow-y-auto custom-scroll space-y-1.5 select-text font-mono text-[8px] leading-normal">
                        <div class="flex justify-between items-center border-b border-slate-50 pb-0.5">
                          <span class="text-[#0c3c8c] font-bold">GET /api/v1/tenant/utility-readings</span>
                          <span class="text-emerald-600 font-bold bg-emerald-50 px-1 border border-emerald-200/50 rounded">200 OK</span>
                        </div>
                        <div class="flex justify-between items-center border-b border-slate-50 pb-0.5">
                          <span class="text-[#0c3c8c] font-bold">POST /api/v1/tenant/tickets/create</span>
                          <span class="text-emerald-600 font-bold bg-emerald-50 px-1 border border-emerald-200/50 rounded">201 CREATED</span>
                        </div>
                        <div class="flex justify-between items-center border-b border-slate-50 pb-0.5">
                          <span class="text-[#0c3c8c] font-bold">GET /api/v1/tenant/billing/invoice-pdf</span>
                          <span class="text-emerald-600 font-bold bg-emerald-50 px-1 border border-emerald-200/50 rounded">200 OK</span>
                        </div>
                        <div class="flex justify-between items-center border-b border-slate-50 pb-0.5">
                          <span class="text-[#0c3c8c] font-bold">POST /api/v1/tenant/payment/confirm</span>
                          <span class="text-emerald-600 font-bold bg-emerald-50 px-1 border border-emerald-200/50 rounded">200 OK</span>
                        </div>
                      </div>
                    </div>

                    <!-- Ticket Distribution -->
                    <div class="col-span-5 bg-white rounded-md border border-[#95bfe9]/70 p-2 flex flex-col justify-between shadow-sm overflow-hidden select-none">
                      <div class="border-b border-slate-100 pb-1 mb-1 flex items-center justify-between">
                        <span class="text-[8px] font-black text-[#0c3c8c] uppercase tracking-wider">Workorders</span>
                        <span class="text-[6px] text-slate-500 font-bold font-mono">Tenant Portal</span>
                      </div>
                      
                      <div class="space-y-1.5 flex-1 flex flex-col justify-around">
                        <div class="flex items-center justify-between text-[8px] font-bold uppercase text-[#475569]">
                          <span>Facility Res:</span>
                          <span class="font-mono text-[#0f2240]">42 requests</span>
                        </div>
                        <div class="w-full bg-slate-100 h-1.5 rounded overflow-hidden">
                          <div class="bg-sky-500 h-full rounded" style="width: 62%"></div>
                        </div>
                        <div class="flex items-center justify-between text-[8px] font-bold uppercase text-[#475569]">
                          <span>Maintenance:</span>
                          <span class="font-mono text-[#0f2240]">18 tickets</span>
                        </div>
                        <div class="w-full bg-slate-100 h-1.5 rounded overflow-hidden">
                          <div class="bg-[#10b981] h-full rounded" style="width: 28%"></div>
                        </div>
                        <div class="flex items-center justify-between text-[8px] font-bold uppercase text-[#475569]">
                          <span>Billing Discrep:</span>
                          <span class="font-mono text-[#0f2240]">5 alerts</span>
                        </div>
                        <div class="w-full bg-slate-100 h-1.5 rounded overflow-hidden">
                          <div class="bg-amber-500 h-full rounded" style="width: 10%"></div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>

                <!-- --- TELEMETRY TERMINAL CONSOLE --- -->
                <div class="bg-black/95 border border-[#81a5d6] rounded p-2 shrink-0 shadow-[0_1px_3px_rgba(0,0,0,0.15)] font-mono text-[9px] relative z-10">
                  <div class="flex items-center justify-between border-b border-slate-800 pb-1 mb-1.5 select-none">
                    <div class="flex items-center gap-1.5">
                      <span class="w-1.5 h-1.5 rounded-full bg-[#10b981] animate-pulse"></span>
                      <span class="text-[7.5px] uppercase tracking-wider text-slate-400 font-bold">Diag Cluster Synchronization Stream Log</span>
                    </div>
                    <span class="text-[7.5px] text-slate-500 font-mono font-bold">DAEMON_PORT: {{ backendPort }}</span>
                  </div>
                  
                  <!-- Scrollable logs box -->
                  <div ref="terminalRef" class="h-12 sm:h-14 md:h-16 lg:h-20 xl:h-24 2xl:h-28 overflow-y-auto space-y-0.5 leading-normal custom-scroll pr-1 select-text">
                    <div v-for="(log, i) in terminalLogs" :key="i" class="flex gap-1.5 items-start font-mono text-[8.5px]">
                      <span class="text-[#0c3c8c] font-black select-none">&gt;</span>
                      <span :class="[
                        log.includes('SUCCESS:') ? 'text-emerald-400 font-bold' : '',
                        log.includes('SYSTEM:') ? 'text-sky-300 font-bold' : '',
                        log.includes('HEALTH:') ? 'text-blue-400 font-bold' : '',
                        log.includes('SQLITE:') ? 'text-amber-400 font-bold' : '',
                        log.includes('SECURE:') ? 'text-indigo-400 font-bold' : '',
                        'text-slate-300'
                      ]">{{ log }}</span>
                    </div>
                    <!-- Live loading bar in console -->
                    <div v-if="syncing" class="flex items-center gap-2 text-sky-300 font-bold font-mono text-[8.5px]">
                      <span>&gt; REPLICATING_BUFFERS:</span>
                      <div class="w-20 bg-slate-800 border border-slate-700 h-1.5 rounded overflow-hidden relative">
                        <div class="bg-gradient-to-r from-sky-400 to-[#3b82f6] h-full rounded transition-all duration-300" :style="{ width: `${syncProgress}%` }"></div>
                      </div>
                      <span>{{ syncProgress }}%</span>
                    </div>
                  </div>
                </div>

              </div>
            </div>

          </div>
        </div>


      </div>
    </section>

    <!-- --- MODULE OVERVIEW (Asymmetric Riot Card Grid) --- -->
    <section id="about" class="py-12 sm:py-16 md:py-20 lg:py-24 bg-[#f8fbfe] border-y border-sky-100 relative z-10">
      <div class="max-w-7xl 2xl:max-w-[1440px] mx-auto px-6">
        
        <div class="text-center max-w-2xl mx-auto mb-12 sm:mb-16 lg:mb-20 scroll-animate fade-in">
          <span class="text-xs font-black text-sky-500 uppercase tracking-widest block mb-3 font-mono">Enterprise Operations</span>
          <h2 class="text-3xl sm:text-4xl font-['Outfit'] font-black text-[#0f2240] tracking-tight leading-none">
            Total Operational Control
          </h2>
          <p class="text-sm text-[#40567a] font-medium leading-relaxed mt-4">
            Bimasakti unifies complex property pipelines under a modern database core. Perfect visual workflows combined with secure data synchronizations.
          </p>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 lg:gap-8">
          
          <!-- Riot Card 1 -->
          <div class="bg-white rounded-xl border border-sky-100 hover:border-sky-300 p-8 shadow-[0_4px_25px_-5px_rgba(56,189,248,0.06)] hover:shadow-[0_8px_30px_rgba(56,189,248,0.12)] transition-all duration-300 flex flex-col justify-between group scroll-animate slide-up relative overflow-hidden">
            <div class="absolute top-0 right-0 w-24 h-24 bg-gradient-to-bl from-sky-100/40 to-transparent rounded-bl-full pointer-events-none transition-transform duration-500 group-hover:scale-110"></div>
            <div>
              <span class="text-xs font-mono font-black text-sky-400 uppercase tracking-widest block mb-4">Module 01</span>
              <h3 class="text-lg font-['Outfit'] font-black text-[#0f2240] mb-3 group-hover:text-sky-500 transition-colors">Commercial Malls</h3>
              <p class="text-xs text-[#40567a] leading-relaxed font-medium">Track commercial shop tenants, leasing metrics, occupancy records, and shopping campaign performance charts.</p>
            </div>
            <div class="border-t border-sky-50 pt-4 mt-8 flex items-center justify-between text-[10px] font-black uppercase tracking-widest text-[#7487a3] group-hover:text-[#0f2240] transition-colors">
              <span>Sync Status: Secured</span>
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor" class="w-3.5 h-3.5 transform group-hover:translate-x-1 transition-transform">
                <path stroke-linecap="round" stroke-linejoin="round" d="M13.5 4.5L21 12m0 0l-7.5 7.5M21 12H3" />
              </svg>
            </div>
          </div>

          <!-- Riot Card 2 -->
          <div class="bg-white rounded-xl border border-sky-100 hover:border-sky-300 p-8 shadow-[0_4px_25px_-5px_rgba(56,189,248,0.06)] hover:shadow-[0_8px_30px_rgba(56,189,248,0.12)] transition-all duration-300 flex flex-col justify-between group scroll-animate slide-up relative overflow-hidden" style="transition-delay: 0.1s;">
            <div class="absolute top-0 right-0 w-24 h-24 bg-gradient-to-bl from-sky-100/40 to-transparent rounded-bl-full pointer-events-none transition-transform duration-500 group-hover:scale-110"></div>
            <div>
              <span class="text-xs font-mono font-black text-sky-400 uppercase tracking-widest block mb-4">Module 02</span>
              <h3 class="text-lg font-['Outfit'] font-black text-[#0f2240] mb-3 group-hover:text-sky-500 transition-colors">Residential Portfolios</h3>
              <p class="text-xs text-[#40567a] leading-relaxed font-medium">Coordinate apartment tenant registers, facility schedule reservations, maintenance tickets, and utility log sheets.</p>
            </div>
            <div class="border-t border-sky-50 pt-4 mt-8 flex items-center justify-between text-[10px] font-black uppercase tracking-widest text-[#7487a3] group-hover:text-[#0f2240] transition-colors">
              <span>Sync Status: Secured</span>
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor" class="w-3.5 h-3.5 transform group-hover:translate-x-1 transition-transform">
                <path stroke-linecap="round" stroke-linejoin="round" d="M13.5 4.5L21 12m0 0l-7.5 7.5M21 12H3" />
              </svg>
            </div>
          </div>

          <!-- Riot Card 3 (Featured XP Star Card) -->
          <div class="bg-gradient-to-br from-[#0f2240] to-[#040811] text-white rounded-xl border border-sky-950 p-8 shadow-[0_4px_25px_-5px_rgba(11,21,40,0.15)] hover:shadow-[0_12px_40px_rgba(11,21,40,0.3)] hover:-translate-y-1 transition-all duration-300 flex flex-col justify-between group scroll-animate slide-up relative overflow-hidden" style="transition-delay: 0.15s;">
            <!-- Gloss Accent -->
            <div class="absolute -top-12 -left-12 w-24 h-24 bg-sky-500/20 rounded-full blur-2xl"></div>
            <div>
              <div class="flex items-center justify-between mb-4">
                <span class="text-xs font-mono font-black text-sky-400 uppercase tracking-widest">Enterprise Core</span>
                <span class="bg-sky-500/20 text-sky-400 font-mono text-[8px] font-black tracking-widest uppercase px-2 py-0.5 rounded border border-sky-500/30">Azure Cloud</span>
              </div>
              <h3 class="text-lg font-['Outfit'] font-black text-white mb-3 group-hover:text-sky-300 transition-colors">Executive Hub</h3>
              <p class="text-xs text-sky-200/75 leading-relaxed font-medium">Access highly encrypted data structures, check accounting sync logs, compile financial VAT forms, and toggle user locks.</p>
            </div>
            <button 
              @click="handleActionClick"
              class="border border-sky-500/40 hover:bg-sky-500 hover:border-sky-500 text-sky-300 hover:text-white font-['Outfit'] font-black text-[9px] uppercase tracking-widest py-3 rounded-lg mt-8 transition-all flex items-center justify-center gap-2 group-hover:shadow-[0_4px_12px_rgba(14,165,233,0.2)]"
            >
              <span>Authenticate Node</span>
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor" class="w-3 h-3">
                <path stroke-linecap="round" stroke-linejoin="round" d="M16.5 10.5V6.75a4.5 4.5 0 10-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 002.25-2.25v-6.75a2.25 2.25 0 00-2.25-2.25H6.75a2.25 2.25 0 00-2.25 2.25v6.75a2.25 2.25 0 002.25 2.25z" />
              </svg>
            </button>
          </div>

        </div>

      </div>
    </section>

    <!-- --- FEATURE SEGMENTS (Double Column Asymmetrical Columns) --- -->
    <section id="features" class="py-12 sm:py-16 md:py-20 lg:py-24 max-w-7xl 2xl:max-w-[1440px] mx-auto px-6 space-y-20 sm:space-y-24 lg:space-y-32">
      
      <!-- Segment 1: Financial & Invoicing Automated Core -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 lg:gap-12 xl:gap-16 items-center">
        
        <div class="lg:col-span-5 scroll-animate slide-up">
          <span class="text-xs font-mono font-black text-sky-500 uppercase tracking-widest block mb-3">Module 01 / Financial Integration</span>
          <h2 class="text-3xl sm:text-4xl font-['Outfit'] font-black text-[#0f2240] tracking-tight leading-none mb-6">
            Billing Accounting & Automatic Tax Modules
          </h2>
          <p class="text-sm text-[#40567a] leading-relaxed font-semibold mb-8">
            Sync operational data directly to financial ledgers. Generate tax output bills instantly through integrated structures configured for local e-Faktur formats. High speed, minimal manual auditing.
          </p>

          <div class="space-y-4">
            <div class="flex items-center gap-3">
              <div class="w-6 h-6 rounded-md bg-sky-100 flex items-center justify-center shrink-0">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-3.5 h-3.5 text-sky-500">
                  <path fill-rule="evenodd" d="M16.704 4.153a.75.75 0 01.143 1.052l-8 10.5a.75.75 0 01-1.127.075l-4.5-4.5a.75.75 0 011.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 011.05-.143z" clip-rule="evenodd" />
                </svg>
              </div>
              <span class="text-xs font-extrabold text-[#40567a]">VAT automated processing system</span>
            </div>
            <div class="flex items-center gap-3">
              <div class="w-6 h-6 rounded-md bg-sky-100 flex items-center justify-center shrink-0">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-3.5 h-3.5 text-sky-500">
                  <path fill-rule="evenodd" d="M16.704 4.153a.75.75 0 01.143 1.052l-8 10.5a.75.75 0 01-1.127.075l-4.5-4.5a.75.75 0 011.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 011.05-.143z" clip-rule="evenodd" />
                </svg>
              </div>
              <span class="text-xs font-extrabold text-[#40567a]">Automatic journal mapping for ledgers</span>
            </div>
            <div class="flex items-center gap-3">
              <div class="w-6 h-6 rounded-md bg-sky-100 flex items-center justify-center shrink-0">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-3.5 h-3.5 text-sky-500">
                  <path fill-rule="evenodd" d="M16.704 4.153a.75.75 0 01.143 1.052l-8 10.5a.75.75 0 01-1.127.075l-4.5-4.5a.75.75 0 011.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 011.05-.143z" clip-rule="evenodd" />
                </svg>
              </div>
              <span class="text-xs font-extrabold text-[#40567a]">Secure SQLite storage with daily backups</span>
            </div>
          </div>
        </div>

        <div class="lg:col-span-7 scroll-animate slide-up bg-white p-3 border border-sky-100 rounded-xl shadow-[0_4px_30px_rgba(56,189,248,0.06)]" style="transition-delay: 0.15s;">
          <div class="border border-sky-50 rounded-lg overflow-hidden relative">
            <img :src="propertyOperations" alt="Realta Bimasakti Property Operations" class="w-full h-full object-cover select-none">
          </div>
        </div>

      </div>

      <!-- Segment 2: Tenant Mobile Desk Portal -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 lg:gap-12 xl:gap-16 items-center">
        
        <div class="lg:col-span-7 scroll-animate slide-up bg-white p-3 border border-sky-100 rounded-xl shadow-[0_4px_30px_rgba(56,189,248,0.06)]">
          <div class="border border-sky-50 rounded-lg overflow-hidden relative">
            <img :src="tenantMobileApp" alt="Bimasakti Tenant Mobile Portal App" class="w-full h-full object-cover select-none">
          </div>
        </div>

        <div class="lg:col-span-5 scroll-animate slide-up" style="transition-delay: 0.15s;">
          <span class="text-xs font-mono font-black text-sky-500 uppercase tracking-widest block mb-3">Module 02 / Mobile Collaboration</span>
          <h2 class="text-3xl sm:text-4xl font-['Outfit'] font-black text-[#0f2240] tracking-tight leading-none mb-6">
            24/7 Service Desk & Mobile Mobile Apps
          </h2>
          <p class="text-sm text-[#40567a] leading-relaxed font-semibold mb-8">
            Allow tenants to view active billing ledgers, verify invoices, make gateway payments, and file property maintenance tickets directly from their mobile phones.
          </p>

          <div class="space-y-4">
            <div class="flex items-center gap-3">
              <div class="w-6 h-6 rounded-md bg-sky-100 flex items-center justify-center shrink-0">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-3.5 h-3.5 text-sky-500">
                  <path fill-rule="evenodd" d="M16.704 4.153a.75.75 0 01.143 1.052l-8 10.5a.75.75 0 01-1.127.075l-4.5-4.5a.75.75 0 011.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 011.05-.143z" clip-rule="evenodd" />
                </svg>
              </div>
              <span class="text-xs font-extrabold text-[#40567a]">Live maintenance ticket dispatch system</span>
            </div>
            <div class="flex items-center gap-3">
              <div class="w-6 h-6 rounded-md bg-sky-100 flex items-center justify-center shrink-0">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-3.5 h-3.5 text-sky-500">
                  <path fill-rule="evenodd" d="M16.704 4.153a.75.75 0 01.143 1.052l-8 10.5a.75.75 0 01-1.127.075l-4.5-4.5a.75.75 0 011.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 011.05-.143z" clip-rule="evenodd" />
                </svg>
              </div>
              <span class="text-xs font-extrabold text-[#40567a]">Seamless mobile transaction gateways</span>
            </div>
            <div class="flex items-center gap-3">
              <div class="w-6 h-6 rounded-md bg-sky-100 flex items-center justify-center shrink-0">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-3.5 h-3.5 text-sky-500">
                  <path fill-rule="evenodd" d="M16.704 4.153a.75.75 0 01.143 1.052l-8 10.5a.75.75 0 01-1.127.075l-4.5-4.5a.75.75 0 011.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 011.05-.143z" clip-rule="evenodd" />
                </svg>
              </div>
              <span class="text-xs font-extrabold text-[#40567a]">Immediate push updates on utility readings</span>
            </div>
          </div>
        </div>

      </div>

    </section>



    <!-- --- CALL TO ACTION --- -->
    <section class="max-w-4xl mx-auto px-6 py-12 sm:py-16 md:py-20 lg:py-24 text-center scroll-animate slide-up relative z-10 flex flex-col items-center">
      <div class="w-12 h-1.5 bg-gradient-to-r from-sky-400 to-blue-500 rounded-full mb-8"></div>
      <h2 class="text-3xl sm:text-4xl font-['Outfit'] font-black text-[#0f2240] tracking-tight leading-none mb-6">
        Unlock Your Operational Power
      </h2>
      <p class="text-sm text-[#40567a] font-medium max-w-md leading-relaxed mb-8">
        Gain access to the secure portal, synchronize key property databases, check VAT accounts, and access custom dashboard analytics.
      </p>
      <button 
        @click="handleActionClick" 
        class="relative overflow-hidden bg-gradient-to-r from-sky-500 to-blue-600 hover:from-sky-400 hover:to-blue-500 text-white font-['Outfit'] font-black text-[11px] uppercase tracking-widest px-10 py-4 rounded-lg shadow-[0_6px_20px_rgba(14,165,233,0.3)] active:scale-[0.98] transition-all duration-300 border border-sky-400/40 group flex items-center gap-2"
      >
        <div class="absolute -inset-full h-full w-1/2 z-5 block transform -skew-x-12 bg-gradient-to-r from-transparent to-white/20 opacity-30 group-hover:animate-shine"></div>
        <span>{{ authStore.isAuthenticated ? 'Access BI Dashboard' : 'Authenticate & Sign In' }}</span>
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor" class="w-3.5 h-3.5 group-hover:translate-x-0.5 transition-transform">
          <path stroke-linecap="round" stroke-linejoin="round" d="M13.5 4.5L21 12m0 0l-7.5 7.5M21 12H3" />
        </svg>
      </button>
    </section>

    <!-- --- FOOTER --- -->
    <footer class="w-full bg-[#040811] border-t border-sky-950 text-[#7487a3] py-12 sm:py-16 relative z-10">
      <div class="max-w-7xl 2xl:max-w-[1440px] mx-auto px-6 flex flex-col md:flex-row justify-between items-center gap-8">
        <div class="flex items-center gap-3">
          <div class="w-8 h-8 rounded-lg bg-sky-950 border border-sky-850 flex items-center justify-center">
            <span class="text-sky-400 font-['Outfit'] font-black text-sm">B</span>
          </div>
          <div>
            <span class="text-white text-xs font-black tracking-widest uppercase block">PT Realta Chakradarma</span>
            <span class="text-[9px] font-bold uppercase tracking-wider text-[#7487a3]/70 font-mono">Bimasakti BI Enterprise Services &copy; 2026</span>
          </div>
        </div>
        <p class="text-[9px] font-bold uppercase tracking-widest text-[#7487a3]/60">All rights reserved. Secure Cloud Encryption enabled.</p>
      </div>
    </footer>

  </div>
</template>

<style scoped>
/* Scroll Triggered Animations CSS */
.scroll-animate {
  opacity: 0;
  transition: all 0.8s cubic-bezier(0.16, 1, 0.3, 1);
  will-change: transform, opacity;
}

.scroll-animate.slide-up {
  transform: translateY(30px);
}

.scroll-animate.fade-in {
  transform: scale(0.98);
}

.scroll-animate.in-view {
  opacity: 1;
  transform: translateY(0) scale(1);
}

/* Custom scrollbar formatting for terminals and pages */
.custom-scroll::-webkit-scrollbar {
  width: 4px;
  height: 4px;
}
.custom-scroll::-webkit-scrollbar-track {
  background: rgba(56, 189, 248, 0.02);
}
.custom-scroll::-webkit-scrollbar-thumb {
  background: rgba(56, 189, 248, 0.25);
  border-radius: 4px;
}
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

