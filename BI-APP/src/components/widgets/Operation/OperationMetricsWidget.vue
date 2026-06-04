<script setup>
import { ref, onMounted } from 'vue'
import apiClient from '@/services/api.js'

// Import ECharts core & required components for tree-shaking
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import * as echarts from 'echarts/core'

use([CanvasRenderer, BarChart, GridComponent, TooltipComponent])

// Component States
const isLoading = ref(true)
const error = ref(null)

// KPI Stat Bindings
const activeTenants = ref(0)
const occupancyRate = ref(0)

// ECharts Options Ref
const chartOption = ref({
  textStyle: {
    fontFamily: "'Inter', sans-serif"
  },
  grid: {
    top: '12%',
    left: '3%',
    right: '3%',
    bottom: '5%',
    containLabel: true
  },
  tooltip: {
    trigger: 'axis',
    backgroundColor: 'rgba(15, 23, 42, 0.95)',
    borderWidth: 0,
    padding: [8, 12],
    textStyle: {
      color: '#fff',
      fontSize: 11
    },
    axisPointer: {
      type: 'shadow',
      shadowStyle: {
        color: 'rgba(99, 102, 241, 0.05)'
      }
    },
    formatter: (params) => {
      const data = params[0]
      return `
        <div style="font-weight: bold; margin-bottom: 4px;">${data.name}</div>
        <div style="display: flex; align-items: center; gap: 6px;">
          <span style="display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: #6366f1;"></span>
          <span style="color: #cbd5e1;">Foot Traffic:</span>
          <span style="font-weight: 800;">${new Intl.NumberFormat('id-ID').format(data.value)}</span>
        </div>
      `
    }
  },
  xAxis: {
    type: 'category',
    data: [], // Populated dynamically
    axisLine: {
      lineStyle: { color: '#e2e8f0' }
    },
    axisLabel: {
      color: '#64748b',
      fontSize: 9,
      fontWeight: 500
    },
    axisTick: {
      show: false
    }
  },
  yAxis: {
    type: 'value',
    splitLine: {
      lineStyle: {
        color: '#f1f5f9',
        type: 'dashed'
      }
    },
    axisLabel: {
      color: '#64748b',
      fontSize: 9,
      fontWeight: 500,
      formatter: (val) => val >= 1000 ? `${(val / 1000).toFixed(1)}k` : val
    }
  },
  series: [
    {
      name: 'Foot Traffic',
      type: 'bar',
      data: [], // Populated dynamically
      barWidth: '40%',
      itemStyle: {
        borderRadius: [6, 6, 0, 0],
        // Beautiful vibrant color gradient
        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
          { offset: 0, color: '#6366f1' },
          { offset: 1, color: '#4f46e5' }
        ])
      },
      emphasis: {
        itemStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: '#818cf8' },
            { offset: 1, color: '#6366f1' }
          ])
        }
      }
    }
  ]
})

// Fetch Metrics from Secure API
const fetchData = async () => {
  isLoading.value = true
  error.value = null
  try {
    const response = await apiClient.get('/v1/dashboard/operation/metrics')
    const { data } = response
    
    // Bind stats card details
    activeTenants.value = data.activeTenants ?? 0
    occupancyRate.value = data.occupancyRate ?? 0
    
    // Map ECharts values
    const labels = data.dailyFootTraffic.map(item => item.date)
    const values = data.dailyFootTraffic.map(item => item.value)
    
    chartOption.value.xAxis.data = labels
    chartOption.value.series[0].data = values
    
  } catch (err) {
    console.error('Error fetching operational metrics:', err)
    error.value = err.response?.data?.message || 'Unable to load operational dashboard data. Please verify your authentication or try again.'
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
        <h4 class="text-xs sm:text-[13px] font-bold text-slate-800 tracking-tight">Operation Metrics</h4>
        <p class="text-[9px] sm:text-[10px] font-medium text-slate-400 mt-0.5">Active tenants and traffic statistics</p>
      </div>
      
      <!-- Live Indicator Badge -->
      <span class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[9px] font-bold bg-emerald-50 text-emerald-700 shrink-0">
        <span class="w-1 h-1 rounded-full bg-emerald-500 animate-pulse"></span>
        Live Metrics
      </span>
    </div>

    <!-- Loading State -->
    <div v-if="isLoading" class="flex-1 flex flex-col items-center justify-center py-4 min-h-0 w-full">
      <div class="w-8 h-8 border-3 border-slate-100 border-t-indigo-600 rounded-full animate-spin"></div>
      <p class="text-indigo-600/90 font-semibold animate-pulse text-xs mt-3 tracking-wide">Loading Operation Metrics...</p>
    </div>

    <!-- Error State -->
    <div v-else-if="error" class="flex-1 flex flex-col items-center justify-center py-4 px-3 text-center min-h-0 w-full">
      <div class="p-2 bg-rose-50 rounded-full text-rose-500 mb-2">
        <svg xmlns="http://www.w3.org/2000/svg" class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
      </div>
      <h4 class="text-slate-800 text-xs font-bold tracking-tight">Failed to Load Metrics</h4>
      <p class="text-slate-500 text-[10px] mt-0.5 max-w-[200px] leading-relaxed truncate">{{ error }}</p>
      <button @click="fetchData" class="mt-2 px-3 py-1 bg-indigo-600 hover:bg-indigo-700 text-white text-[10px] font-bold rounded shadow transition-all duration-200">
        Try Again
      </button>
    </div>

    <!-- Main Content -->
    <div v-else class="flex-1 min-h-0 flex flex-row gap-3 items-center">
      
      <!-- Stat Cards Column -->
      <div class="flex flex-col gap-2 w-[125px] sm:w-[135px] shrink-0">
        
        <!-- Active Tenants Card -->
        <div class="p-2 sm:p-2.5 rounded-xl bg-gradient-to-br from-slate-50 to-slate-100/50 border border-slate-100/60 flex flex-col justify-between h-[105px]">
          <span class="text-[9px] font-bold text-slate-500 uppercase tracking-wider">Active Tenants</span>
          <div class="flex items-baseline gap-1 mt-1">
            <span class="text-2xl font-black text-slate-800 tracking-tight leading-none">{{ activeTenants }}</span>
            <span class="text-[8px] sm:text-[9px] font-semibold text-indigo-600">units</span>
          </div>
        </div>

        <!-- Occupancy Rate Card -->
        <div class="p-2 sm:p-2.5 rounded-xl bg-gradient-to-br from-slate-50 to-slate-100/50 border border-slate-100/60 flex flex-col justify-between h-[105px]">
          <span class="text-[9px] font-bold text-slate-500 uppercase tracking-wider">Occupancy Rate</span>
          <div class="flex items-baseline gap-1 mt-1">
            <span class="text-2xl font-black text-slate-800 tracking-tight leading-none">{{ occupancyRate }}</span>
            <span class="text-[8px] sm:text-[9px] font-bold text-slate-700">%</span>
          </div>
        </div>

      </div>

      <!-- Chart Column -->
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
