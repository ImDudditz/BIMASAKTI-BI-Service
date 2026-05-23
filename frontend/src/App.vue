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
  router.push('/login')
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
  <div class="h-screen flex flex-col font-sans text-slate-800 overflow-hidden relative bg-slate-50">
    
    <!-- ONLY show navigation if the user is logged in -->
    <nav v-if="authStore.isAuthenticated" class="shrink-0 z-40 w-full bg-sky-600 shadow-md relative">
      <div class="w-full mx-auto px-4 sm:px-6 lg:px-8 2xl:px-12">
        <div class="flex items-center justify-between h-14">
          
          <!-- LEFT SIDE: Logo & Menus -->
          <!-- Removed 'w-full' so it stops pushing the right section off-screen -->
          <div class="flex items-center gap-6 h-full">
            <div class="flex items-center gap-3 pr-6 border-r border-sky-500 h-full">
              <div v-if="activeLogoUrl" class="bg-white rounded p-1 flex items-center justify-center shadow-sm">
                <img :src="activeLogoUrl" @error="handleLogoError" alt="Company Logo" class="h-6 w-auto object-contain">
              </div>
              <h1 class="text-[15px] font-bold text-white tracking-wide uppercase">{{ companyName }}</h1>
            </div>

            <div class="hidden md:flex items-center h-full space-x-2">
              <div v-if="hasFinancialDashboard || hasOperationDashboard || hasMaintenanceDashboard" class="group relative h-full flex items-center">
                <button class="flex items-center gap-1 text-[13px] font-medium text-sky-50 px-3 py-2 rounded-md hover:bg-sky-700 hover:text-white transition-colors">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 mr-1" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 14v3m4-3v3m4-3v3M3 21h18M3 10h18M3 7l9-4 9 4M4 10h16v11H4V10z" /></svg>
                  Dashboard
                  <svg class="w-4 h-4 transition-transform group-hover:rotate-180" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clip-rule="evenodd"></path></svg>
                </button>
                <div class="absolute top-14 left-0 pt-1 hidden group-hover:block w-56 z-50">
                  <div class="bg-white shadow-lg border border-slate-200 rounded-b-md overflow-hidden py-1">
                    <RouterLink v-if="hasFinancialDashboard" to="/dashboard" class="block px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-sky-50 hover:text-sky-700 transition-colors">Financial Dashboard</RouterLink>
                    <RouterLink v-if="hasOperationDashboard" to="/dashboard/operation" class="block px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-sky-50 hover:text-sky-700 transition-colors">Operation Dashboard</RouterLink>
                    <RouterLink v-if="hasMaintenanceDashboard" to="/dashboard/maintenance" class="block px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-sky-50 hover:text-sky-700 transition-colors">Service & Maintenance</RouterLink>
                  </div>
                </div>
              </div>

              <div v-if="hasBalanceSheet || hasIncomeStatement" class="group relative h-full flex items-center">
                <button class="flex items-center gap-1 text-[13px] font-medium text-sky-50 px-3 py-2 rounded-md hover:bg-sky-700 hover:text-white transition-colors">
                  Reports
                  <svg class="w-4 h-4 transition-transform group-hover:rotate-180" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clip-rule="evenodd"></path></svg>
                </button>
                <div class="absolute top-14 left-0 pt-1 hidden group-hover:block w-56 z-50">
                  <div class="bg-white shadow-lg border border-slate-200 rounded-b-md overflow-hidden py-1">
                    <RouterLink v-if="hasBalanceSheet" to="/balance-sheet" class="block px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-sky-50 hover:text-sky-700 transition-colors">Balance Sheet</RouterLink>
                    <RouterLink v-if="hasIncomeStatement" to="/income-statement" class="block px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-sky-50 hover:text-sky-700 transition-colors">Income Statement</RouterLink>
                    <div class="border-t border-slate-100 my-1"></div>
                    <a href="#" class="block px-4 py-2.5 text-sm font-medium text-slate-500 hover:bg-slate-50 hover:text-sky-700 transition-colors">General Ledger</a>
                    <a href="#" class="block px-4 py-2.5 text-sm font-medium text-slate-500 hover:bg-slate-50 hover:text-sky-700 transition-colors">Trial Balance</a>
                  </div>
                </div>
              </div>

              <div class="group relative h-full flex items-center">
                <button class="flex items-center gap-1 text-[13px] font-medium text-sky-50 px-3 py-2 rounded-md hover:bg-sky-700 hover:text-white transition-colors">
                  Settings
                  <svg class="w-4 h-4 transition-transform group-hover:rotate-180" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clip-rule="evenodd"></path></svg>
                </button>
                
                <div class="absolute top-14 left-0 pt-1 hidden group-hover:block w-64 z-50">
                  <div class="bg-white shadow-lg border border-slate-200 rounded-b-md overflow-hidden py-1">
                    <RouterLink to="/settings" class="block px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-sky-50 hover:text-sky-700 transition-colors">
                      Account Mapping Wizard
                    </RouterLink>
                    <div class="border-t border-slate-100 my-1"></div>
                    <div class="px-4 py-2 text-[10px] font-black text-slate-400 uppercase tracking-widest">
                      Print Financial Report
                    </div>
                    <button @click="triggerExcelExport" class="w-full text-left flex items-center gap-3 px-4 py-2.5 text-sm font-bold text-emerald-700 hover:bg-emerald-50 transition-colors">
                      <span class="text-lg">📊</span> Export to Excel
                    </button>
                    <button @click="triggerPdfExport" class="w-full text-left flex items-center gap-3 px-4 py-2.5 text-sm font-bold text-rose-700 hover:bg-rose-50 transition-colors">
                      <span class="text-lg">📕</span> Export to PDF
                    </button>
                  </div>
                </div>
              </div>
              <div class="group relative h-full flex items-center" v-if="authStore.isAdmin">
                <RouterLink to="/admin" class="flex items-center gap-1 text-[13px] font-medium text-sky-50 px-3 py-2 rounded-md hover:bg-sky-700 hover:text-white transition-colors">
                  Admin Panel
                </RouterLink>
              </div>

            </div>
          </div>
          
          <!-- RIGHT SIDE: User Profile & Logout -->
          <div class="flex items-center gap-4 shrink-0 pl-4 border-l border-sky-500 h-8">
            <div class="flex items-center gap-2.5" v-if="authStore.user">
              <!-- Avatar Circle -->
              <div class="w-7 h-7 rounded-full bg-sky-800 text-sky-100 flex items-center justify-center text-xs font-black uppercase shadow-inner border border-sky-700/50">
                {{ authStore.user.username.charAt(0) }}
              </div>
              <span class="text-[13px] font-bold text-sky-50 tracking-wide">{{ authStore.user.username }}</span>
            </div>

            <!-- Logout Button -->
            <button @click="handleLogout" class="flex items-center gap-1.5 text-[12px] font-bold text-sky-200 hover:text-white hover:bg-sky-700 px-3 py-1.5 rounded-lg transition-colors group">
              <svg class="w-4 h-4 text-sky-400 group-hover:text-rose-400 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"></path></svg>
              Logout
            </button>
          </div>

        </div>
      </div>
    </nav>

    <RouterView class="grow overflow-hidden flex flex-col w-full relative z-10" />

    <Teleport to="body">
      <div v-if="isOpen" class="fixed inset-0 z-200 flex items-center justify-center p-4">
        <div class="absolute inset-0 bg-slate-900/40 backdrop-blur-sm" @click="modalStore.closeModal"></div>
        <div class="relative bg-white rounded-2xl shadow-2xl max-w-md w-full overflow-hidden animate-slide-up border border-slate-100">
          <div class="p-6">
            <div class="flex items-start gap-4">
              <div class="text-3xl bg-slate-50 p-3 rounded-xl border border-slate-100 shadow-sm">{{ icon }}</div>
              <div class="pt-1 w-full">
                <h3 class="text-lg font-black text-sky-950 tracking-tight">{{ title }}</h3>
                <p class="text-sm text-slate-500 font-medium mt-1 leading-relaxed">{{ message }}</p>
                <input v-if="type === 'prompt'" v-model="modalStore.inputValue" type="text" class="mt-4 w-full border border-slate-300 rounded-lg p-2.5 text-sm font-bold text-slate-800 focus:ring-2 ring-sky-500 outline-none transition-shadow" @keyup.enter="modalStore.confirmModal" autofocus>
              </div>
            </div>
          </div>
          <div class="bg-slate-50/80 px-6 py-4 flex items-center justify-end gap-3 border-t border-slate-100">
            <button v-if="type !== 'alert'" @click="modalStore.closeModal" class="px-4 py-2 text-sm font-bold text-slate-600 hover:text-slate-900 hover:bg-slate-200 rounded-lg transition-colors">{{ cancelText }}</button>
            <button @click="modalStore.confirmModal" class="px-5 py-2 text-sm font-bold text-white rounded-lg shadow-sm transition-colors active:scale-95" :class="confirmColor">{{ confirmText }}</button>
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
</style>