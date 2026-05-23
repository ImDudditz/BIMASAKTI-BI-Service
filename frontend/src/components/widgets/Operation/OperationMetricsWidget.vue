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
    top: '10%',
    left: '2%',
    right: '2%',
    bottom: '5%',
    containLabel: true
  },
  tooltip: {
    trigger: 'axis',
    backgroundColor: 'rgba(15, 23, 42, 0.95)',
    borderWidth: 0,
    padding: [10, 14],
    textStyle: {
      color: '#fff',
      fontSize: 12
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
      fontSize: 11,
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
      fontSize: 11,
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
  <div class="relative flex flex-col bg-white/90 backdrop-blur-md border border-slate-100 rounded-2xl shadow-sm p-6 overflow-hidden transition-all duration-300 hover:shadow-md hover:border-slate-200/80">
    
    <!-- Header Block -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h3 class="text-lg font-bold text-slate-800 tracking-tight">Operation Metrics</h3>
        <p class="text-xs font-medium text-slate-500 mt-0.5">Real-time tenant activity and daily foot traffic statistics</p>
      </div>
      
      <!-- Live Indicator Badge -->
      <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700">
        <span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
        Live Metrics
      </span>
    </div>

    <!-- Loading State -->
    <div v-if="isLoading" class="flex flex-col items-center justify-center py-20 min-h-[380px]">
      <div class="w-10 h-10 border-4 border-slate-100 border-t-indigo-600 rounded-full animate-spin"></div>
      <p class="text-indigo-600/90 font-semibold animate-pulse text-sm mt-4 tracking-wide">Loading Operation Metrics...</p>
    </div>

    <!-- Error State -->
    <div v-else-if="error" class="flex flex-col items-center justify-center py-16 px-4 text-center min-h-[380px]">
      <div class="p-3 bg-rose-50 rounded-full text-rose-500 mb-4">
        <svg xmlns="http://www.w3.org/2000/svg" class="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
      </div>
      <h4 class="text-slate-800 font-bold tracking-tight">Failed to Load Metrics</h4>
      <p class="text-slate-500 text-xs mt-1 max-w-sm leading-relaxed">{{ error }}</p>
      <button @click="fetchData" class="mt-4 px-4 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold rounded-lg shadow transition-all duration-200">
        Try Again
      </button>
    </div>

    <!-- Main Content -->
    <div v-else class="flex flex-col gap-6">
      
      <!-- Stat Cards Grid -->
      <div class="grid grid-cols-2 gap-4">
        
        <!-- Active Tenants Card -->
        <div class="p-4 rounded-xl bg-gradient-to-br from-slate-50 to-slate-100/50 border border-slate-100/60 flex flex-col justify-between">
          <span class="text-xs font-bold text-slate-500 uppercase tracking-wider">Active Tenants</span>
          <div class="flex items-baseline gap-2 mt-2">
            <span class="text-3xl font-extrabold text-slate-800 tracking-tight">{{ activeTenants }}</span>
            <span class="text-xs font-semibold text-indigo-600">units</span>
          </div>
        </div>

        <!-- Occupancy Rate Card -->
        <div class="p-4 rounded-xl bg-gradient-to-br from-slate-50 to-slate-100/50 border border-slate-100/60 flex flex-col justify-between">
          <span class="text-xs font-bold text-slate-500 uppercase tracking-wider">Occupancy Rate</span>
          <div class="flex items-baseline gap-1 mt-2">
            <span class="text-3xl font-extrabold text-slate-800 tracking-tight">{{ occupancyRate }}</span>
            <span class="text-sm font-bold text-slate-700">%</span>
          </div>
        </div>

      </div>

      <!-- Chart Container -->
      <div class="relative w-full h-[280px]">
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
