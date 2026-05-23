<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { RouterView, RouterLink, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import api from '@/services/api'
import { useReportFilterStore } from './stores/reportFilters'
import { useGlobalModalStore } from '@/stores/globalModal'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const filterStore = useReportFilterStore()
const { companyName, companyId, selectedYear, selectedPeriod, activePreset } = storeToRefs(filterStore)

onMounted(async () => {
  if (authStore.isAuthenticated) {
    await authStore.fetchPermissions()
  }
})

// --- ROLE CONTROL PERMISSIONS ---
const hasFinancialDashboard = computed(() => {
  return authStore.isAdmin || ['kpi_cards', 'capital_growth', 'operating_cash_flow', 'revenue_budget', 'expense_budget'].some(k => authStore.userWidgets.includes(k))
})

const hasOperationDashboard = computed(() => {
  return authStore.isAdmin || ['operation_metrics', 'lease_expirations'].some(k => authStore.userWidgets.includes(k))
})

const hasMaintenanceDashboard = computed(() => {
  return authStore.isAdmin || ['tickets_kpi', 'maintenance_status', 'tickets_by_category'].some(k => authStore.userWidgets.includes(k))
})

const hasBalanceSheet = computed(() => {
  return authStore.isAdmin || authStore.userReports.includes('balance_sheet')
})

const hasIncomeStatement = computed(() => {
  return authStore.isAdmin || authStore.userReports.includes('income_statement')
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

const activePageSubtitle = computed(() => {
  const path = router.currentRoute.value.path
  if (path === '/dashboard') return 'Financials & Operations Overview'
  if (path === '/dashboard/operation') return 'Occupancy, Revenue & Operations'
  if (path === '/dashboard/maintenance') return 'Tickets, Requests & Maintenance KPI'
  if (path === '/balance-sheet') return 'Financial Position & Assets Ledger'
  if (path === '/income-statement') return 'Revenue, Expenses & Net Profit'
  if (path === '/settings') return 'Configure Accounts & Financial Groups'
  return 'Financials & Operations Overview'
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
const { isOpen, type, title, message, confirmText, cancelText, confirmColor, icon } = storeToRefs(modalStore)

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

// --- GLOBAL EXCEL EXPORT LOGIC ---
const monthNames = {
  "01": "January", "02": "February", "03": "March", "04": "April",
  "05": "May", "06": "June", "07": "July", "08": "August",
  "09": "September", "10": "October", "11": "November", "12": "December"
}

const fetchLedger = async (y, p) => {
  try {
    const res = await api.get('/reports/ledger', { 
      params: { year: y, period: p, preset: activePreset.value, company_id: companyId.value } 
    })
    if (res.data.status === 'success') {
      return { structure: res.data.data, netIncome: res.data.net_income || 0 }
    }
  } catch (err) { console.error("Export API Error:", err) }
  return { structure: {}, netIncome: 0 }
}

// Runs an array of async factory functions in sequential batches of `size`
const chunkFetch = async (factories, size = 4) => {
  const results = []
  for (let i = 0; i < factories.length; i += size) {
    const batch = factories.slice(i, i + size).map(fn => fn())
    const batchResults = await Promise.all(batch)
    results.push(...batchResults)
  }
  return results
}

const buildExportPayload = async () => {
  const y = parseInt(selectedYear.value)
  const p = parseInt(selectedPeriod.value)
  
  let lastMoP = p - 1
  let lastMoY = y
  if (lastMoP === 0) { lastMoP = 12; lastMoY = y - 1 }
  
  const pStr = p.toString().padStart(2, '0')
  const lastMoPStr = lastMoP.toString().padStart(2, '0')

  const basePromises = [
    fetchLedger(y.toString(), pStr),
    fetchLedger(lastMoY.toString(), lastMoPStr),
    fetchLedger((y - 1).toString(), pStr)
  ]
  
  const qFactories = [
    () => fetchLedger(y.toString(), '03'), () => fetchLedger(y.toString(), '06'),
    () => fetchLedger(y.toString(), '09'), () => fetchLedger(y.toString(), '12')
  ]
  
  const yFactories = Array.from({length: 12}, (_, i) => () => fetchLedger(y.toString(), (i+1).toString().padStart(2, '0')))

  const [curr, prevMo, prevYr] = await Promise.all(basePromises)
  const quarterlyData = await chunkFetch(qFactories, 4)
  const yearlyData = await chunkFetch(yFactories, 4)

  return {
    currData: curr,
    prevMoData: prevMo,
    prevYrData: prevYr,
    quarterlyData: quarterlyData,
    yearlyData: yearlyData,
    company: companyName.value,
    year: selectedYear.value,
    periodName: monthNames[pStr],
    lastMonthName: monthNames[lastMoPStr],
    lastMonthYear: lastMoY.toString(),
    lastYearName: monthNames[pStr],
    lastYearYear: (y - 1).toString(),
    pStr
  }
}

const triggerExcelExport = async () => {
  if (!selectedYear.value || !selectedPeriod.value) {
    modalStore.showAlert('Missing Filters', 'Please ensure a Year and Period are selected.', true)
    return
  }

  modalStore.showAlert('Exporting...', 'Compiling your global financial report. This may take a few moments as we gather quarterly and yearly data.', false)

  try {
    const payload = await buildExportPayload()
    const pStr = payload.pStr
    delete payload.pStr

    const response = await api.post('/export/excel', payload, {
      responseType: 'blob' 
    })
    
    const filename = `${companyName.value}_${monthNames[pStr]}_${selectedYear.value}.xlsx`.replace(/ /g, '_')
    
    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', filename)
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url) 
    
    modalStore.closeModal()

  } catch (error) {
    console.error("Global Export Failed:", error)
    modalStore.showAlert('Export Failed', 'The server could not generate the Excel file. Please try again.', true)
  }
}

const triggerPdfExport = async () => {
  if (!selectedYear.value || !selectedPeriod.value) {
    modalStore.showAlert('Missing Filters', 'Please ensure a Year and Period are selected.', true)
    return
  }

  modalStore.showAlert('Exporting...', 'Compiling your global financial report. This may take a few moments as we gather quarterly and yearly data.', false)

  try {
    const payload = await buildExportPayload()
    const pStr = payload.pStr
    delete payload.pStr

    const response = await api.post('/export/pdf', payload, {
      responseType: 'blob' 
    })
    
    const filename = `${companyName.value}_${monthNames[pStr]}_${selectedYear.value}.pdf`.replace(/ /g, '_')
    
    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', filename)
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url) 
    
    modalStore.closeModal()

  } catch (error) {
    console.error("Global Export Failed:", error)
    modalStore.showAlert('Export Failed', 'The server could not generate the PDF file. Please try again.', true)
  }
}
</script>

<template>
  <!-- Main layout container with global styles -->
  <div class="h-screen w-screen flex flex-col font-sans text-slate-800 overflow-hidden relative bg-[#f0f4f9] select-none">
    
    <!-- BACKGROUND PASTEL BLUR CIRLCES - Only shown on authenticated dashboard shell -->
    <template v-if="isAuthPage">
      <div class="absolute top-[-10%] left-[-10%] w-[50%] h-[50%] rounded-full bg-indigo-300/30 blur-[130px] pointer-events-none z-0"></div>
      <div class="absolute bottom-[-10%] right-[-10%] w-[50%] h-[50%] rounded-full bg-indigo-200/30 blur-[130px] pointer-events-none z-0"></div>
      <div class="absolute top-[30%] right-[20%] w-[35%] h-[35%] rounded-full bg-[#3c56d6]/12 blur-[110px] pointer-events-none z-0"></div>
    </template>

    <!-- AUTHENTICATED PREMIUM GLASSMORPHIC DASHBOARD SHELL -->
    <div v-if="isAuthPage" class="flex-1 flex gap-6 p-6 overflow-hidden h-full w-full relative z-10">
      
      <!-- LEFT-HAND GLASS SIDEBAR -->
      <aside class="w-[260px] bg-white/45 backdrop-blur-xl border border-white/60 shadow-2xl rounded-[32px] flex flex-col p-6 shrink-0 z-40 transition-all duration-500 hover:shadow-indigo-100/40">
        
        <!-- Brand section with Company Logo & Name -->
        <div class="flex flex-col items-center justify-center text-center pb-6 border-b border-slate-200/40 shrink-0 gap-3">
          <div v-if="activeLogoUrl" class="flex items-center justify-center w-full max-h-16 px-4">
            <img :src="activeLogoUrl" @error="handleLogoError" alt="Company Logo" class="max-w-full max-h-14 object-contain">
          </div>
          <div class="flex flex-col items-center min-w-0 max-w-full">
            <h1 class="text-xs font-black text-slate-900 tracking-wide uppercase leading-snug break-words whitespace-normal px-2 max-w-full" :title="companyName">
              {{ companyName }}
            </h1>
          </div>
        </div>

        <!-- Sidebar Navigation List -->
        <nav class="flex-grow overflow-y-auto mt-6 pr-1 space-y-2 custom-scrollbar">
          
          <!-- 1. OVERVIEW -->
          <RouterLink 
            to="/overview" 
            class="flex items-center gap-3 px-4 py-3 rounded-2xl text-[13px] font-bold border transition-all duration-300 group"
            :class="router.currentRoute.value.path === '/overview' 
              ? 'bg-[#3c56d6]/10 text-[#3c56d6] border-[#3c56d6]/20 shadow-sm' 
              : 'text-slate-500 border-transparent hover:text-slate-900 hover:bg-white/40'"
          >
            <span class="text-base group-hover:scale-110 transition-transform">🏠</span>
            <span>Overview</span>
          </RouterLink>

          <!-- 2. FINANCIALS (Accordion) -->
          <div class="space-y-1">
            <button 
              @click="toggleMenu('financials')"
              class="w-full flex items-center justify-between px-4 py-3 rounded-2xl text-[13px] font-bold text-slate-500 hover:text-slate-900 hover:bg-white/40 border border-transparent transition-all duration-300 group"
            >
              <div class="flex items-center gap-3">
                <span class="text-base group-hover:scale-110 transition-transform">💳</span>
                <span>Financials</span>
              </div>
              <!-- Rotating Chevron -->
              <span class="text-[10px] transition-transform duration-300" :class="{ 'rotate-180': expandedMenus.financials }">▼</span>
            </button>
            
            <!-- Financials Submenus -->
            <div 
              v-show="expandedMenus.financials" 
              class="pl-6 pr-2 space-y-1 overflow-hidden transition-all duration-300"
            >
              <!-- Dashboard -->
              <RouterLink 
                to="/dashboard" 
                class="flex items-center gap-2.5 px-4 py-2 rounded-xl text-xs font-bold border transition-all duration-200"
                :class="router.currentRoute.value.path === '/dashboard'
                  ? 'bg-[#3c56d6]/8 text-[#3c56d6] border-[#3c56d6]/15 shadow-sm'
                  : 'text-slate-500 border-transparent hover:text-slate-900 hover:bg-white/30'"
              >
                <span>•</span>
                <span>Dashboard</span>
              </RouterLink>
              
              <!-- Report -->
              <RouterLink 
                to="/balance-sheet" 
                class="flex items-center gap-2.5 px-4 py-2 rounded-xl text-xs font-bold border transition-all duration-200"
                :class="['/balance-sheet', '/income-statement'].includes(router.currentRoute.value.path)
                  ? 'bg-[#3c56d6]/8 text-[#3c56d6] border-[#3c56d6]/15 shadow-sm'
                  : 'text-slate-500 border-transparent hover:text-slate-900 hover:bg-white/30'"
              >
                <span>•</span>
                <span>Report</span>
              </RouterLink>
              
              <!-- Print -->
              <RouterLink 
                to="/print" 
                class="flex items-center gap-2.5 px-4 py-2 rounded-xl text-xs font-bold border transition-all duration-200"
                :class="router.currentRoute.value.path === '/print'
                  ? 'bg-[#3c56d6]/8 text-[#3c56d6] border-[#3c56d6]/15 shadow-sm'
                  : 'text-slate-500 border-transparent hover:text-slate-900 hover:bg-white/30'"
              >
                <span>•</span>
                <span>Print</span>
              </RouterLink>
            </div>
          </div>

          <!-- 3. TENANCY (Accordion) -->
          <div class="space-y-1">
            <button 
              @click="toggleMenu('tenancy')"
              class="w-full flex items-center justify-between px-4 py-3 rounded-2xl text-[13px] font-bold text-slate-500 hover:text-slate-900 hover:bg-white/40 border border-transparent transition-all duration-300 group"
            >
              <div class="flex items-center gap-3">
                <span class="text-base group-hover:scale-110 transition-transform">🏢</span>
                <span>Tenancy</span>
              </div>
              <span class="text-[10px] transition-transform duration-300" :class="{ 'rotate-180': expandedMenus.tenancy }">▼</span>
            </button>
            
            <!-- Tenancy Submenus -->
            <div 
              v-show="expandedMenus.tenancy" 
              class="pl-6 pr-2 space-y-1 overflow-hidden transition-all duration-300"
            >
              <!-- Dashboard -->
              <RouterLink 
                to="/dashboard/operation" 
                class="flex items-center gap-2.5 px-4 py-2 rounded-xl text-xs font-bold border transition-all duration-200"
                :class="router.currentRoute.value.path === '/dashboard/operation'
                  ? 'bg-[#3c56d6]/8 text-[#3c56d6] border-[#3c56d6]/15 shadow-sm'
                  : 'text-slate-500 border-transparent hover:text-slate-900 hover:bg-white/30'"
              >
                <span>•</span>
                <span>Dashboard</span>
              </RouterLink>
              
              <!-- Report -->
              <RouterLink 
                to="/tenancy-report" 
                class="flex items-center gap-2.5 px-4 py-2 rounded-xl text-xs font-bold border transition-all duration-200"
                :class="router.currentRoute.value.path === '/tenancy-report'
                  ? 'bg-[#3c56d6]/8 text-[#3c56d6] border-[#3c56d6]/15 shadow-sm'
                  : 'text-slate-500 border-transparent hover:text-slate-900 hover:bg-white/30'"
              >
                <span>•</span>
                <span>Report</span>
              </RouterLink>
            </div>
          </div>

          <!-- 4. SERVICES (Accordion) -->
          <div class="space-y-1">
            <button 
              @click="toggleMenu('services')"
              class="w-full flex items-center justify-between px-4 py-3 rounded-2xl text-[13px] font-bold text-slate-500 hover:text-slate-900 hover:bg-white/40 border border-transparent transition-all duration-300 group"
            >
              <div class="flex items-center gap-3">
                <span class="text-base group-hover:scale-110 transition-transform">🔧</span>
                <span>Services</span>
              </div>
              <span class="text-[10px] transition-transform duration-300" :class="{ 'rotate-180': expandedMenus.services }">▼</span>
            </button>
            
            <!-- Services Submenus -->
            <div 
              v-show="expandedMenus.services" 
              class="pl-6 pr-2 space-y-1 overflow-hidden transition-all duration-300"
            >
              <!-- Dashboard -->
              <RouterLink 
                to="/dashboard/maintenance" 
                class="flex items-center gap-2.5 px-4 py-2 rounded-xl text-xs font-bold border transition-all duration-200"
                :class="router.currentRoute.value.path === '/dashboard/maintenance'
                  ? 'bg-[#3c56d6]/8 text-[#3c56d6] border-[#3c56d6]/15 shadow-sm'
                  : 'text-slate-500 border-transparent hover:text-slate-900 hover:bg-white/30'"
              >
                <span>•</span>
                <span>Dashboard</span>
              </RouterLink>
              
              <!-- Report -->
              <RouterLink 
                to="/services-report" 
                class="flex items-center gap-2.5 px-4 py-2 rounded-xl text-xs font-bold border transition-all duration-200"
                :class="router.currentRoute.value.path === '/services-report'
                  ? 'bg-[#3c56d6]/8 text-[#3c56d6] border-[#3c56d6]/15 shadow-sm'
                  : 'text-slate-500 border-transparent hover:text-slate-900 hover:bg-white/30'"
              >
                <span>•</span>
                <span>Report</span>
              </RouterLink>
            </div>
          </div>

          <!-- Divider -->
          <div class="border-t border-slate-200/40 my-2"></div>

          <!-- SETTINGS (Utility Link) -->
          <RouterLink 
            to="/settings" 
            class="flex items-center gap-3 px-4 py-2.5 rounded-2xl text-[11px] font-bold border transition-all duration-200 group"
            :class="router.currentRoute.value.path === '/settings' 
              ? 'bg-[#3c56d6]/6 text-[#3c56d6] border-[#3c56d6]/12 shadow-sm' 
              : 'text-slate-400 border-transparent hover:text-slate-600 hover:bg-white/30'"
          >
            <span class="text-xs group-hover:scale-110 transition-transform">⚙️</span>
            <span>Settings</span>
          </RouterLink>


        </nav>

      </aside>

      <!-- RIGHT-HAND MAIN CONTAINER AREA -->
      <div class="flex-1 flex flex-col overflow-hidden h-full min-w-0">
        
        <!-- SUB-PAGE OUTER CONTAINER (Frosted RouterView Wrapper) -->
        <main class="flex-1 bg-white/45 backdrop-blur-xl border border-white/60 shadow-2xl rounded-[32px] overflow-hidden relative flex flex-col min-h-0">
          <RouterView class="grow overflow-hidden flex flex-col w-full relative z-10" />
        </main>

      </div>

    </div>

    <!-- NON-AUTHENTICATED / DIRECT RENDER LAYOUT (Landing & Login Pages) -->
    <div v-else class="flex-1 w-full h-full flex flex-col overflow-hidden relative z-10">
      <RouterView class="grow overflow-hidden flex flex-col w-full relative z-10" />
    </div>

    <!-- GLOBAL CONFIRMATION / PROMPT MODAL MANAGER -->
    <Teleport to="body">
      <div v-if="isOpen" class="fixed inset-0 z-[999] flex items-center justify-center p-4">
        <!-- Backdrop -->
        <div class="absolute inset-0 bg-slate-900/40 backdrop-blur-sm" @click="modalStore.closeModal"></div>
        <!-- Modal Card -->
        <div class="relative bg-white/95 backdrop-blur-xl rounded-[24px] shadow-2xl max-w-md w-full overflow-hidden animate-slide-up border border-white/80">
          <div class="p-6">
            <div class="flex items-start gap-4">
              <div class="text-3xl bg-slate-50 p-3 rounded-xl border border-slate-100 shadow-sm shrink-0">{{ icon }}</div>
              <div class="pt-1 w-full">
                <h3 class="text-lg font-black text-sky-950 tracking-tight leading-tight">{{ title }}</h3>
                <p class="text-sm text-slate-500 font-bold mt-1 leading-relaxed">{{ message }}</p>
                <input v-if="type === 'prompt'" v-model="modalStore.inputValue" type="text" class="mt-4 w-full border border-slate-200 bg-white/50 rounded-xl p-2.5 text-sm font-bold text-slate-800 focus:ring-2 ring-cyan-500 focus:border-cyan-500 focus:bg-white outline-none transition-all" @keyup.enter="modalStore.confirmModal" autofocus>
              </div>
            </div>
          </div>
          <div class="bg-slate-50/80 px-6 py-4 flex items-center justify-end gap-3 border-t border-slate-100">
            <button v-if="type !== 'alert'" @click="modalStore.closeModal" class="px-5 py-2.5 text-xs font-black text-slate-600 hover:text-slate-900 hover:bg-slate-200/50 rounded-xl transition-all active:scale-95">{{ cancelText }}</button>
            <button @click="modalStore.confirmModal" class="px-6 py-2.5 text-xs font-black text-white rounded-xl shadow-md shadow-cyan-900/10 transition-all active:scale-95" :class="confirmColor">{{ confirmText }}</button>
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

/* Custom premium slim scrollbar */
.custom-scrollbar::-webkit-scrollbar {
  width: 6px;
  height: 6px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: transparent;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: rgba(0, 0, 0, 0.08);
  border-radius: 99px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: rgba(0, 0, 0, 0.16);
}
</style>