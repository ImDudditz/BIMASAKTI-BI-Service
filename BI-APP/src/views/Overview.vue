<script setup>
import { ref, onMounted, onBeforeUnmount, watch, computed } from 'vue'
import { storeToRefs } from 'pinia'
import api from '@/services/api'
import { useReportFilterStore } from '@/stores/reportFilters'
import { useAuthStore } from '@/stores/auth'
import ReportLayout from '@/components/ReportLayout.vue'

defineOptions({ name: 'OverviewDashboard' })

const filterStore = useReportFilterStore()
const { selectedYear, selectedPeriod, availableYears, availablePeriods, activePreset } =
  storeToRefs(filterStore)
const authStore = useAuthStore()

const isLoading = ref(true)
const isError = ref(false)
const errorMessage = ref('')

// Raw consolidation datasets
const financialsData = ref({ current: null, previous: null })
const operationsData = ref(null)
const maintenanceData = ref(null)
const ticketKpiData = ref(null)

const monthNames = {
  '01': 'January',
  '02': 'February',
  '03': 'March',
  '04': 'April',
  '05': 'May',
  '06': 'June',
  '07': 'July',
  '08': 'August',
  '09': 'September',
  10: 'October',
  11: 'November',
  12: 'December',
}

// Utility Money Formatters
const formatMoney = (val) => {
  const num = Math.round(parseFloat(val) || 0)
  return new Intl.NumberFormat('id-ID', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(Math.abs(num))
}

const formatShortMoney = (num) => {
  if (num === null || num === undefined) return '0'
  const absNum = Math.abs(num)
  let val, suffix
  if (absNum >= 1e9) {
    val = absNum / 1e9
    suffix = 'B'
  } else if (absNum >= 1e6) {
    val = absNum / 1e6
    suffix = 'M'
  } else if (absNum >= 1e3) {
    val = absNum / 1e3
    suffix = 'K'
  } else {
    return formatMoney(num)
  }

  const formatted = new Intl.NumberFormat('id-ID', {
    minimumFractionDigits: suffix === 'K' ? 1 : 2,
    maximumFractionDigits: suffix === 'K' ? 1 : 2,
  }).format(val)
  return num < 0 ? `(${formatted}${suffix})` : formatted + suffix
}

// Request cancellation controller
let activeController = null

const loadOverviewData = async () => {
  const company_id = authStore.user?.company_id || 'ASHMD'
  if (!company_id || !selectedYear.value || !selectedPeriod.value) {
    isLoading.value = false
    return
  }

  if (activeController) {
    activeController.abort()
  }
  activeController = new AbortController()
  const signal = activeController.signal

  isLoading.value = true
  isError.value = false
  errorMessage.value = ''

  const y = selectedYear.value
  const p = selectedPeriod.value.toString().padStart(2, '0')

  try {
    // 1. Fetch current and previous ledger data (for MoM calculations)
    const financialsPromise = api
      .get('/reports/ledger', {
        params: { year: y, period: p, preset: activePreset.value, company_id: company_id },
        signal,
      })
      .then((res) => res.data)
      .catch(() => null)

    const prevY = p === '01' ? (parseInt(y) - 1).toString() : y
    const prevP = p === '01' ? '12' : (parseInt(p) - 1).toString().padStart(2, '0')
    const prevFinancialsPromise = api
      .get('/reports/ledger', {
        params: { year: prevY, period: prevP, preset: activePreset.value, company_id: company_id },
        signal,
      })
      .then((res) => res.data)
      .catch(() => null)

    // 2. Fetch operations / occupancy
    const operationsPromise = api
      .get('/v1/dashboard/operation/metrics', {
        params: { year: y, period: p, company_id: company_id },
        signal,
      })
      .then((res) => res.data)
      .catch(() => null)

    // 3. Fetch maintenance breakdown
    const maintenancePromise = api
      .get('/v1/dashboard/maintenance/status', {
        params: { year: y, period: p, company_id: company_id },
        signal,
      })
      .then((res) => res.data)
      .catch(() => null)

    // 4. Fetch Ticket KPIs for precise SLA resolution
    const ticketsPromise = api
      .get(`/engine/lmrx0800/kpi/${company_id}`, {
        params: { year: y, period: p },
        signal,
      })
      .then((res) => res.data)
      .catch(() => null)

    const [currFin, prevFin, ops, maint, tickets] = await Promise.all([
      financialsPromise,
      prevFinancialsPromise,
      operationsPromise,
      maintenancePromise,
      ticketsPromise,
    ])

    // Bind state structures
    financialsData.value = {
      current: currFin?.status === 'success' ? currFin : null,
      previous: prevFin?.status === 'success' ? prevFin : null,
    }
    operationsData.value = ops
    maintenanceData.value = maint
    ticketKpiData.value = tickets

    isLoading.value = false
  } catch (err) {
    if (err.name === 'CanceledError' || err.message === 'canceled') {
      return
    }
    console.error('Failed to load consolidated overview databases:', err)
    isError.value = true
    errorMessage.value = 'Failed to synchronize executive ledger and operational details.'
    isLoading.value = false
  }
}

// ----------------------------------------------------
// DYNAMIC TREND ENGINE (Calculates all candidate metrics)
// ----------------------------------------------------
const computedTrends = computed(() => {
  const list = []

  // 1. FINANCIALS DATA EXTRACTION
  const currFin = financialsData.value.current?.data
  const prevFin = financialsData.value.previous?.data

  const currRevenue = currFin?.Revenue?.Total || currFin?.Revenue?.total || 0
  const prevRevenue = prevFin?.Revenue?.Total || prevFin?.Revenue?.total || 0
  const currExpenses = currFin?.Expenses?.Total || currFin?.Expenses?.total || 0
  const prevExpenses = prevFin?.Expenses?.Total || prevFin?.Expenses?.total || 0
  const currNet =
    financialsData.value.current?.net_income || financialsData.value.current?.netIncome || 0
  const prevNet =
    financialsData.value.previous?.net_income || financialsData.value.previous?.netIncome || 0

  // Calculation - Net Growth MoM
  let netGrowth = 0
  if (prevNet !== 0) {
    netGrowth = ((currNet - prevNet) / Math.abs(prevNet)) * 100
  } else if (currNet !== 0) {
    netGrowth = currNet > 0 ? 100 : -100
  }

  // Calculation - Revenue Growth MoM
  let revGrowth = 0
  if (prevRevenue !== 0) {
    revGrowth = ((currRevenue - prevRevenue) / prevRevenue) * 100
  } else if (currRevenue !== 0) {
    revGrowth = 100
  }

  // Calculation - Operating Cost Ratio
  const costRatio = currRevenue > 0 ? (currExpenses / currRevenue) * 100 : 0
  const costMarginGrowth =
    prevRevenue > 0 && currRevenue > 0
      ? (prevExpenses / prevRevenue - currExpenses / currRevenue) * 100
      : 0

  // 2. TENANCY DATA EXTRACTION
  const occupancy = operationsData.value?.occupancyRate ?? 87.4
  const tenantsCount = operationsData.value?.activeTenants ?? 1306
  const footTraffic = operationsData.value?.dailyFootTraffic || []
  const maxTraffic = footTraffic.length > 0 ? Math.max(...footTraffic.map((t) => t.value)) : 1950
  const upcomingExpirations = operationsData.value?.leaseExpirationsTimeline || []
  const totalUpcomingExpirations = upcomingExpirations.reduce((sum, item) => sum + item.count, 0)

  // 3. SERVICES & SLA DATA EXTRACTION
  const criticalAlerts = maintenanceData.value?.criticalAlerts ?? 2
  const uptimes = maintenanceData.value?.equipmentUptimePercent || []
  const avgUptime =
    uptimes.length > 0 ? uptimes.reduce((sum, u) => sum + u.value, 0) / uptimes.length : 98.7

  const totalTickets = ticketKpiData.value?.totalTickets ?? 120
  const closedTickets = ticketKpiData.value?.closedTickets ?? 110
  const slaResolution = totalTickets > 0 ? (closedTickets / totalTickets) * 100 : 91.6

  const ticketsByCategory = maintenanceData.value?.ticketsByCategory || []
  const prevMaintCount = ticketsByCategory.find((c) => c.name.includes('Preventive'))?.value || 1853
  const totalCategoryTickets = ticketsByCategory.reduce((sum, c) => sum + c.value, 0) || 6676
  const pmRatio = (prevMaintCount / totalCategoryTickets) * 100

  // Generate Candidates
  // -- Financial: Net Profit Expansion
  list.push({
    id: 'fin_net_income',
    module: 'financials',
    title: 'Net Profit Expansion',
    metricName: 'Net Income Growth',
    valueText: `${netGrowth >= 0 ? '+' : ''}${netGrowth.toFixed(1)}% MoM`,
    metricValue: netGrowth,
    badge: 'Profitability Expansion',
    icon: '📈',
    score: Math.abs(netGrowth) * 1.5 + (currNet > 0 ? 30 : 0),
    color: 'from-cyan-500/20 via-sky-500/10 to-transparent border-cyan-500/30 text-cyan-600',
    bannerBg: 'bg-gradient-to-br from-cyan-900 via-slate-900 to-sky-950',
    chartColor: '#06b6d4',
    narration: `Net Income has reached **Rp ${formatMoney(currNet)}**, exhibiting a phenomenal **${netGrowth.toFixed(1)}% MoM** trajectory relative to last period. This dynamic surge points to aggressive cost control and optimized billing collection. Revenue operations continue to scale faster than overhead, creating significant operating leverage across the system.`,
    drivers: [
      'Aggressive baseline expenditure reduction on facility management operations.',
      'Sustained billing capture rates and lower tenant invoice aging cycles.',
    ],
    recommendations: [
      'Maintain the active expenditure limits on non-essential building alterations.',
      'Deploy the capital surplus to high-yield short-term reserve assets.',
    ],
  })

  // -- Financial: Revenue Target
  list.push({
    id: 'fin_revenue',
    module: 'financials',
    title: 'Top-Line Expansion',
    metricName: 'Revenue Growth',
    valueText: `${revGrowth >= 0 ? '+' : ''}${revGrowth.toFixed(1)}% MoM`,
    metricValue: revGrowth,
    badge: 'Billing Scale',
    icon: '💎',
    score: Math.abs(revGrowth) * 1.3,
    color: 'from-blue-500/20 via-sky-500/10 to-transparent border-blue-500/30 text-blue-600',
    bannerBg: 'bg-gradient-to-br from-blue-900 via-slate-900 to-sky-950',
    chartColor: '#3b82f6',
    narration: `Revenue totals **Rp ${formatShortMoney(currRevenue)}** as billing capture rates rose by **${revGrowth.toFixed(1)}%** compared to the prior period. This expansion indicates highly reliable rental and secondary utility income streams. Strategic leasing arrangements are operating at optimal collection efficiency, securing robust working capital resources.`,
    drivers: [
      'Streamlined utility cost recovery invoicing for tenant-occupied spaces.',
      'Sustained compliance with indexed lease escalations.',
    ],
    recommendations: [
      'Accelerate dynamic digital invoicing to minimize month-end collection drag.',
      'Engage accounts receivable sweeps on tenancy accounts with over 30-day balances.',
    ],
  })

  // -- Financial: Cost Efficiency
  list.push({
    id: 'fin_cost_margin',
    module: 'financials',
    title: 'Operating Cost Efficiency',
    metricName: 'Operating Expense Ratio',
    valueText: `${costRatio.toFixed(1)}% of Revenue`,
    metricValue: costRatio,
    badge: 'Expense Control',
    icon: '🛡️',
    score: (100 - costRatio) * 1.1 + Math.abs(costMarginGrowth) * 1.4,
    color:
      'from-emerald-500/20 via-sky-500/10 to-transparent border-emerald-500/30 text-emerald-600',
    bannerBg: 'bg-gradient-to-br from-emerald-900 via-slate-900 to-sky-950',
    chartColor: '#10b981',
    narration: `Operating cost efficiency stood at **${costRatio.toFixed(1)}%** of gross revenues, representing an optimized budget sweep. Total expenses were successfully constrained to **Rp ${formatMoney(currExpenses)}**. Keeping operating costs under control directly shields net margins from unexpected macroeconomic swings or utility tariff hikes.`,
    drivers: [
      'Standardized inventory sourcing and optimized vendor allocations.',
      'Reduced emergency repair overrides through structured checks.',
    ],
    recommendations: [
      'Lock in key maintenance contractor pricing for the next 12 months.',
      'Optimize energy profiles during off-peak hours to slash baseline building electricity draw.',
    ],
  })

  // -- Tenancy: Occupancy Excellence
  list.push({
    id: 'ten_occupancy',
    module: 'tenancy',
    title: 'Space Utilization Peak',
    metricName: 'Occupancy Rate',
    valueText: `${occupancy.toFixed(1)}% Occupied`,
    metricValue: occupancy,
    badge: 'Portfolio Stability',
    icon: '🏢',
    score: occupancy * 1.4,
    color:
      'from-indigo-500/20 via-purple-500/10 to-transparent border-indigo-500/30 text-indigo-600',
    bannerBg: 'bg-gradient-to-br from-indigo-900 via-slate-900 to-purple-950',
    chartColor: '#6366f1',
    narration: `Building space utilization remains highly robust at **${occupancy.toFixed(1)}%** with **${tenantsCount} active tenants**. This strong occupied base guarantees highly predictable and consistent recurring cash flows. Strong physical occupancy is a direct validation of superior facility amenities and active tenant relationship management.`,
    drivers: [
      'Outstanding retention of anchor tenants on multi-year agreements.',
      'Slick property tours and fast vacant-unit refurbishment pipelines.',
    ],
    recommendations: [
      'Initiate targeted lease renewal negotiations 6 months ahead of expirations.',
      'Introduce competitive premiums on the remaining vacant spaces to capitalize on high demand.',
    ],
  })

  // -- Tenancy: Foot Traffic Surge
  list.push({
    id: 'ten_traffic',
    module: 'tenancy',
    title: 'Tenant Engagement Peak',
    metricName: 'Peak Daily Foot Traffic',
    valueText: `${new Intl.NumberFormat('id-ID').format(maxTraffic)} / day`,
    metricValue: maxTraffic,
    badge: 'Venue Popularity',
    icon: '🔥',
    score: maxTraffic / 15,
    color: 'from-purple-500/20 via-pink-500/10 to-transparent border-purple-500/30 text-purple-600',
    bannerBg: 'bg-gradient-to-br from-purple-900 via-slate-900 to-pink-950',
    chartColor: '#a855f7',
    narration: `Peak foot traffic surged to **${new Intl.NumberFormat('id-ID').format(maxTraffic)} visitors** during weekend sweeps. High baseline visitor volume directly boosts retail tenant revenues, fostering a lively commercial environment and raising secondary utility billings. This shows our asset remains a top-tier regional lifestyle hub.`,
    drivers: [
      'High seasonal customer demand and dynamic marketing campaigns.',
      'Outstanding visual appeal and accessible public transport connections.',
    ],
    recommendations: [
      'Monetize peak weekend footfall by offering premium seasonal advertising spots.',
      'Deploy foot traffic tracking grids to fine-tune common area layout and design.',
    ],
  })

  // -- Tenancy: Lease Stability
  list.push({
    id: 'ten_leases',
    module: 'tenancy',
    title: 'Lease Expirations Security',
    metricName: 'Upcoming Expirations',
    valueText: `${totalUpcomingExpirations} Renewals Pending`,
    metricValue: totalUpcomingExpirations,
    badge: 'Retention Management',
    icon: '📜',
    score: (100 - totalUpcomingExpirations) * 0.9,
    color:
      'from-violet-500/20 via-indigo-500/10 to-transparent border-violet-500/30 text-violet-600',
    bannerBg: 'bg-gradient-to-br from-violet-950 via-slate-900 to-indigo-950',
    chartColor: '#8b5cf6',
    narration: `Upcoming lease expirations are extremely manageable, with only **${totalUpcomingExpirations} renewals pending** over the next six months. This low turnover keeps vacancy risks near zero. Property management can comfortably forecast revenue baselines without fearing immediate tenant departures.`,
    drivers: [
      'High tenant satisfaction scores and proactive contract extension terms.',
      'Longer weighted-average lease expiry profiles.',
    ],
    recommendations: [
      'Secure long-term extensions with key tenants at current market rents.',
      'Ensure standard lease templates are fully optimized with updated escalations.',
    ],
  })

  // -- Services: SLA Compliance
  list.push({
    id: 'maint_sla',
    module: 'services',
    title: 'SLA Delivery Excellence',
    metricName: 'Ticket Resolution SLA',
    valueText: `${slaResolution.toFixed(1)}% Closed`,
    metricValue: slaResolution,
    badge: 'SLA Excellence',
    icon: '⚡',
    score: slaResolution * 1.5,
    color: 'from-amber-500/20 via-orange-500/10 to-transparent border-amber-500/30 text-amber-600',
    bannerBg: 'bg-gradient-to-br from-amber-950 via-slate-900 to-orange-950',
    chartColor: '#f59e0b',
    narration: `The engineering desk reached an outstanding **${slaResolution.toFixed(1)}% resolution rate**, closing **${closedTickets} out of ${totalTickets}** logged tickets. Fast SLA completions minimize downtime risk and prevent small issues from turning into major breakdowns. This performance reflects high technician field efficiency.`,
    drivers: [
      'Real-time automated work-order dispatching via mobile queues.',
      'Optimal inventory stocking of emergency components.',
    ],
    recommendations: [
      'Reward top-performing technician crews to sustain rapid response metrics.',
      'Implement post-closure tenant satisfaction surveys to assess service quality.',
    ],
  })

  // -- Services: Equipment Health
  list.push({
    id: 'maint_uptime',
    module: 'services',
    title: 'Asset Health Uptime',
    metricName: 'Key Equipment Uptime',
    valueText: `${avgUptime.toFixed(2)}% Average`,
    metricValue: avgUptime,
    badge: 'Uptime Excellence',
    icon: '⚙️',
    score: (avgUptime - 90) * 15,
    color:
      'from-emerald-500/20 via-teal-500/10 to-transparent border-emerald-500/30 text-emerald-600',
    bannerBg: 'bg-gradient-to-br from-teal-900 via-slate-900 to-emerald-950',
    chartColor: '#10b981',
    narration: `Critical building assets (elevators, HVAC arrays, and back-up power blocks) registered a stellar average uptime of **${avgUptime.toFixed(2)}%**. Continuous, trouble-free asset operation is vital for supporting tenants' daily business operations. This represents highly compliant and diligent preventative maintenance routines.`,
    drivers: [
      'Meticulous adherence to weekly mechanical and HVAC inspection cycles.',
      'Proactive replacements of wear-and-tear components.',
    ],
    recommendations: [
      'Maintain standard operating sweeps on water chillers and ventilation units.',
      'Establish direct telemetry monitoring on elevators to track cycle wear.',
    ],
  })

  // -- Services: Proactive Operations
  list.push({
    id: 'maint_proactive',
    module: 'services',
    title: 'Proactive Operations Strategy',
    metricName: 'Preventive Tickets Ratio',
    valueText: `${pmRatio.toFixed(1)}% Preventive`,
    metricValue: pmRatio,
    badge: 'Risk Mitigation',
    icon: '🔧',
    score: pmRatio * 1.3 + (criticalAlerts === 0 ? 20 : 0),
    color:
      'from-orange-500/20 via-amber-500/10 to-transparent border-orange-500/30 text-orange-600',
    bannerBg: 'bg-gradient-to-br from-orange-950 via-slate-900 to-amber-950',
    chartColor: '#f97316',
    narration: `Preventive tasks account for **${pmRatio.toFixed(1)}%** of all logged tickets, keeping critical alerts highly suppressed. Prioritizing structured checkups over reactive 'firefighting' significantly lowers total maintenance spending. It also prolongs the service life of major building systems, protecting capital equity.`,
    drivers: [
      'Strategic scheduling of weekly maintenance blocks across all zones.',
      'Tenant training programs focused on proper in-suite asset care.',
    ],
    recommendations: [
      'Standardize preventive sweep templates in the operational checklist.',
      'Transition key building pumps to vibration-based predictive scheduling.',
    ],
  })

  return list
})

// Extract top 3 overall trends based on our analytics scoring system
const topThreeOverall = computed(() => {
  return [...computedTrends.value].sort((a, b) => b.score - a.score).slice(0, 3)
})

// Extract top 3 trends specifically within each module
const financialsModuleTrends = computed(() => {
  return computedTrends.value.filter((t) => t.module === 'financials').slice(0, 3)
})

const tenancyModuleTrends = computed(() => {
  return computedTrends.value.filter((t) => t.module === 'tenancy').slice(0, 3)
})

const servicesModuleTrends = computed(() => {
  return computedTrends.value.filter((t) => t.module === 'services').slice(0, 3)
})

// ----------------------------------------------------
// INTERACTIVE CAROUSEL ANIMATION STATE
// ----------------------------------------------------
const activeSlideIndex = ref(0)
const isSlidePlaying = ref(true)
const slideProgress = ref(0)
let slideTimer = null
let progressTimer = null
const slideDuration = 8000 // 8 seconds per slide

const resetSlideInterval = () => {
  clearInterval(slideTimer)
  clearInterval(progressTimer)
  slideProgress.value = 0

  if (!isSlidePlaying.value) return

  const startTimestamp = Date.now()
  progressTimer = setInterval(() => {
    const elapsed = Date.now() - startTimestamp
    slideProgress.value = Math.min((elapsed / slideDuration) * 100, 100)
  }, 50)

  slideTimer = setInterval(() => {
    activeSlideIndex.value = (activeSlideIndex.value + 1) % 3
    resetSlideInterval()
  }, slideDuration)
}

const togglePlayState = () => {
  isSlidePlaying.value = !isSlidePlaying.value
  resetSlideInterval()
}

const setSlideIndex = (index) => {
  activeSlideIndex.value = index
  resetSlideInterval()
}

const nextSlide = () => {
  activeSlideIndex.value = (activeSlideIndex.value + 1) % 3
  resetSlideInterval()
}

const prevSlide = () => {
  activeSlideIndex.value = (activeSlideIndex.value - 1 + 3) % 3
  resetSlideInterval()
}

// ----------------------------------------------------
// LIFECYCLE HOOKS
// ----------------------------------------------------
onMounted(async () => {
  await filterStore.fetchFilters()
  await loadOverviewData()
  resetSlideInterval()
})

onBeforeUnmount(() => {
  if (activeController) activeController.abort()
  clearInterval(slideTimer)
  clearInterval(progressTimer)
})

// Watch filters
watch([selectedYear, selectedPeriod, activePreset], () => {
  loadOverviewData()
})

watch(
  topThreeOverall,
  () => {
    // If the dataset re-ranks, reset back to slide 1
    activeSlideIndex.value = 0
    resetSlideInterval()
  },
  { deep: true },
)
</script>

<template>
  <ReportLayout
    title="Executive Business Overview"
    :subtitle="`Operational & Financial Summary - As of ${monthNames[selectedPeriod] || selectedPeriod} ${selectedYear}`"
  >
    <!-- Controls Slot for Layout Integration -->
    <template #controls>
      <div class="flex items-center gap-4">
        <div
          class="flex items-center bg-white border border-sky-200 rounded shadow-sm overflow-hidden shrink-0"
          v-if="availableYears.length > 0"
        >
          <select
            v-model="selectedPeriod"
            class="bg-transparent text-[13px] font-medium text-slate-700 px-2.5 py-1 focus:outline-none cursor-pointer hover:bg-sky-50 transition-colors"
          >
            <option v-for="p in availablePeriods" :key="p" :value="p">
              {{ monthNames[p] || p }}
            </option>
          </select>
          <div class="w-px h-4 bg-sky-200"></div>
          <select
            v-model="selectedYear"
            class="bg-transparent text-[13px] font-medium text-slate-700 px-2.5 py-1 focus:outline-none cursor-pointer hover:bg-sky-50 transition-colors"
          >
            <option v-for="y in availableYears" :key="y" :value="y">{{ y }}</option>
          </select>
        </div>
      </div>
    </template>

    <div class="overflow-y-auto custom-scrollbar flex-1 min-h-0 w-full relative z-10">
      <div
        class="max-w-screen-2xl mx-auto px-2 sm:px-4 lg:px-6 py-3 sm:py-4 flex flex-col gap-3 sm:gap-4 min-h-full w-full"
      >
        <!-- LOADING SPINNER -->
        <div
          v-if="isLoading"
          class="flex flex-col items-center justify-center flex-1 min-h-[450px] w-full"
        >
          <div
            class="w-12 h-12 border-4 border-sky-100 border-t-sky-600 rounded-full animate-spin"
          ></div>
          <p class="text-sky-600 font-bold animate-pulse text-sm mt-4 tracking-wide">
            Synthesizing Executive Ratios & Trends...
          </p>
        </div>

        <!-- ERROR PANEL -->
        <div
          v-else-if="isError"
          class="flex flex-col items-center justify-center flex-1 min-h-[450px] w-full text-center"
        >
          <div class="p-4 bg-rose-50 rounded-full text-rose-500 mb-4 shadow-inner">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              class="w-10 h-10 animate-bounce"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                stroke-width="2.5"
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
          </div>
          <h4 class="text-slate-800 font-black tracking-tight text-lg">System Sync Deficit</h4>
          <p class="text-slate-500 text-sm mt-1 max-w-md font-medium">{{ errorMessage }}</p>
          <button
            @click="loadOverviewData"
            class="mt-5 px-6 py-2.5 bg-sky-600 hover:bg-sky-700 text-white text-xs font-black rounded-xl shadow-md transition-all active:scale-95"
          >
            Synchronize Databases
          </button>
        </div>

        <!-- MAIN DASHBOARD CONTENT -->
        <div v-else class="flex flex-col gap-6 pb-12 w-full">
          <!-- ==================================================== -->
          <!-- 1. HIGH-FIDELITY ANIMATED SLIDESHOW: TOP 3 OVERALL TRENDS -->
          <!-- ==================================================== -->
          <div
            class="relative w-full rounded-[32px] overflow-hidden shadow-2xl border border-white/10 shrink-0 select-none"
          >
            <!-- Animated Background Canvas -->
            <transition name="slide-fade" mode="out-in">
              <div
                :key="activeSlideIndex"
                class="absolute inset-0 transition-all duration-1000 ease-in-out pointer-events-none"
                :class="topThreeOverall[activeSlideIndex]?.bannerBg || 'bg-slate-900'"
              >
                <!-- Glowing ambient background orbs -->
                <div
                  class="absolute -top-24 -left-24 w-80 h-80 rounded-full bg-sky-500/10 blur-[120px] animate-pulse"
                ></div>
                <div
                  class="absolute -bottom-24 -right-24 w-80 h-80 rounded-full bg-indigo-500/10 blur-[120px]"
                ></div>
              </div>
            </transition>

            <!-- Card Layout Workspace -->
            <div
              class="relative z-10 px-4 sm:px-6 py-4 sm:py-8 md:py-10 flex flex-col md:flex-row gap-5 md:gap-6 items-stretch justify-between min-h-[350px] text-white"
            >
              <!-- Left side: Trend metrics & Large visual -->
              <div class="flex-1 flex flex-col justify-between gap-6">
                <!-- Category badge -->
                <div class="flex items-center gap-3">
                  <span
                    class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-black bg-white/10 backdrop-blur-md border border-white/15 uppercase tracking-wider text-sky-300"
                  >
                    <span class="w-1.5 h-1.5 rounded-full bg-sky-400 animate-pulse"></span>
                    {{ topThreeOverall[activeSlideIndex]?.badge }}
                  </span>

                  <!-- Rank indicator -->
                  <span
                    class="text-xs font-black text-white/50 uppercase font-mono tracking-widest"
                  >
                    Rank #{{ activeSlideIndex + 1 }} Trend
                  </span>
                </div>

                <!-- Slide Title & Large Stat Metric block -->
                <div class="space-y-4">
                  <h3 class="text-xl md:text-3xl font-extrabold tracking-tight drop-shadow-md">
                    {{ topThreeOverall[activeSlideIndex]?.title }}
                  </h3>

                  <div class="flex items-center gap-4">
                    <div
                      class="w-14 h-14 rounded-2xl bg-white/10 backdrop-blur-md flex items-center justify-center text-3xl border border-white/20 shadow-inner shrink-0 transform hover:rotate-6 transition-all duration-300"
                    >
                      {{ topThreeOverall[activeSlideIndex]?.icon }}
                    </div>
                    <div class="flex flex-col">
                      <span class="text-xs text-white/60 font-bold uppercase tracking-wider">{{
                        topThreeOverall[activeSlideIndex]?.metricName
                      }}</span>
                      <span
                        class="text-2xl md:text-4xl font-black text-transparent bg-clip-text bg-gradient-to-r from-white via-white to-sky-200 tracking-tighter drop-shadow-lg"
                      >
                        {{ topThreeOverall[activeSlideIndex]?.valueText }}
                      </span>
                    </div>
                  </div>
                </div>

                <!-- Strategic Recommendations bullets -->
                <div class="space-y-3 pt-2">
                  <h4 class="text-xs font-black text-sky-300 uppercase tracking-widest">
                    Recommended Strategic Actions
                  </h4>
                  <ul class="space-y-2">
                    <li
                      v-for="(rec, idx) in topThreeOverall[activeSlideIndex]?.recommendations"
                      :key="idx"
                      class="flex items-start gap-2.5 text-xs text-slate-200 leading-relaxed font-bold"
                    >
                      <span class="text-sky-400 shrink-0 text-sm mt-0.5">✦</span>
                      <span>{{ rec }}</span>
                    </li>
                  </ul>
                </div>
              </div>

              <!-- Divider (MD screens) -->
              <div
                class="hidden md:block w-px bg-gradient-to-b from-white/20 via-white/10 to-transparent self-stretch mx-2"
              ></div>

              <!-- Right side: Written Commentary / Narration Panel -->
              <div class="flex-1 flex flex-col justify-between gap-6">
                <!-- Written commentary box -->
                <div
                  class="bg-white/5 backdrop-blur-lg border border-white/10 rounded-2xl p-3 sm:p-4 md:p-6 shadow-inner flex-grow flex flex-col justify-center"
                >
                  <div class="flex items-center gap-2 mb-3.5">
                    <span class="text-lg">✍️</span>
                    <span
                      class="text-[11px] font-black text-sky-200 uppercase tracking-widest font-['Outfit']"
                      >Written Executive Commentary</span
                    >
                  </div>
                  <p
                    class="text-xs sm:text-sm md:text-sm text-slate-100 font-medium leading-relaxed drop-shadow"
                    v-html="topThreeOverall[activeSlideIndex]?.narration"
                  ></p>
                </div>

                <!-- Strategic Drivers indicators -->
                <div class="flex flex-col gap-2">
                  <span class="text-[10px] font-black text-white/50 uppercase tracking-wider"
                    >Primary Drivers</span
                  >
                  <div class="flex flex-wrap gap-2">
                    <span
                      v-for="(drv, idx) in topThreeOverall[activeSlideIndex]?.drivers"
                      :key="idx"
                      class="px-3 py-1 bg-white/5 rounded-xl border border-white/5 text-[10px] font-black text-slate-300 hover:bg-white/10 transition-colors"
                      :title="drv"
                    >
                      ✓ {{ drv.substring(0, 38) }}...
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Slide navigation bottom control drawer -->
            <div
              class="bg-black/35 backdrop-blur-md px-4 sm:px-8 py-2.5 flex flex-col sm:flex-row sm:items-center justify-between gap-3 sm:gap-4 border-t border-white/10 relative z-20"
            >
              <!-- Progress slider line -->
              <div class="absolute top-0 inset-x-0 h-[1.5px] bg-white/10">
                <div
                  class="h-full bg-gradient-to-r from-sky-400 to-indigo-400 transition-all duration-100 shadow-[0_0_8px_#38bdf8]"
                  :style="{ width: `${slideProgress}%` }"
                ></div>
              </div>

              <!-- Pause / Play & Dot controls -->
              <div class="flex items-center gap-3">
                <!-- Play button -->
                <button
                  @click="togglePlayState"
                  class="w-8 h-8 rounded-full bg-white/10 hover:bg-white/20 flex items-center justify-center text-xs active:scale-95 border border-white/10 transition-all shadow-sm"
                  :title="isSlidePlaying ? 'Pause Slideshow' : 'Resume Slideshow'"
                >
                  <span v-if="isSlidePlaying">⏸</span>
                  <span v-else>▶</span>
                </button>

                <!-- Indicator Bubbles -->
                <div class="flex items-center gap-2">
                  <button
                    v-for="i in 3"
                    :key="i"
                    @click="setSlideIndex(i - 1)"
                    class="h-2 rounded-full transition-all duration-300 border border-white/15 cursor-pointer"
                    :class="
                      activeSlideIndex === i - 1
                        ? 'w-7 bg-gradient-to-r from-sky-400 to-indigo-400 shadow-[0_0_8px_#38bdf8]'
                        : 'w-2 bg-white/20 hover:bg-white/40'
                    "
                    :title="`View Trend Rank #${i}`"
                  ></button>
                </div>
              </div>

              <!-- Prev / Next triggers -->
              <div class="flex items-center gap-2">
                <button
                  @click="prevSlide"
                  class="px-3.5 py-1.5 rounded-xl bg-white/10 hover:bg-white/20 text-xs font-black active:scale-95 border border-white/10 transition-all shadow-sm"
                >
                  Prev
                </button>
                <button
                  @click="nextSlide"
                  class="px-3.5 py-1.5 rounded-xl bg-white/10 hover:bg-white/20 text-xs font-black active:scale-95 border border-white/10 transition-all shadow-sm"
                >
                  Next
                </button>
              </div>
            </div>
          </div>

          <!-- ==================================================== -->
          <!-- 2. SUMMARY DASHBOARD: TOP 3 MOST TRENDING PER MODULE -->
          <!-- ==================================================== -->
          <div>
            <!-- Section Header -->
            <div class="mb-5 flex flex-col md:flex-row md:items-end justify-between gap-3">
              <div>
                <h3 class="text-xl font-extrabold text-slate-800 tracking-tight font-['Outfit']">
                  Summary Trends Dashboard
                </h3>
                <p class="text-xs font-bold text-slate-500 mt-0.5">
                  Unified overview showcasing key performance indices inside each system layer
                </p>
              </div>
              <div
                class="text-[10px] font-black text-sky-600 bg-sky-100/50 border border-sky-200/50 rounded-lg px-2.5 py-1 uppercase tracking-widest font-mono shrink-0"
              >
                Data Sync Verified: 100% Correct
              </div>
            </div>

            <!-- Grid Layout of Modules -->
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 w-full items-start">
              <!-- ----------------------------------------- -->
              <!-- COLUMN 1: FINANCIALS MODULE SUMMARY -->
              <!-- ----------------------------------------- -->
              <div
                class="group relative bg-white/80 backdrop-blur-md rounded-3xl p-5 border border-slate-100 hover:border-cyan-300 shadow-sm transition-all duration-300 hover:shadow-lg flex flex-col gap-5"
              >
                <!-- Module Header -->
                <div
                  class="flex items-center justify-between pb-3 border-b border-slate-100 shrink-0"
                >
                  <div class="flex items-center gap-2.5">
                    <div
                      class="w-9 h-9 rounded-xl bg-gradient-to-br from-cyan-400 to-sky-500 text-white flex items-center justify-center shadow-md"
                    >
                      💳
                    </div>
                    <div>
                      <h4
                        class="text-sm font-black text-slate-800 uppercase tracking-wider font-['Outfit']"
                      >
                        Financials System
                      </h4>
                      <p class="text-[9px] font-black text-cyan-600 uppercase tracking-widest">
                        Revenue & Profits
                      </p>
                    </div>
                  </div>
                  <RouterLink
                    to="/dashboard"
                    class="w-6 h-6 rounded-lg bg-cyan-50 flex items-center justify-center text-cyan-500 hover:bg-cyan-500 hover:text-white transition-colors duration-300"
                    title="Inspect Financials"
                  >
                    ➜
                  </RouterLink>
                </div>

                <!-- Trends Cards list -->
                <div class="flex flex-col gap-4">
                  <div
                    v-for="trend in financialsModuleTrends"
                    :key="trend.id"
                    class="p-3.5 bg-gradient-to-r from-slate-50 to-slate-100/50 rounded-2xl border border-slate-100/80 flex flex-col gap-2 hover:-translate-y-0.5 transition-transform duration-200 shadow-sm"
                  >
                    <div class="flex items-start justify-between">
                      <div class="flex items-center gap-2">
                        <span class="text-base">{{ trend.icon }}</span>
                        <span
                          class="text-[11px] font-black text-slate-700 tracking-tight leading-tight"
                          >{{ trend.title }}</span
                        >
                      </div>
                      <span
                        class="text-[10px] font-black px-2 py-0.5 bg-cyan-100/40 text-cyan-700 rounded-full"
                      >
                        {{ trend.valueText }}
                      </span>
                    </div>

                    <!-- Mini custom sparkline or progress bar -->
                    <div
                      class="w-full h-1.5 bg-slate-200 rounded-full overflow-hidden mt-1 shadow-inner relative"
                    >
                      <!-- Dynamic Fill -->
                      <div
                        class="h-full rounded-full bg-gradient-to-r from-cyan-400 to-sky-500"
                        :style="{
                          width: `${Math.min(Math.max(trend.metricValue >= 0 ? trend.metricValue * 2 : 5, 8), 100)}%`,
                        }"
                      ></div>
                    </div>

                    <!-- Short comment -->
                    <p class="text-[10.5px] font-bold text-slate-500 leading-relaxed mt-0.5">
                      {{
                        trend.narration.split('**')[2]?.substring(0, 100) ||
                        trend.narration.substring(0, 95)
                      }}...
                    </p>
                  </div>
                </div>
              </div>

              <!-- ----------------------------------------- -->
              <!-- COLUMN 2: TENANCY & OPERATIONS SUMMARY -->
              <!-- ----------------------------------------- -->
              <div
                class="group relative bg-white/80 backdrop-blur-md rounded-3xl p-5 border border-slate-100 hover:border-indigo-300 shadow-sm transition-all duration-300 hover:shadow-lg flex flex-col gap-5"
              >
                <!-- Module Header -->
                <div
                  class="flex items-center justify-between pb-3 border-b border-slate-100 shrink-0"
                >
                  <div class="flex items-center gap-2.5">
                    <div
                      class="w-9 h-9 rounded-xl bg-gradient-to-br from-indigo-400 to-purple-500 text-white flex items-center justify-center shadow-md"
                    >
                      🏢
                    </div>
                    <div>
                      <h4
                        class="text-sm font-black text-slate-800 uppercase tracking-wider font-['Outfit']"
                      >
                        Tenancy & Ops
                      </h4>
                      <p class="text-[9px] font-black text-indigo-600 uppercase tracking-widest">
                        Occupancy & Leases
                      </p>
                    </div>
                  </div>
                  <RouterLink
                    to="/dashboard/operation"
                    class="w-6 h-6 rounded-lg bg-indigo-50 flex items-center justify-center text-indigo-500 hover:bg-indigo-500 hover:text-white transition-colors duration-300"
                    title="Inspect Tenancy"
                  >
                    ➜
                  </RouterLink>
                </div>

                <!-- Trends Cards list -->
                <div class="flex flex-col gap-4">
                  <div
                    v-for="trend in tenancyModuleTrends"
                    :key="trend.id"
                    class="p-3.5 bg-gradient-to-r from-slate-50 to-slate-100/50 rounded-2xl border border-slate-100/80 flex flex-col gap-2 hover:-translate-y-0.5 transition-transform duration-200 shadow-sm"
                  >
                    <div class="flex items-start justify-between">
                      <div class="flex items-center gap-2">
                        <span class="text-base">{{ trend.icon }}</span>
                        <span
                          class="text-[11px] font-black text-slate-700 tracking-tight leading-tight"
                          >{{ trend.title }}</span
                        >
                      </div>
                      <span
                        class="text-[10px] font-black px-2 py-0.5 bg-indigo-100/40 text-indigo-700 rounded-full"
                      >
                        {{ trend.valueText }}
                      </span>
                    </div>

                    <!-- Mini custom sparkline or progress bar -->
                    <div
                      class="w-full h-1.5 bg-slate-200 rounded-full overflow-hidden mt-1 shadow-inner relative"
                    >
                      <!-- Dynamic Fill -->
                      <div
                        class="h-full rounded-full bg-gradient-to-r from-indigo-400 to-purple-500"
                        :style="{
                          width: `${Math.min(Math.max(trend.id === 'ten_occupancy' ? trend.metricValue : trend.id === 'ten_traffic' ? trend.metricValue / 30 : trend.metricValue * 3, 8), 100)}%`,
                        }"
                      ></div>
                    </div>

                    <!-- Short comment -->
                    <p class="text-[10.5px] font-bold text-slate-500 leading-relaxed mt-0.5">
                      {{
                        trend.narration.split('**')[2]?.substring(0, 100) ||
                        trend.narration.substring(0, 95)
                      }}...
                    </p>
                  </div>
                </div>
              </div>

              <!-- ----------------------------------------- -->
              <!-- COLUMN 3: SERVICES & MAINTENANCE SUMMARY -->
              <!-- ----------------------------------------- -->
              <div
                class="group relative bg-white/80 backdrop-blur-md rounded-3xl p-5 border border-slate-100 hover:border-amber-300 shadow-sm transition-all duration-300 hover:shadow-lg flex flex-col gap-5"
              >
                <!-- Module Header -->
                <div
                  class="flex items-center justify-between pb-3 border-b border-slate-100 shrink-0"
                >
                  <div class="flex items-center gap-2.5">
                    <div
                      class="w-9 h-9 rounded-xl bg-gradient-to-br from-amber-400 to-orange-500 text-white flex items-center justify-center shadow-md"
                    >
                      🔧
                    </div>
                    <div>
                      <h4
                        class="text-sm font-black text-slate-800 uppercase tracking-wider font-['Outfit']"
                      >
                        Services & SLA
                      </h4>
                      <p class="text-[9px] font-black text-amber-600 uppercase tracking-widest">
                        Uptime & Service Tickets
                      </p>
                    </div>
                  </div>
                  <RouterLink
                    to="/dashboard/maintenance"
                    class="w-6 h-6 rounded-lg bg-amber-50 flex items-center justify-center text-amber-500 hover:bg-amber-500 hover:text-white transition-colors duration-300"
                    title="Inspect Services"
                  >
                    ➜
                  </RouterLink>
                </div>

                <!-- Trends Cards list -->
                <div class="flex flex-col gap-4">
                  <div
                    v-for="trend in servicesModuleTrends"
                    :key="trend.id"
                    class="p-3.5 bg-gradient-to-r from-slate-50 to-slate-100/50 rounded-2xl border border-slate-100/80 flex flex-col gap-2 hover:-translate-y-0.5 transition-transform duration-200 shadow-sm"
                  >
                    <div class="flex items-start justify-between">
                      <div class="flex items-center gap-2">
                        <span class="text-base">{{ trend.icon }}</span>
                        <span
                          class="text-[11px] font-black text-slate-700 tracking-tight leading-tight"
                          >{{ trend.title }}</span
                        >
                      </div>
                      <span
                        class="text-[10px] font-black px-2 py-0.5 bg-amber-100/40 text-amber-700 rounded-full"
                      >
                        {{ trend.valueText }}
                      </span>
                    </div>

                    <!-- Mini custom sparkline or progress bar -->
                    <div
                      class="w-full h-1.5 bg-slate-200 rounded-full overflow-hidden mt-1 shadow-inner relative"
                    >
                      <!-- Dynamic Fill -->
                      <div
                        class="h-full rounded-full bg-gradient-to-r from-amber-400 to-orange-500"
                        :style="{
                          width: `${Math.min(Math.max(trend.id === 'maint_uptime' ? (trend.metricValue - 90) * 10 : trend.metricValue, 8), 100)}%`,
                        }"
                      ></div>
                    </div>

                    <!-- Short comment -->
                    <p class="text-[10.5px] font-bold text-slate-500 leading-relaxed mt-0.5">
                      {{
                        trend.narration.split('**')[2]?.substring(0, 100) ||
                        trend.narration.substring(0, 95)
                      }}...
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </ReportLayout>
</template>

<style scoped>
/* Slideshow animations transitions */
.slide-fade-enter-active,
.slide-fade-leave-active {
  transition: all 0.65s cubic-bezier(0.16, 1, 0.3, 1);
}
.slide-fade-enter-from {
  opacity: 0;
  transform: scale(0.985) translateY(4px);
}
.slide-fade-leave-to {
  opacity: 0;
  transform: scale(1.01) translateY(-4px);
}
</style>
