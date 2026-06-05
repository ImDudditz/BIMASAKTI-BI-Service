<template>
  <div
    class="relative bg-white/90 backdrop-blur-md border border-slate-100 rounded-2xl shadow-sm p-4 sm:p-5 overflow-hidden flex flex-col h-[330px] hover:shadow-md hover:border-slate-200/80 transition-all duration-300"
  >
    <div class="flex items-center justify-between mb-3">
      <div>
        <h4 class="text-xs sm:text-[13px] font-bold text-slate-800 tracking-tight">
          Top 5 Request by Tenant
        </h4>
        <p class="text-[10px] font-medium text-slate-400">
          Tenants with the most active service requests
        </p>
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
  requestsData: {
    type: Array,
    required: true,
    default: () => [],
  },
})

// Store a map of tenant name to top request type for tooltip access
const topRequestTypesMap = ref({})

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
      shadowStyle: { color: 'rgba(59, 130, 246, 0.04)' },
    },
    formatter: (params) => {
      const data = params[0]
      const tenantName = data.name
      const count = data.value
      const topType = topRequestTypesMap.value[tenantName] || 'General'

      return `
        <div style="font-weight: bold; margin-bottom: 4px; max-width: 250px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">
          Tenant: ${tenantName}
        </div>
        <div style="display: flex; align-items: center; gap: 6px; margin-bottom: 4px;">
          <span style="display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: #3b82f6;"></span>
          <span style="color: #cbd5e1;">Requests:</span>
          <span style="font-weight: 800; color: #93c5fd;">${count} tickets</span>
        </div>
        <div style="font-size: 11px; color: #94a3b8; font-style: italic; margin-top: 4px; padding-top: 4px; border-top: 1px solid rgba(255,255,255,0.1);">
          Top Type: ${topType}
        </div>
      `
    },
  },
  xAxis: {
    type: 'value',
    splitLine: { lineStyle: { color: '#f1f5f9', type: 'dashed' } },
    axisLabel: { color: '#64748b', fontSize: 10 },
  },
  yAxis: {
    type: 'category',
    data: [],
    inverse: true, // keeps highest count at the top
    axisLine: { lineStyle: { color: '#cbd5e1' } },
    axisLabel: {
      color: '#475569',
      fontSize: 10,
      fontWeight: 600,
      formatter: (value) => {
        // Truncate long tenant names to keep layout extremely neat
        return value.length > 18 ? value.slice(0, 16) + '...' : value
      },
    },
  },
  series: [
    {
      name: 'Requests',
      type: 'bar',
      barWidth: '45%',
      data: [],
      itemStyle: {
        borderRadius: [0, 4, 4, 0],
        color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [
          { offset: 0, color: '#3b82f6' },
          { offset: 1, color: '#93c5fd' },
        ]),
      },
      label: {
        show: true,
        position: 'right',
        color: '#475569',
        fontSize: 10,
        fontWeight: 700,
        formatter: '{c}',
      },
    },
  ],
})

watch(
  () => props.requestsData,
  (newData) => {
    if (newData) {
      const sortedData = [...newData].slice(0, 5)

      // Build map for quick O(1) tooltip lookup
      const typeMap = {}
      sortedData.forEach((item) => {
        typeMap[item.tenant] = item.topRequestType || 'General'
      })
      topRequestTypesMap.value = typeMap

      chartOption.value.yAxis.data = sortedData.map((item) => item.tenant)
      chartOption.value.series[0].data = sortedData.map((item) => item.requestCount)
    }
  },
  { immediate: true, deep: true },
)
</script>

<style scoped>
.v-chart {
  width: 100%;
  height: 100%;
}
</style>
