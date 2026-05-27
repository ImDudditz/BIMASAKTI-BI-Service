<template>
  <div class="relative bg-white/90 backdrop-blur-md border border-slate-100 rounded-2xl shadow-sm p-6 overflow-hidden flex flex-col h-[380px] hover:shadow-md hover:border-slate-200/80 transition-all duration-300">
    
    <div class="flex items-center justify-between mb-4">
      <div>
        <h4 class="text-sm font-bold text-slate-800 tracking-tight">Top 5 Complaint by Area</h4>
        <p class="text-[11px] font-medium text-slate-400">Areas with the highest concentration of active complaints</p>
      </div>
    </div>

    <!-- Chart Container -->
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
  complaintsData: {
    type: Array,
    required: true,
    default: () => []
  }
})

const chartOption = ref({
  textStyle: { fontFamily: "'Inter', sans-serif" },
  grid: { top: '8%', left: '3%', right: '8%', bottom: '5%', containLabel: true },
  tooltip: {
    trigger: 'axis',
    backgroundColor: 'rgba(15, 23, 42, 0.95)',
    borderWidth: 0,
    padding: [10, 14],
    textStyle: { color: '#fff', fontSize: 12 },
    axisPointer: {
      type: 'shadow',
      shadowStyle: { color: 'rgba(244, 63, 94, 0.04)' }
    },
    formatter: (params) => {
      const data = params[0]
      return `
        <div style="font-weight: bold; margin-bottom: 4px;">Area: ${data.name}</div>
        <div style="display: flex; align-items: center; gap: 6px;">
          <span style="display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: #f43f5e;"></span>
          <span style="color: #cbd5e1;">Complaints:</span>
          <span style="font-weight: 800; color: #fda4af;">${data.value} tickets</span>
        </div>
      `
    }
  },
  xAxis: {
    type: 'value',
    splitLine: { lineStyle: { color: '#f1f5f9', type: 'dashed' } },
    axisLabel: { color: '#64748b', fontSize: 10 }
  },
  yAxis: {
    type: 'category',
    data: [],
    inverse: true, // keeps highest complaint count at the top
    axisLine: { lineStyle: { color: '#cbd5e1' } },
    axisLabel: { color: '#475569', fontSize: 11, fontWeight: 600 }
  },
  series: [
    {
      name: 'Complaints',
      type: 'bar',
      barWidth: '45%',
      data: [],
      itemStyle: {
        borderRadius: [0, 4, 4, 0], // rounded corners on right side
        color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [
          { offset: 0, color: '#f43f5e' },
          { offset: 1, color: '#fda4af' }
        ])
      },
      label: {
        show: true,
        position: 'right',
        color: '#475569',
        fontSize: 10,
        fontWeight: 700,
        formatter: '{c}'
      }
    }
  ]
})

watch(() => props.complaintsData, (newData) => {
  if (newData) {
    // Take top 5 and sort them for horizontal layout if inverse handles it
    const sortedData = [...newData].slice(0, 5)
    chartOption.value.yAxis.data = sortedData.map(item => item.area)
    chartOption.value.series[0].data = sortedData.map(item => item.complaintCount)
  }
}, { immediate: true, deep: true })
</script>

<style scoped>
.v-chart {
  width: 100%;
  height: 100%;
}
</style>
