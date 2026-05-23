<template>
  <ReportLayout 
    title="Service & Maintenance Dashboard" 
    :subtitle="`As of ${monthNames[selectedPeriod]} ${selectedYear}`">
    
    <template #controls>
      <div class="flex items-center gap-4" v-if="isAuthorized">
        <div class="flex items-center bg-white border border-sky-200 rounded shadow-sm overflow-hidden shrink-0" v-if="availableYears.length > 0">
          <select v-model="selectedPeriod" class="bg-transparent text-[13px] font-medium text-slate-700 px-2.5 py-1 focus:outline-none cursor-pointer hover:bg-sky-50 transition-colors">
            <option v-for="p in availablePeriods" :key="p" :value="p">{{ monthNames[p] || p }}</option>
          </select>
          <div class="w-px h-4 bg-sky-200"></div>
          <select v-model="selectedYear" class="bg-transparent text-[13px] font-medium text-slate-700 px-2.5 py-1 focus:outline-none cursor-pointer hover:bg-sky-50 transition-colors">
            <option v-for="y in availableYears" :key="y" :value="y">{{ y }}</option>
          </select>
        </div>

        <div class="flex items-center gap-3 bg-white border border-sky-200 rounded px-3 py-1 shadow-sm shrink-0" v-if="availableYears.length > 1">
          <span class="text-[13px] font-bold text-sky-900">Compare Years:</span>
          <label v-for="y in availableYears.filter(year => year !== selectedYear)" :key="y" class="flex items-center gap-1.5 cursor-pointer text-[13px] font-medium text-slate-700">
            <input type="checkbox" :value="y" v-model="comparisonYears" class="w-3.5 h-3.5 text-sky-600 rounded border-sky-300 focus:ring-sky-500 cursor-pointer">
            {{ y }}
          </label>
        </div>
      </div>
    </template>

    <div class="overflow-y-auto custom-scrollbar flex-1 min-h-0 w-full relative z-10">
      <div class="max-w-screen-2xl mx-auto px-4 sm:px-6 lg:px-8 py-6 flex flex-col min-h-full w-full">
        
        <!-- Access Restricted View -->
        <div v-if="!isAuthorized" class="flex-1 flex flex-col items-center justify-center py-20 px-4 text-center">
          <div class="max-w-md w-full bg-white/85 backdrop-blur-md rounded-2xl shadow-xl border border-slate-200/60 p-8 space-y-6 animate-slide-up">
            <div class="w-16 h-16 bg-rose-50 border border-rose-100 rounded-full flex items-center justify-center mx-auto text-rose-500 shadow-inner">
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-8 h-8">
                <path stroke-linecap="round" stroke-linejoin="round" d="M16.5 10.5V6.75a4.5 4.5 0 10-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 002.25-2.25v-6.75a2.25 2.25 0 00-2.25-2.25H6.75a2.25 2.25 0 00-2.25 2.25v6.75a2.25 2.25 0 002.25 2.25z" />
              </svg>
            </div>
            <div class="space-y-2">
              <h3 class="text-xl font-black text-slate-800 tracking-tight">Access Restricted</h3>
              <p class="text-sm text-slate-500 font-medium leading-relaxed">
                You do not have permission to view the Service & Maintenance Dashboard. Please contact your system administrator to request access.
              </p>
            </div>
            <div class="pt-2">
              <RouterLink to="/dashboard" class="inline-flex items-center gap-2 px-5 py-2.5 bg-amber-500 hover:bg-amber-600 text-white text-sm font-bold rounded-xl shadow-md hover:shadow-amber-500/20 active:scale-95 transition-all duration-200">
                Back to Dashboard
              </RouterLink>
            </div>
          </div>
        </div>

        <!-- Loading Spinner -->
        <div v-else-if="isLoading" class="flex flex-col items-center justify-center flex-1 min-h-0 w-full py-24">
          <div class="w-10 h-10 border-4 border-amber-100 border-t-amber-500 rounded-full animate-spin"></div>
          <p class="text-amber-600 font-medium animate-pulse text-sm mt-4">Syncing maintenance logs...</p>
        </div>

        <!-- Failure state fallback -->
        <div v-else-if="error" class="flex flex-col items-center justify-center flex-1 min-h-0 w-full py-20 text-center">
          <div class="p-3.5 bg-rose-50 rounded-full text-rose-500 mb-4 shadow-sm">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          </div>
          <h4 class="text-slate-800 font-bold tracking-tight">Sync Fail</h4>
          <p class="text-slate-500 text-xs mt-1 max-w-md leading-relaxed">{{ error }}</p>
          <button @click="loadDashboardData" class="mt-4 px-4 py-2 bg-amber-500 hover:bg-amber-600 text-white text-xs font-bold rounded-lg shadow-sm transition-all duration-200">
            Retry Connection
          </button>
        </div>

        <!-- Dashboard Layout Grid -->
        <div v-else class="grid grid-cols-1 lg:grid-cols-2 gap-6 pb-8 w-full">
          
          <!-- Tickets KPI Strip -->
          <div class="lg:col-span-2 mb-2">
            <TicketsKPI :companyId="companyId" :selectedYear="selectedYear" :selectedPeriod="selectedPeriod" />
          </div>

          <!-- Primary Status Card + Uptime doughnut -->
          <div class="lg:col-span-2">
            <MaintenanceStatusWidget />
          </div>

          <!-- Secondary Categories Chart -->
          <div class="lg:col-span-2">
            <TicketsByCategoryWidget :categoriesData="ticketsByCategory" />
          </div>

        </div>
      </div>
    </div>
  </ReportLayout>
</template>

<script setup>
import { ref, onMounted, watch, computed } from 'vue'
import { storeToRefs } from 'pinia'
import api from '@/services/api'
import { useReportFilterStore } from '@/stores/reportFilters'
import { useAuthStore } from '@/stores/auth'
import ReportLayout from '@/components/ReportLayout.vue'
import MaintenanceStatusWidget from '@/components/widgets/ServiceAndMaintenance/MaintenanceStatusWidget.vue'
import TicketsByCategoryWidget from '@/components/widgets/ServiceAndMaintenance/TicketsByCategoryWidget.vue'
import TicketsKPI from '@/components/widgets/ServiceAndMaintenance/TicketsKPI.vue'

const filterStore = useReportFilterStore()
const { selectedYear, selectedPeriod, availableYears, availablePeriods } = storeToRefs(filterStore)

const authStore = useAuthStore()

// Localized Async States
const isLoading = ref(true)
const error = ref(null)

// Current tenant context
const companyId = ref(authStore.user?.company_id || 'ASHMD')
const comparisonYears = ref([])

const monthNames = {
  "01": "January", "02": "February", "03": "March", "04": "April",
  "05": "May", "06": "June", "07": "July", "08": "August",
  "09": "September", "10": "October", "11": "November", "12": "December"
}

// Maintenance bound structures
const statusPayload = ref(null)
const ticketsByCategory = ref([])

const isAuthorized = computed(() => {
  return authStore.isAdmin || authStore.userWidgets.includes('tickets_kpi') || authStore.userWidgets.includes('maintenance_status') || authStore.userWidgets.includes('tickets_by_category')
})

const loadDashboardData = async () => {
  if (!isAuthorized.value) {
    isLoading.value = false
    return
  }

  const company_id = authStore.user?.company_id || 'ASHMD'
  if (!company_id || !selectedYear.value || !selectedPeriod.value) {
    isLoading.value = false
    return
  }

  isLoading.value = true
  error.value = null

  const y = selectedYear.value
  const p = selectedPeriod.value.toString().padStart(2, '0')

  try {
    // Queries maintenance status details from the backend endpoint
    const res = await api.get('/v1/dashboard/maintenance/status', {
      params: {
        year: y,
        period: p,
        company_id: company_id
      }
    })
    statusPayload.value = res.data
    
    // Store localized category distributions
    ticketsByCategory.value = res.data.ticketsByCategory || []
  } catch (err) {
    console.error('Maintenance Dashboard connection failure:', err)
    error.value = err.response?.data?.message || 'Maintenance service connection timeout. Please verify network routing or backend session authorization.'
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  filterStore.fetchFilters().then(() => loadDashboardData())
})

watch([selectedYear, selectedPeriod, comparisonYears], () => {
  loadDashboardData()
})
</script>
