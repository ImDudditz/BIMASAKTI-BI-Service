<script setup>
import { ref, onMounted, watch } from 'vue'
import api from '@/services/api'

const props = defineProps({
  companyId: {
    type: String,
    required: true,
  },
  selectedYear: {
    type: String,
    required: false,
  },
  selectedPeriod: {
    type: String,
    required: false,
  },
})

// Local component state for data fetching
const isLoading = ref(true)
const error = ref(null)

// Initialize data structure with default 0s
const kpiData = ref({
  totalTickets: 0,
  complaintTickets: 0,
  maintenanceTickets: 0,
  requestTickets: 0,
  closedTickets: 0,
})

/**
 * Basic formatter utility to add thousands separators.
 * Used internally for the widget template.
 */
const formatNumber = (num) => {
  if (num === null || num === undefined) return '0'
  return new Intl.NumberFormat('en-US').format(num)
}

const fetchTicketsKPI = async () => {
  isLoading.value = true
  error.value = null

  try {
    // Ensure the period parameter is strictly a two-digit string to match SQLite's %m format
    const formattedPeriod = String(props.selectedPeriod).padStart(2, '0')

    // API connection using the provided companyId prop and reactive filters
    const res = await api.get(`/engine/lmrx0800/kpi/${props.companyId}`, {
      params: {
        year: props.selectedYear,
        period: formattedPeriod,
      },
    })
    kpiData.value = res.data
  } catch (err) {
    console.error('Failed to fetch ticket KPIs:', err)
    error.value =
      err.response?.data?.detail ||
      'Failed to load Ticket KPI data. Please check backend connection.'
  } finally {
    isLoading.value = false
  }
}

// Automatically fetch data once the widget is mounted in the dashboard
onMounted(() => {
  fetchTicketsKPI()
})

// Reactively fetch data when selectedYear, selectedPeriod, or companyId changes
watch(
  () => [props.selectedYear, props.selectedPeriod, props.companyId],
  () => {
    fetchTicketsKPI()
  },
)
</script>

<template>
  <!-- Loading state indicator -->
  <div v-if="isLoading" class="flex justify-center items-center py-8 w-full">
    <div
      class="w-8 h-8 border-4 border-slate-200 border-t-amber-500 rounded-full animate-spin"
    ></div>
  </div>

  <!-- Fallback error view -->
  <div
    v-else-if="error"
    class="p-4 bg-rose-50 text-rose-500 rounded-xl text-center text-sm font-medium w-full border border-rose-100"
  >
    {{ error }}
  </div>

  <!-- Main Grid Layout (Strictly 5 columns on large screens) -->
  <div v-else class="grid grid-cols-2 lg:grid-cols-5 gap-3 sm:gap-4 w-full animate-slide-up">
    <!-- KPI Card: Total Tickets -->
    <div
      class="relative bg-white/90 backdrop-blur-md rounded-xl shadow-sm border border-white/50 p-3.5 sm:p-4"
    >
      <div class="relative z-10">
        <p class="text-[10px] sm:text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">
          Total Tickets
        </p>
        <h3 class="text-lg sm:text-xl md:text-2xl font-black text-slate-800 leading-none mt-1">
          {{ formatNumber(kpiData.totalTickets) }}
        </h3>
      </div>
    </div>

    <!-- KPI Card: Complaints -->
    <div
      class="relative bg-white/90 backdrop-blur-md rounded-xl shadow-sm border border-white/50 p-3.5 sm:p-4"
    >
      <div class="relative z-10">
        <p class="text-[10px] sm:text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">
          Complaints
        </p>
        <h3 class="text-lg sm:text-xl md:text-2xl font-black text-slate-800 leading-none mt-1">
          {{ formatNumber(kpiData.complaintTickets) }}
        </h3>
      </div>
    </div>

    <!-- KPI Card: Maintenance -->
    <div
      class="relative bg-white/90 backdrop-blur-md rounded-xl shadow-sm border border-white/50 p-3.5 sm:p-4"
    >
      <div class="relative z-10">
        <p class="text-[10px] sm:text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">
          Maintenance
        </p>
        <h3 class="text-lg sm:text-xl md:text-2xl font-black text-slate-800 leading-none mt-1">
          {{ formatNumber(kpiData.maintenanceTickets) }}
        </h3>
      </div>
    </div>

    <!-- KPI Card: Requests -->
    <div
      class="relative bg-white/90 backdrop-blur-md rounded-xl shadow-sm border border-white/50 p-3.5 sm:p-4"
    >
      <div class="relative z-10">
        <p class="text-[10px] sm:text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">
          Requests
        </p>
        <h3 class="text-lg sm:text-xl md:text-2xl font-black text-slate-800 leading-none mt-1">
          {{ formatNumber(kpiData.requestTickets) }}
        </h3>
      </div>
    </div>

    <!-- KPI Card: Closed -->
    <div
      class="relative bg-white/90 backdrop-blur-md rounded-xl shadow-sm border border-white/50 p-3.5 sm:p-4"
    >
      <div class="relative z-10">
        <p class="text-[10px] sm:text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">
          Closed
        </p>
        <h3 class="text-lg sm:text-xl md:text-2xl font-black text-slate-800 leading-none mt-1">
          {{ formatNumber(kpiData.closedTickets) }}
        </h3>
      </div>
    </div>
  </div>
</template>
