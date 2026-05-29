<script setup>
import { ref, onMounted } from 'vue'
import apiClient from '@/services/api.js'

// Import ECharts core & required components for tree-shaking
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { PieChart } from 'echarts/charts'
import { LegendComponent, TooltipComponent } from 'echarts/components'

use([CanvasRenderer, PieChart, LegendComponent, TooltipComponent])

// Component States
const isLoading = ref(true)
const error = ref(null)

// KPI Stat Bindings
const openTickets = ref(0)
const criticalAlerts = ref(0)

// Curated harmonious colors for uptime indicators
const colors = ['#10b981', '#3b82f6', '#f59e0b', '#8b5cf6', '#06b6d4']

// ECharts Options Ref
const chartOption = ref({
  textStyle: {
    fontFamily: "'Inter', sans-serif"
  },
  color: colors,
  tooltip: {
    trigger: 'item',
    backgroundColor: 'rgba(15, 23, 42, 0.95)',
    borderWidth: 0,
    padding: [10, 14],
    textStyle: {
      color: '#fff',
      fontSize: 12
    },
    formatter: (params) => {
      return `
        <div style="font-weight: bold; margin-bottom: 4px;">${params.name}</div>
        <div style="display: flex; align-items: center; gap: 6px;">
          <span style="display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: ${params.color};"></span>
          <span style="color: #cbd5e1;">Uptime:</span>
          <span style="font-weight: 800; color: #10b981;">${params.value}%</span>
        </div>
      `
    }
  },
  legend: {
    orient: 'horizontal',
    bottom: '2%',
    left: 'center',
    icon: 'circle',
    itemGap: 8,
    itemWidth: 8,
    itemHeight: 8,
    textStyle: {
      color: '#475569',
      fontSize: 9,
      fontWeight: 500
    }
  },
  series: [
    {
      name: 'Equipment Uptime',
      type: 'pie',
      radius: ['55%', '78%'],
      center: ['50%', '45%'],
      avoidLabelOverlap: true,
      itemStyle: {
        borderRadius: 4,
        borderColor: '#fff',
        borderWidth: 2
      },
      label: {
        show: false
      },
      emphasis: {
        label: {
          show: false
        },
        itemStyle: {
          shadowBlur: 6,
          shadowOffsetX: 0,
          shadowColor: 'rgba(0, 0, 0, 0.05)'
        }
      },
      data: [] // Populated dynamically
    }
  ]
})

// Fetch Status from Secure API
const fetchData = async () => {
  isLoading.value = true
  error.value = null
  try {
    const response = await apiClient.get('/v1/dashboard/maintenance/status')
    const { data } = response
    
    // Bind stats card details
    openTickets.value = data.openTickets ?? 0
    criticalAlerts.value = data.criticalAlerts ?? 0
    
    // Map ECharts values for equipmentUptimePercent
    chartOption.value.series[0].data = data.equipmentUptimePercent || []
    
  } catch (err) {
    console.error('Error fetching maintenance status:', err)
    error.value = err.response?.data?.message || 'Unable to load maintenance dashboard data. Please verify your authentication or try again.'
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  fetchData()
})
</script>

<template>
  <div class="relative bg-white/90 backdrop-blur-md border border-slate-100 rounded-2xl shadow-sm p-4 overflow-hidden flex flex-col h-[330px] hover:shadow-md hover:border-slate-200/80 transition-all duration-300">
    
    <!-- Header Block -->
    <div class="flex items-center justify-between mb-2.5 shrink-0">
      <div>
        <h4 class="text-xs sm:text-[13px] font-bold text-slate-800 tracking-tight">Maintenance Status</h4>
        <p class="text-[9px] sm:text-[10px] font-medium text-slate-400 mt-0.5">Active tickets and uptime reliability</p>
      </div>
      
      <!-- System Normal Status Badge -->
      <span class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[9px] font-bold bg-sky-50 text-sky-700 shrink-0">
        <span class="w-1 h-1 rounded-full bg-sky-500"></span>
        Systems Online
      </span>
    </div>

    <!-- Loading State -->
    <div v-if="isLoading" class="flex-1 flex flex-col items-center justify-center py-4 min-h-0 w-full">
      <div class="w-8 h-8 border-3 border-slate-100 border-t-amber-500 rounded-full animate-spin"></div>
      <p class="text-amber-600/90 font-semibold animate-pulse text-xs mt-3 tracking-wide">Loading System Status...</p>
    </div>

    <!-- Error State -->
    <div v-else-if="error" class="flex-1 flex flex-col items-center justify-center py-4 px-3 text-center min-h-0 w-full">
      <div class="p-2 bg-rose-50 rounded-full text-rose-500 mb-2">
        <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
      </div>
      <h4 class="text-slate-800 text-xs font-bold tracking-tight">Failed to Load Maintenance</h4>
      <p class="text-slate-500 text-[10px] mt-0.5 max-w-[200px] leading-relaxed truncate">{{ error }}</p>
      <button @click="fetchData" class="mt-2 px-3 py-1 bg-amber-500 hover:bg-amber-600 text-white text-[10px] font-bold rounded shadow transition-all duration-200">
        Try Again
      </button>
    </div>

    <!-- Main Content -->
    <div v-else class="flex-1 min-h-0 flex flex-row gap-3 items-center">
      
      <!-- Stat Cards Column -->
      <div class="flex flex-col gap-2 w-[125px] sm:w-[135px] shrink-0">
        
        <!-- Open Tickets Card -->
        <div class="p-2 sm:p-2.5 rounded-xl bg-gradient-to-br from-slate-50 to-slate-100/50 border border-slate-100/60 flex flex-col justify-between h-[105px]">
          <span class="text-[9px] font-bold text-slate-500 uppercase tracking-wider">Open Tickets</span>
          <div class="flex items-baseline gap-1 mt-1">
            <span class="text-2xl font-black text-slate-800 tracking-tight leading-none">{{ openTickets }}</span>
            <span class="text-[8px] sm:text-[9px] font-bold text-amber-500">active</span>
          </div>
        </div>

        <!-- Critical Alerts Card -->
        <div class="p-2 sm:p-2.5 rounded-xl bg-gradient-to-br from-slate-50 to-slate-100/50 border border-slate-100/60 flex flex-col justify-between h-[105px]" :class="{'from-rose-50/40 border-rose-100': criticalAlerts > 0}">
          <span class="text-[9px] font-bold text-slate-500 uppercase tracking-wider">Critical Alerts</span>
          <div class="flex items-baseline gap-1 mt-1">
            <span class="text-2xl font-black tracking-tight leading-none" :class="criticalAlerts > 0 ? 'text-rose-600' : 'text-slate-800'">{{ criticalAlerts }}</span>
            <span class="text-[8px] sm:text-[9px] font-bold" :class="criticalAlerts > 0 ? 'text-rose-500 animate-pulse' : 'text-slate-500'">warnings</span>
          </div>
        </div>

      </div>

      <!-- Doughnut Chart Column -->
      <div class="flex-1 h-full min-h-0 relative">
        <v-chart class="w-full h-full" :option="chartOption" autoresize />
      </div>

    </div>
  </div>
</template>

<style scoped>
.v-chart {
  width: 100%;
  height: 100%;
}
</style>
