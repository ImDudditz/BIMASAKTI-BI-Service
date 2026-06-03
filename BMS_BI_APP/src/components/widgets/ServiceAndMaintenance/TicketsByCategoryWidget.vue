<template>
  <div class="relative bg-white/90 backdrop-blur-md border border-slate-100 rounded-2xl shadow-sm p-4 sm:p-5 overflow-hidden flex flex-col h-[330px] hover:shadow-md hover:border-slate-200/80 transition-all duration-300">
    
    <div class="flex items-center justify-between mb-3">
      <div>
        <h4 class="text-xs sm:text-[13px] font-bold text-slate-800 tracking-tight">Tickets by Category</h4>
        <p class="text-[10px] font-medium text-slate-400">Total volume breakdown by department</p>
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
import { PieChart } from 'echarts/charts'
import { TooltipComponent, LegendComponent } from 'echarts/components'

use([CanvasRenderer, PieChart, TooltipComponent, LegendComponent])

const props = defineProps({
  categoriesData: {
    type: Array,
    required: true,
    default: () => []
  }
})

const colors = ['#f43f5e', '#3b82f6', '#fbbf24', '#8b5cf6', '#10b981', '#64748b']

const chartOption = ref({
  textStyle: { fontFamily: "'Inter', sans-serif" },
  color: colors,
  tooltip: {
    trigger: 'item',
    backgroundColor: 'rgba(15, 23, 42, 0.95)',
    borderWidth: 0,
    padding: [10, 14],
    textStyle: { color: '#fff', fontSize: 12 },
    formatter: (params) => {
      return `
        <div style="font-weight: bold; margin-bottom: 4px;">${params.name} Services</div>
        <div style="display: flex; align-items: center; gap: 6px;">
          <span style="display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: ${params.color};"></span>
          <span style="color: #cbd5e1;">Active Log:</span>
          <span style="font-weight: 800; color: #f43f5e;">${params.value} tickets (${params.percent}%)</span>
        </div>
      `
    }
  },
  legend: {
    orient: 'vertical',
    right: '0%',
    top: 'center',
    icon: 'circle',
    itemGap: 12,
    itemWidth: 8,
    itemHeight: 8,
    textStyle: { color: '#475569', fontSize: 11, fontWeight: 500 }
  },
  series: [
    {
      name: 'Ticket Category',
      type: 'pie',
      radius: '62%',
      center: ['40%', '50%'],
      avoidLabelOverlap: true,
      itemStyle: {
        borderRadius: 4,
        borderColor: '#fff',
        borderWidth: 2
      },
      label: { show: false },
      data: []
    }
  ]
})

watch(() => props.categoriesData, (newData) => {
  if (newData) {
    chartOption.value.series[0].data = newData
  }
}, { immediate: true, deep: true })
</script>

<style scoped>
.v-chart {
  width: 100%;
  height: 100%;
}
</style>
