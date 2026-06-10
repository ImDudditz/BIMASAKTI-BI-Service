<template>
  <!-- KPI Cards Grid Layout (spans full width) -->
  <div v-if="props.config.type === 'kpi_cards'" class="col-span-full grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6 w-full">
    <div v-if="loading" class="col-span-full bg-white/80 backdrop-blur-md rounded-2xl p-8 shadow-sm border border-slate-100 flex flex-col items-center justify-center gap-2 h-[22vh] min-h-[160px]">
      <div class="w-8 h-8 border-3 border-sky-100 border-t-sky-500 rounded-full animate-spin"></div>
      <span class="text-xs font-semibold text-slate-400">Loading KPI metrics...</span>
    </div>
    
    <div v-else-if="error" class="col-span-full bg-white/80 backdrop-blur-md rounded-2xl p-8 shadow-sm border border-slate-100 flex flex-col items-center justify-center text-center h-[22vh] min-h-[160px]">
      <span class="text-xs font-bold text-rose-500">KPI Sync Failed</span>
      <span class="text-[10px] text-slate-400 mt-1">{{ error }}</span>
    </div>
    
    <div v-else v-for="card in data" :key="card.id" 
          class="bg-white/80 backdrop-blur-md rounded-2xl p-6 shadow-sm border border-slate-100 hover:shadow-md hover:border-sky-200 hover:-translate-y-1 transition-all duration-300 flex items-center justify-between h-[22vh] min-h-[160px] w-full">
      <div class="flex flex-col gap-1.5 min-w-0 flex-1">
        <span class="text-[11px] font-bold text-slate-500 tracking-wider uppercase truncate">{{ card.title }}</span>
        
        <!-- Current Row -->
        <div class="flex flex-col">
          <span class="text-[10px] font-medium text-slate-400">Current</span>
          <span :class="card.id === 'net_income' ? (card.current_value >= 0 ? 'text-emerald-600' : 'text-rose-600') : 'text-slate-800'" class="text-sm xs:text-base font-black tracking-tight flex items-center gap-1.5">
            {{ formatValue(card.current_value, card.format) }}
            <!-- Current Growth Trend Indicator -->
            <span v-if="card.id === 'growth'" :class="card.current_value >= 0 ? 'text-emerald-600 bg-emerald-50' : 'text-rose-600 bg-rose-50'" class="text-[9px] font-black px-1.5 py-0.5 rounded-md flex items-center gap-0.5 shrink-0">
              <svg v-if="card.current_value >= 0" xmlns="http://www.w3.org/2000/svg" class="w-2.5 h-2.5" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M12 7a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0V8.414l-4.293 4.293a1 1 0 01-1.414 0L7 9.414l-4.293 4.293a1 1 0 01-1.414-1.414l5-5a1 1 0 011.414 0L11 10.586 14.586 7H12z" clip-rule="evenodd" />
              </svg>
              <svg v-else xmlns="http://www.w3.org/2000/svg" class="w-2.5 h-2.5" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M12 13a1 1 0 100 2h5a1 1 0 001-1v-5a1 1 0 10-2 0v2.586l-4.293-4.293a1 1 0 00-1.414 0L7 10.586 2.707 6.293a1 1 0 00-1.414 1.414l5 5a1 1 0 001.414 0L11 9.414 14.586 13H12z" clip-rule="evenodd" />
              </svg>
            </span>
          </span>
        </div>

        <!-- YTD Row -->
        <div class="flex flex-col border-t border-slate-100 pt-1 mt-0.5">
          <span class="text-[10px] font-medium text-slate-400">YTD</span>
          <span :class="card.id === 'net_income' ? (card.ytd_value >= 0 ? 'text-emerald-600' : 'text-rose-600') : 'text-slate-800'" class="text-sm xs:text-base font-black tracking-tight flex items-center gap-1.5">
            {{ formatValue(card.ytd_value, card.format) }}
            <!-- YTD Growth Trend Indicator -->
            <span v-if="card.id === 'growth'" :class="card.ytd_value >= 0 ? 'text-emerald-600 bg-emerald-50' : 'text-rose-600 bg-rose-50'" class="text-[9px] font-black px-1.5 py-0.5 rounded-md flex items-center gap-0.5 shrink-0">
              <svg v-if="card.ytd_value >= 0" xmlns="http://www.w3.org/2000/svg" class="w-2.5 h-2.5" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M12 7a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0V8.414l-4.293 4.293a1 1 0 01-1.414 0L7 9.414l-4.293 4.293a1 1 0 01-1.414-1.414l5-5a1 1 0 011.414 0L11 10.586 14.586 7H12z" clip-rule="evenodd" />
              </svg>
              <svg v-else xmlns="http://www.w3.org/2000/svg" class="w-2.5 h-2.5" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M12 13a1 1 0 100 2h5a1 1 0 001-1v-5a1 1 0 10-2 0v2.586l-4.293-4.293a1 1 0 00-1.414 0L7 10.586 2.707 6.293a1 1 0 00-1.414 1.414l5 5a1 1 0 001.414 0L11 9.414 14.586 13H12z" clip-rule="evenodd" />
              </svg>
            </span>
          </span>
        </div>
      </div>
      
      <!-- Icon Container -->
      <div :class="getIconBgClass(card)" class="w-14 h-14 rounded-2xl flex items-center justify-center shadow-inner border transition-colors duration-300 shrink-0">
        <svg v-if="card.id === 'revenue'" xmlns="http://www.w3.org/2000/svg" class="w-7 h-7 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
        </svg>
        <svg v-else-if="card.id === 'expenditure'" xmlns="http://www.w3.org/2000/svg" class="w-7 h-7 text-rose-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M13 17h8m0 0v-8m0 8l-8-8-4 4-6-6" />
        </svg>
        <svg v-else-if="card.id === 'net_income'" xmlns="http://www.w3.org/2000/svg" :class="card.current_value >= 0 ? 'text-emerald-500' : 'text-rose-500'" class="w-7 h-7" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <svg v-else-if="card.id === 'growth'" xmlns="http://www.w3.org/2000/svg" class="w-7 h-7 text-indigo-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M11 3.055A9.003 9.003 0 1020.945 13H11V3.055z" />
          <path stroke-linecap="round" stroke-linejoin="round" d="M20.488 9H15V3.512A9.025 9.025 0 0120.488 9z" />
        </svg>
      </div>
    </div>
  </div>

  <!-- Standard Chart Card Layout -->
  <div v-else :class="config.type === 'echarts_pie' ? 'p-0 overflow-hidden border-slate-300 h-[42vh] min-h-[380px]' : 'p-5 border-slate-100 h-[35vh] min-h-[300px]'" class="bg-white/80 backdrop-blur-md rounded-2xl shadow-sm border hover:shadow-md hover:border-sky-200 hover:-translate-y-1 transition-all duration-300 flex flex-col">
    <!-- Pie Chart Header -->
    <div v-if="config.type === 'echarts_pie'" class="bg-[#98c9f6] py-3.5 px-4 border-b border-slate-300 text-center w-full shrink-0">
      <h3 class="font-bold text-slate-800 text-xl tracking-tight leading-normal py-1">
        {{ config.name }}
      </h3>
    </div>
    
    <!-- Standard Chart Header -->
    <div v-else class="flex justify-between items-center mb-4 shrink-0">
      <h3 class="font-bold text-slate-800 text-sm tracking-tight flex items-center gap-2">
        <span class="w-1.5 h-3 bg-sky-500 rounded-full"></span>
        {{ config.name }}
      </h3>
    </div>
    
    <div :class="config.type === 'echarts_pie' ? 'p-4 flex-1 relative min-h-0' : 'flex-1 relative min-h-0'" v-if="!loading && !error">
      <v-chart class="w-full h-full" :option="chartOption" autoresize />
    </div>
    
    <div v-else-if="loading" class="flex-1 flex flex-col items-center justify-center gap-2">
      <div class="w-8 h-8 border-3 border-sky-100 border-t-sky-500 rounded-full animate-spin"></div>
      <span class="text-xs font-semibold text-slate-400">Syncing dataset...</span>
    </div>
    
    <div v-else class="flex-1 flex flex-col items-center justify-center text-center p-4">
      <div class="p-2 bg-rose-50 rounded-full text-rose-500 mb-2">
        <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
      </div>
      <span class="text-xs font-bold text-rose-500">Query Failed</span>
      <span class="text-[10px] text-slate-400 mt-1 max-w-[200px] leading-relaxed">{{ error }}</span>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed, watch } from 'vue'

/**
 * Safely access a property from a plain data row using a server-provided key.
 * - Rejects keys that are not own properties (blocks prototype chain access).
 * - Returns `fallback` when key is absent or invalid.
 */
const SAFE_KEY_RE = /^[A-Za-z0-9_]+$/
function safeGet(row, key, fallback = null) {
  if (typeof key !== 'string' || !SAFE_KEY_RE.test(key)) return fallback
  if (!Object.prototype.hasOwnProperty.call(row, key)) return fallback
  return row[key] ?? fallback
}

import { useAuthStore } from '@/stores/auth'
import { useReportFilterStore } from '@/stores/reportFilters'
import api from '@/services/api'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { BarChart, LineChart, PieChart } from 'echarts/charts'
import {
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent
} from 'echarts/components'
import VChart from 'vue-echarts'

use([
  CanvasRenderer,
  BarChart,
  LineChart,
  PieChart,
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent
])

const props = defineProps({
  config: {
    type: Object,
    required: true
  }
})

const authStore = useAuthStore()
const filterStore = useReportFilterStore()
const data = ref([])
const loading = ref(false)
const error = ref('')

const fetchData = async () => {
  loading.value = true
  error.value = ''
  try {
    const username = authStore.user?.username
    if (!username) {
      error.value = 'Not authenticated'
      return
    }

    const res = await api.post(`/dynamic-widgets/data/${props.config.category}/${props.config.id}`, {
      year: filterStore.selectedYear,
      period: filterStore.selectedPeriod
    })
    data.value = res.data
  } catch (err) {
    console.error('[DynamicWidget] Query Error:', err)
    error.value = err.response?.data?.detail || 'Database link error.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchData()
})

// Refetch if widget config changes
watch(() => props.config, () => {
  fetchData()
}, { deep: true })

// Refetch data when dashboard filters change
watch([() => filterStore.selectedYear, () => filterStore.selectedPeriod], () => {
  fetchData()
})

const getIconBgClass = (card) => {
  switch (card.id) {
    case 'revenue': return 'bg-emerald-50 border-emerald-100'
    case 'expenditure': return 'bg-rose-50 border-rose-100'
    case 'net_income': return card.current_value >= 0 ? 'bg-emerald-50 border-emerald-100' : 'bg-rose-50 border-rose-100'
    case 'growth': return 'bg-indigo-50 border-indigo-100'
    default: return 'bg-slate-50 border-slate-100'
  }
}

const formatValue = (value, format) => {
  if (value === null || value === undefined) return 'N/A'
  if (format === 'currency') {
    const absVal = Math.abs(value)
    const sign = value < 0 ? '-' : ''
    const formatted = absVal.toLocaleString('id-ID')
    return sign + 'Rp ' + formatted
  }
  if (format === 'percentage') {
    return (value > 0 ? '+' : '') + value + '%'
  }
  return value.toString()
}

const monthNames = {
  '01': 'January', '02': 'February', '03': 'March', '04': 'April',
  '05': 'May', '06': 'June', '07': 'July', '08': 'August',
  '09': 'September', '10': 'October', '11': 'November', '12': 'December',
}

const formatChartValue = (value) => {
  if (value === null || value === undefined) return '0'
  const absVal = Math.abs(value)
  const sign = value < 0 ? '-' : ''
  let formatted = ''
  if (absVal >= 1e12) {
    formatted = (absVal / 1e12).toFixed(2) + 'T'
  } else if (absVal >= 1e9) {
    formatted = (absVal / 1e9).toFixed(2) + 'B'
  } else if (absVal >= 1e6) {
    formatted = (absVal / 1e6).toFixed(2) + 'M'
  } else if (absVal >= 1e3) {
    formatted = (absVal / 1e3).toFixed(2) + 'K'
  } else {
    formatted = absVal.toLocaleString('id-ID')
  }
  return sign + formatted
}

const chartOption = computed(() => {
  if (!data.value || data.value.length === 0) return {}

  const dimensions = props.config.query?.dimensions || []
  const measures = props.config.query?.measures || []
  const type = (props.config.type || 'bar').replace('echarts_', '')
  
  const isFinancial = props.config.category === 'Financials'
  const prefix = isFinancial ? 'Rp ' : ''
  
  if (props.config.id === 'operating_cash_flow') {
    const periodField = dimensions[0] || 'period'
    const categoryField = dimensions[1] || 'sub_grup_cash_flow'
    const measureField = `SUM_ptd_amount`

    const periods = Array.from(new Set(data.value.map(row => safeGet(row, periodField))))
      .filter(p => p !== null && p !== undefined)
      .sort()

    const xAxisData = periods.map(val => {
      let cleanVal = String(val).trim()
      if (cleanVal.includes('-')) {
        cleanVal = cleanVal.split('-')[1]
      }
      const strVal = cleanVal.padStart(2, '0')
      return monthNames[strVal] || val
    })

    const incomeData = []
    const expenseData = []

    periods.forEach(p => {
      const rowsForPeriod = data.value.filter(row => safeGet(row, periodField) === p)
      const incRow = rowsForPeriod.find(row => {
        const cat = String(safeGet(row, categoryField, '')).toUpperCase()
        return cat.includes('IO') || cat.includes('OI') || cat.includes('INFLOW')
      })
      const expRow = rowsForPeriod.find(row => {
        const cat = String(safeGet(row, categoryField, '')).toUpperCase()
        return cat.includes('OO') || cat.includes('OUTFLOW')
      })

      incomeData.push(incRow ? safeGet(incRow, measureField, 0) : 0)
      expenseData.push(expRow ? safeGet(expRow, measureField, 0) : 0)
    })

    const series = [
      {
        name: 'Income',
        type: 'bar',
        barMaxWidth: 30,
        itemStyle: {
          borderRadius: [4, 4, 0, 0],
          color: '#10b981'
        },
        label: {
          show: true,
          position: 'top',
          formatter: (params) => formatChartValue(params.value),
          fontSize: 9,
          color: '#64748b'
        },
        data: incomeData,
        tooltip: {
          valueFormatter: (val) => prefix + formatChartValue(val)
        }
      },
      {
        name: 'Expense',
        type: 'bar',
        barMaxWidth: 30,
        itemStyle: {
          borderRadius: [4, 4, 0, 0],
          color: '#ef4444'
        },
        label: {
          show: true,
          position: 'top',
          formatter: (params) => formatChartValue(params.value),
          fontSize: 9,
          color: '#64748b'
        },
        data: expenseData,
        tooltip: {
          valueFormatter: (val) => prefix + formatChartValue(val)
        }
      }
    ]

    return {
      color: ['#10b981', '#ef4444'],
      tooltip: { 
        trigger: 'axis',
        backgroundColor: 'rgba(255, 255, 255, 0.95)',
        borderColor: '#e2e8f0',
        borderWidth: 1,
        textStyle: { color: '#1e293b', fontSize: 11 }
      },
      legend: { 
        bottom: 0, 
        textStyle: { color: '#64748b', fontSize: 10 } 
      },
      grid: { 
        left: '3%', 
        right: '4%', 
        bottom: '12%', 
        top: '10%',
        containLabel: true 
      },
      xAxis: {
        type: 'category',
        data: xAxisData,
        axisLabel: { color: '#64748b', fontSize: 10, rotate: xAxisData.length > 5 ? 30 : 0 },
        axisLine: { lineStyle: { color: '#e2e8f0' } },
        splitLine: {
          show: true,
          lineStyle: { color: '#f1f5f9' }
        }
      },
      yAxis: {
        type: 'value',
        axisLabel: { 
          color: '#64748b', 
          fontSize: 10,
          formatter: (val) => prefix + formatChartValue(val)
        },
        splitLine: { lineStyle: { color: '#f1f5f9' } }
      },
      series: series
    }
  }

  if (type === 'pie') {
    const measureField = `${(measures[0]?.agg || 'COUNT').toUpperCase()}_${measures[0]?.field}`
    const dimField = dimensions[0] || 'Name'
    const isPeriodDimension = dimField.toLowerCase().includes('period') || dimField.toLowerCase().includes('month')
    
    const rawChartData = data.value.map(row => {
      let dimName = safeGet(row, dimField, 'Uncategorized')
      if (isPeriodDimension && dimName !== 'Uncategorized') {
        let cleanVal = String(dimName).trim()
        if (cleanVal.includes('-')) {
          cleanVal = cleanVal.split('-')[1]
        }
        const strVal = cleanVal.padStart(2, '0')
        dimName = monthNames[strVal] || dimName
      }
      return {
        name: String(dimName).trim(),
        value: safeGet(row, measureField, 0)
      }
    })

    // Sort descending
    rawChartData.sort((a, b) => b.value - a.value)

    let chartData = rawChartData
    if (rawChartData.length > 10) {
      const top9 = rawChartData.slice(0, 9)
      const others = rawChartData.slice(9)
      const othersSum = others.reduce((sum, item) => sum + item.value, 0)
      top9.push({
        name: 'Others',
        value: othersSum
      })
      chartData = top9
    }

    return {
      color: ['#118DFF', '#12239E', '#E66C37', '#6B007B', '#E044A7', '#744EC2', '#D9B300', '#D64550', '#1AAB40', '#15C6F4'],
      tooltip: { 
        trigger: 'item', 
        formatter: (params) => {
          return `${params.name}: ${prefix}${formatChartValue(params.value)} (${params.percent}%)`
        }
      },
      legend: { 
        orient: 'vertical',
        right: 15,
        top: 'center',
        type: 'scroll',
        icon: 'circle',
        itemWidth: 10,
        itemHeight: 10,
        itemGap: 12,
        textStyle: { color: '#334155', fontSize: 11, fontFamily: 'system-ui, -apple-system, sans-serif' } 
      },
      series: [
        {
          type: 'pie',
          radius: '60%',
          center: ['30%', '50%'],
          avoidLabelOverlap: true,
          itemStyle: {
            borderColor: '#fff',
            borderWidth: 1.5
          },
          label: { 
            show: true,
            position: 'outside',
            formatter: (params) => {
              const formattedVal = formatChartValue(params.value)
                .replace('B', 'bn')
                .replace('M', 'm')
                .replace('K', 'k')
                .replace('T', 't')
              return `${formattedVal} (${params.percent.toFixed(2)}%)`
            },
            fontSize: 10,
            color: '#334155',
            fontFamily: 'system-ui, -apple-system, sans-serif'
          },
          labelLine: {
            show: true,
            lineStyle: {
              color: '#94a3b8'
            }
          },
          emphasis: {
            label: {
              show: true,
              fontSize: 11,
              fontWeight: 'bold'
            }
          },
          data: chartData
        }
      ]
    }
  }

  const dimField = dimensions[0] || ''
  const isPeriodDimension = dimField.toLowerCase().includes('period') || dimField.toLowerCase().includes('month')
  
  const xAxisData = data.value.map(row => {
    const val = safeGet(row, dimField, 'N/A')
    if (isPeriodDimension && val !== 'N/A') {
      let cleanVal = String(val).trim()
      if (cleanVal.includes('-')) {
        cleanVal = cleanVal.split('-')[1]
      }
      const strVal = cleanVal.padStart(2, '0')
      return monthNames[strVal] || val
    }
    return val
  })
  
  const colors = ['#0ea5e9', '#10b981', '#f59e0b', '#6366f1', '#ec4899']
  const series = measures.map((m, index) => {
    const aggField = `${(m.agg || 'SUM').toUpperCase()}_${m.field}`
    const isLine = type === 'line'
    const seriesColor = colors[index % colors.length]
    return {
      name: m.field.toLowerCase().includes('budget')
        ? 'Budget'
        : m.field.toLowerCase().includes('amount') || m.field.toLowerCase().includes('balance')
          ? 'Actual'
          : m.field.replace('_', ' ').toUpperCase(),
      type: type,
      smooth: true,
      showSymbol: true,
      symbol: isLine ? 'circle' : undefined,
      symbolSize: isLine ? 8 : undefined,
      barMaxWidth: 30,
      itemStyle: {
        borderRadius: type === 'bar' ? [4, 4, 0, 0] : 0,
        shadowColor: isLine ? 'rgba(0,0,0,0.15)' : undefined,
        shadowBlur: isLine ? 5 : undefined
      },
      lineStyle: isLine ? {
        type: 'solid',
        width: 3,
        shadowColor: 'rgba(0, 0, 0, 0.15)',
        shadowBlur: 8,
        shadowOffsetY: 4
      } : undefined,
      areaStyle: isLine ? {
        color: {
          type: 'linear',
          x: 0,
          y: 0,
          x2: 0,
          y2: 1,
          colorStops: [
            { offset: 0, color: seriesColor + '4D' },
            { offset: 1, color: seriesColor + '00' }
          ]
        }
      } : undefined,
      label: {
        show: true,
        position: 'top',
        formatter: (params) => formatChartValue(params.value),
        fontSize: 9,
        color: '#64748b'
      },
      data: data.value.map((row, idx) => {
        const val = safeGet(row, aggField, 0)
        if (isLine && idx === data.value.length - 1) {
          return {
            value: val,
            symbol: 'arrow',
            symbolSize: 12,
            itemStyle: {
              color: seriesColor
            }
          }
        }
        return val
      }),
      tooltip: {
        valueFormatter: (val) => prefix + formatChartValue(val)
      }
    }
  })

  return {
    color: ['#0ea5e9', '#10b981', '#f59e0b', '#6366f1', '#ec4899'],
    tooltip: { 
      trigger: 'axis',
      backgroundColor: 'rgba(255, 255, 255, 0.95)',
      borderColor: '#e2e8f0',
      borderWidth: 1,
      textStyle: { color: '#1e293b', fontSize: 11 }
    },
    legend: { 
      bottom: 0, 
      textStyle: { color: '#64748b', fontSize: 10 } 
    },
    grid: { 
      left: '3%', 
      right: '4%', 
      bottom: '12%', 
      top: '10%',
      containLabel: true 
    },
    xAxis: {
      type: 'category',
      data: xAxisData,
      axisLabel: { color: '#64748b', fontSize: 10, rotate: xAxisData.length > 5 ? 30 : 0 },
      axisLine: { lineStyle: { color: '#e2e8f0' } },
      splitLine: {
        show: true,
        lineStyle: { color: '#f1f5f9' }
      }
    },
    yAxis: {
      type: 'value',
      axisLabel: { 
        color: '#64748b', 
        fontSize: 10,
        formatter: (val) => prefix + formatChartValue(val)
      },
      splitLine: { lineStyle: { color: '#f1f5f9' } }
    },
    series: series
  }
})
</script>
