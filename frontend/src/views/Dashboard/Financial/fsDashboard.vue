<script setup>
import { ref, onMounted, onBeforeUnmount, watch, defineAsyncComponent } from 'vue'
import { storeToRefs } from 'pinia'
import api from '@/services/api'
import { useReportFilterStore } from '@/stores/reportFilters'
import { useAuthStore } from '@/stores/auth'
import ReportLayout from '@/components/ReportLayout.vue'

const filterStore = useReportFilterStore()
const { selectedYear, selectedPeriod, availableYears, availablePeriods, activePreset } = storeToRefs(filterStore)

const authStore = useAuthStore()

const isLoading = ref(true)

const activeWidgets = ref([])

const monthNames = {
  "01": "January", "02": "February", "03": "March", "04": "April",
  "05": "May", "06": "June", "07": "July", "08": "August",
  "09": "September", "10": "October", "11": "November", "12": "December"
}

const formatMoney = (val) => {
  const num = Math.round(parseFloat(val) || 0);
  return new Intl.NumberFormat('id-ID', { minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(Math.abs(num));
}

const formatShortMoney = (num) => {
  if (num === null || num === undefined) return '0';
  const absNum = Math.abs(num);
  let val, suffix;
  if (absNum >= 1e9) { val = absNum / 1e9; suffix = 'B'; }
  else if (absNum >= 1e6) { val = absNum / 1e6; suffix = 'M'; }
  else if (absNum >= 1e3) { val = absNum / 1e3; suffix = 'K'; }
  else { return formatMoney(num); }
  
  const formatted = new Intl.NumberFormat('id-ID', { 
      minimumFractionDigits: suffix === 'K' ? 1 : 2, 
      maximumFractionDigits: suffix === 'K' ? 1 : 2 
  }).format(val);
  return num < 0 ? `(${formatted}${suffix})` : formatted + suffix;
}

const comparisonYears = ref([])

// Widget Registry
const widgetRegistry = {
  'kpi_cards': defineAsyncComponent(() => import('@/components/widgets/Financial/KpiCardsWidget.vue')),
  'capital_growth': defineAsyncComponent(() => import('@/components/widgets/Financial/CapitalGrowthWidget.vue')),
  'operating_cash_flow': defineAsyncComponent(() => import('@/components/widgets/Financial/OperatingCashFlowWidget.vue')),
  'revenue_budget': defineAsyncComponent(() => import('@/components/widgets/Financial/RevenueBudgetWidget.vue')),
  'expense_budget': defineAsyncComponent(() => import('@/components/widgets/Financial/ExpenseBudgetWidget.vue')),
  'operation_metrics': defineAsyncComponent(() => import('@/components/widgets/Operation/OperationMetricsWidget.vue')),
  'lease_expirations': defineAsyncComponent(() => import('@/components/widgets/Operation/LeaseExpirationsWidget.vue')),
  'tickets_kpi': defineAsyncComponent(() => import('@/components/widgets/ServiceAndMaintenance/TicketsKPI.vue')),
  'maintenance_status': defineAsyncComponent(() => import('@/components/widgets/ServiceAndMaintenance/MaintenanceStatusWidget.vue')),
  'tickets_by_category': defineAsyncComponent(() => import('@/components/widgets/ServiceAndMaintenance/TicketsByCategoryWidget.vue'))
}

const baseEchartsOptions = {
  textStyle: { fontFamily: "'Inter', sans-serif" },
  grid: { left: '3%', right: '4%', bottom: '5%', top: '15%', containLabel: true }
}

// Reactive Dashboard Data
const rawLedgerData = ref(null)
const leaseExpirations = ref([])
const ticketsByCategory = ref([])

const getWidgetProps = (widget) => {
  const baseProps = { formatMoney, formatShortMoney, baseEchartsOptions, config: widget.config };
  switch (widget.widget_key) {
    case 'kpi_cards':
      return { ...baseProps, rawLedgerData: rawLedgerData.value };
    case 'capital_growth':
      return { ...baseProps, rawLedgerData: rawLedgerData.value, selectedYear: selectedYear.value };
    case 'operating_cash_flow':
      return { ...baseProps, selectedYear: selectedYear.value };
    case 'revenue_budget':
      return { ...baseProps, rawLedgerData: rawLedgerData.value, selectedYear: selectedYear.value };
    case 'expense_budget':
      return { ...baseProps, rawLedgerData: rawLedgerData.value, selectedYear: selectedYear.value };
    case 'lease_expirations':
      return { ...baseProps, expirationsData: leaseExpirations.value };
    case 'tickets_by_category':
      return { ...baseProps, categoriesData: ticketsByCategory.value };
    case 'tickets_kpi':
      return { ...baseProps, companyId: authStore.user?.company_id || 'ASHMD', selectedYear: selectedYear.value, selectedPeriod: selectedPeriod.value };
    default:
      return baseProps;
  }
}

const fetchUserWidgets = async () => {
  try {
    const company_id = authStore.user?.company_id;
    const username = authStore.user?.username;
    if (!company_id || !username) return;
    const res = await api.get('/dashboard/my-widgets', { params: { company_id, username } });
    activeWidgets.value = res.data;
  } catch {
    // Ignore error
  }
}

// Request cancellation controller
let activeController = null

const loadDashboardData = async () => {
  const company_id = authStore.user?.company_id;
  if (!company_id || !selectedYear.value || !selectedPeriod.value) {
    isLoading.value = false
    return
  }

  // Cancel any active outstanding requests from a previous dashboard filter change
  if (activeController) {
    activeController.abort()
  }
  activeController = new AbortController()
  const signal = activeController.signal
  
  isLoading.value = true

  const y = selectedYear.value
  const p = selectedPeriod.value.toString().padStart(2, '0')

  try {
    // 1. Fetch current period data
    const currRes = await api.get('/reports/ledger', { 
      params: { year: y, period: p, preset: activePreset.value, company_id: company_id },
      signal
    })
    
    let currentPeriodPayload = null
    if (currRes.data && currRes.data.status === 'success') {
      currentPeriodPayload = {
        year: y,
        period: p,
        data: currRes.data.data,
        net_income: currRes.data.net_income || currRes.data.netIncome || 0
      }
    }

    // 2. Fetch previous period data (for month-over-month growth comparisons)
    let previousPeriodPayload = null
    const prevY = p === '01' ? (parseInt(y) - 1).toString() : y
    const prevP = p === '01' ? '12' : (parseInt(p) - 1).toString().padStart(2, '0')
    try {
      const prevRes = await api.get('/reports/ledger', { 
        params: { year: prevY, period: prevP, preset: activePreset.value, company_id: company_id },
        signal
      })
      if (prevRes.data && prevRes.data.status === 'success') {
        previousPeriodPayload = {
          year: prevY,
          period: prevP,
          data: prevRes.data.data,
          net_income: prevRes.data.net_income || prevRes.data.netIncome || 0
        }
      }
    } catch (err) {
      if (err.name === 'CanceledError' || err.message === 'canceled') throw err
      // Ignore other errors for optional comparisons
    }

    // 3. Fetch yearly data for selected year and comparison years (12 periods each)
    const yearsToFetch = [...new Set([selectedYear.value, ...comparisonYears.value])]
    const yearlyDataPayload = {}
    
    for (const year of yearsToFetch) {
      const yearPromises = []
      for (let i = 1; i <= 12; i++) {
        yearPromises.push(
          api.get('/reports/ledger', { 
            params: { year: year, period: i.toString().padStart(2, '0'), preset: activePreset.value, company_id: company_id },
            signal
          })
          .then(res => res.data)
          .catch((err) => {
            if (err.name === 'CanceledError' || err.message === 'canceled') throw err
            return null
          })
        )
      }
      yearlyDataPayload[year] = await Promise.all(yearPromises)
    }

    // Package the raw results into a single state payload passed down to the widgets
    rawLedgerData.value = {
      current: currentPeriodPayload,
      previous: previousPeriodPayload,
      yearlyData: yearlyDataPayload
    }

    // Fetch Operation Metrics if any operation widget is active
    const hasOps = activeWidgets.value.some(w => ['operation_metrics', 'lease_expirations'].includes(w.widget_key))
    if (hasOps) {
      try {
        const opsRes = await api.get('/v1/dashboard/operation/metrics', {
          params: { year: y, period: p, company_id: company_id },
          signal
        })
        leaseExpirations.value = opsRes.data.leaseExpirationsTimeline || []
      } catch (err) {
        if (err.name === 'CanceledError' || err.message === 'canceled') throw err
        console.error("Failed to fetch operation metrics for fsDashboard:", err)
      }
    }

    // Fetch Maintenance Status if any maintenance widget is active
    const hasMaint = activeWidgets.value.some(w => ['tickets_kpi', 'maintenance_status', 'tickets_by_category'].includes(w.widget_key))
    if (hasMaint) {
      try {
        const maintRes = await api.get('/v1/dashboard/maintenance/status', {
          params: { year: y, period: p, company_id: company_id },
          signal
        })
        ticketsByCategory.value = maintRes.data.ticketsByCategory || []
      } catch (err) {
        if (err.name === 'CanceledError' || err.message === 'canceled') throw err
        console.error("Failed to fetch maintenance metrics for fsDashboard:", err)
      }
    }

    isLoading.value = false

  } catch (err) {
    if (err.name === 'CanceledError' || err.message === 'canceled') {
      console.log('[fsDashboard] Requests aborted successfully.')
      return
    }
    isLoading.value = false
  }
}

onMounted(async () => {
  await fetchUserWidgets()
  filterStore.fetchFilters().then(() => loadDashboardData())
})

onBeforeUnmount(() => {
  // Abort any outstanding request when navigating away from the dashboard
  if (activeController) {
    activeController.abort()
  }
})

watch([selectedYear, selectedPeriod, activePreset, comparisonYears], () => loadDashboardData())

</script>

<template>
  <ReportLayout 
    title="Executive Dashboard" 
    :subtitle="`As of ${monthNames[selectedPeriod]} ${selectedYear}`">
    
    <template #controls>
      <div class="flex items-center gap-4">
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
        
        <div v-if="isLoading" class="flex flex-col items-center justify-center flex-1 min-h-0 w-full h-full">
          <div class="w-10 h-10 border-4 border-sky-100 border-t-sky-600 rounded-full animate-spin"></div>
          <p class="text-sky-600 font-medium animate-pulse text-sm mt-4">Loading Executive Data...</p>
        </div>

        <div v-else class="flex flex-col gap-5 pb-8 w-full">
          
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-5 w-full">
            <template v-for="widget in activeWidgets" :key="widget.widget_key">
              <div :class="{'lg:col-span-2': ['kpi_cards', 'operation_metrics', 'lease_expirations', 'tickets_kpi', 'maintenance_status', 'tickets_by_category'].includes(widget.widget_key)}">
                <component 
                  :is="widgetRegistry[widget.widget_key]" 
                  v-bind="getWidgetProps(widget)" 
                />
              </div>
            </template>
          </div>

        </div>
      </div>
    </div>
  </ReportLayout>
</template>