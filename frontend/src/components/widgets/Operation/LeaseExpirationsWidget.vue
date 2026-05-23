<template>
  <div class="relative bg-white/90 backdrop-blur-md border border-slate-100 rounded-2xl shadow-sm p-6 overflow-hidden flex flex-col h-[380px] hover:shadow-md hover:border-slate-200/80 transition-all duration-300">
    
    <div class="flex items-center justify-between mb-4">
      <div>
        <h4 class="text-sm font-bold text-slate-800 tracking-tight">Lease Expirations Timeline</h4>
        <p class="text-[11px] font-medium text-slate-400">Projected contract terminations over the next 6 months</p>
      </div>
    </div>

    <!-- Chart Block -->
    <div class="grow relative min-h-0 w-full">
      <v-chart class="w-full h-full" :option="chartOption" autoresize />
    </div>
  </div>
</template>

<script setup>
import { ref, watch, defineProps } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import * as echarts from 'echarts/core'

use([CanvasRenderer, BarChart, GridComponent, TooltipComponent])

const props = defineProps({
  expirationsData: {
    type: Array,
    required: true,
    default: () => []
  }
})

const chartOption = ref({
  textStyle: { fontFamily: "'Inter', sans-serif" },
  grid: { top: '15%', left: '3%', right: '3%', bottom: '5%', containLabel: true },
  tooltip: {
    trigger: 'axis',
    backgroundColor: 'rgba(15, 23, 42, 0.95)',
    borderWidth: 0,
    padding: [10, 14],
    textStyle: { color: '#fff', fontSize: 12 },
    axisPointer: {
      type: 'shadow',
      shadowStyle: { color: 'rgba(249, 115, 22, 0.04)' }
    },
    formatter: (params) => {
      const data = params[0]
      return `
        <div style="font-weight: bold; margin-bottom: 4px;">${data.name}</div>
        <div style="display: flex; align-items: center; gap: 6px;">
          <span style="display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: #f97316;"></span>
          <span style="color: #cbd5e1;">Expirations:</span>
          <span style="font-weight: 800; color: #fdba74;">${data.value} leases</span>
        </div>
      `
    }
  },
  xAxis: {
    type: 'category',
    data: [],
    axisLine: { lineStyle: { color: '#cbd5e1' } },
    axisLabel: { color: '#64748b', fontSize: 10, fontWeight: 600 }
  },
  yAxis: {
    type: 'value',
    minInterval: 1,
    splitLine: { lineStyle: { color: '#f1f5f9', type: 'dashed' } },
    axisLabel: { color: '#64748b', fontSize: 10 }
  },
  series: [
    {
      name: 'Leases Expiring',
      type: 'bar',
      barWidth: '40%',
      data: [],
      itemStyle: {
        borderRadius: [4, 4, 0, 0],
        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
          { offset: 0, color: '#f97316' },
          { offset: 1, color: '#eab308' }
        ])
      }
    }
  ]
})

// Sync property values reactively
watch(() => props.expirationsData, (newData) => {
  if (newData && newData.length > 0) {
    chartOption.value.xAxis.data = newData.map(item => item.month)
    chartOption.value.series[0].data = newData.map(item => item.count)
  }
}, { immediate: true, deep: true })
</script>

<style scoped>
.v-chart {
  width: 100%;
  height: 100%;
}
</style>
