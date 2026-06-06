<template>
  <div class="bg-white rounded-xl p-4 shadow-sm h-[400px] flex flex-col">
    <div class="flex justify-between items-center mb-4">
      <h3 class="font-bold text-gray-800">{{ config.name }}</h3>
    </div>
    <div class="flex-1 relative" v-if="!loading && !error">
      <v-chart class="w-full h-full" :option="chartOption" autoresize />
    </div>
    <div v-else-if="loading" class="flex-1 flex items-center justify-center">
      <span class="text-gray-400">Loading...</span>
    </div>
    <div v-else class="flex-1 flex items-center justify-center">
      <span class="text-red-400">{{ error }}</span>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import api from '@/services/api' // assuming standard path
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
    // Assuming authStore has companyId
    const companyId = authStore.user?.company_id || 'COMP001'
    const res = await api.post(`/dynamic-widgets/data/${props.config.category}/${props.config.id}`, {}, {
      params: { companyId }
    })
    data.value = res.data
  } catch (err) {
    console.error(err)
    error.value = 'Failed to load widget data'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchData()
})

const chartOption = computed(() => {
  if (!data.value || data.value.length === 0) return {}

  const dimensions = props.config.query?.dimensions || []
  const measures = props.config.query?.measures || []
  
  const dimField = dimensions[0] || ''
  const xAxisData = data.value.map(row => row[dimField])
  
  const series = measures.map(m => {
    const aggField = `${(m.agg || 'SUM').toUpperCase()}_${m.field}`
    return {
      name: m.field,
      type: (props.config.type || 'bar').replace('echarts_', ''),
      data: data.value.map(row => row[aggField])
    }
  })

  return {
    tooltip: { trigger: 'axis' },
    legend: { bottom: 0 },
    grid: { left: '3%', right: '4%', bottom: '10%', containLabel: true },
    xAxis: {
      type: 'category',
      data: xAxisData
    },
    yAxis: {
      type: 'value'
    },
    series: series
  }
})
</script>
