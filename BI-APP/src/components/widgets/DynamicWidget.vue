<template>
  <div class="bg-white/80 backdrop-blur-md rounded-2xl p-5 shadow-sm border border-slate-100 hover:shadow-md hover:border-sky-200 hover:-translate-y-1 transition-all duration-300 h-[35vh] min-h-[300px] flex flex-col">
    <div class="flex justify-between items-center mb-4">
      <h3 class="font-bold text-slate-800 text-sm tracking-tight flex items-center gap-2">
        <span class="w-1.5 h-3 bg-sky-500 rounded-full"></span>
        {{ config.name }}
      </h3>
    </div>
    
    <div class="flex-1 relative min-h-0" v-if="!loading && !error">
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

    const res = await api.post(`/dynamic-widgets/data/${props.config.category}/${props.config.id}`)
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

const chartOption = computed(() => {
  if (!data.value || data.value.length === 0) return {}

  const dimensions = props.config.query?.dimensions || []
  const measures = props.config.query?.measures || []
  const type = (props.config.type || 'bar').replace('echarts_', '')
  
  if (type === 'pie') {
    const measureField = `${(measures[0]?.agg || 'COUNT').toUpperCase()}_${measures[0]?.field}`
    const dimField = dimensions[0] || 'Name'
    const chartData = data.value.map(row => ({
      name: safeGet(row, dimField, 'Uncategorized'),
      value: safeGet(row, measureField, 0)
    }))

    return {
      tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
      legend: { bottom: 0, textStyle: { color: '#64748b', fontSize: 10 } },
      series: [
        {
          type: 'pie',
          radius: ['40%', '70%'],
          avoidLabelOverlap: false,
          itemStyle: {
            borderRadius: 8,
            borderColor: '#fff',
            borderWidth: 2
          },
          label: { show: false },
          emphasis: {
            label: {
              show: true,
              fontSize: 12,
              fontWeight: 'bold'
            }
          },
          data: chartData
        }
      ]
    }
  }

  const dimField = dimensions[0] || ''
  const xAxisData = data.value.map(row => safeGet(row, dimField, 'N/A'))
  
  const series = measures.map(m => {
    const aggField = `${(m.agg || 'SUM').toUpperCase()}_${m.field}`
    return {
      name: m.field.replace('_', ' ').toUpperCase(),
      type: type,
      smooth: true,
      showSymbol: type === 'line',
      barMaxWidth: 30,
      itemStyle: {
        borderRadius: type === 'bar' ? [4, 4, 0, 0] : 0
      },
      data: data.value.map(row => safeGet(row, aggField, 0))
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
      axisLine: { lineStyle: { color: '#e2e8f0' } }
    },
    yAxis: {
      type: 'value',
      axisLabel: { color: '#64748b', fontSize: 10 },
      splitLine: { lineStyle: { color: '#f1f5f9' } }
    },
    series: series
  }
})
</script>
