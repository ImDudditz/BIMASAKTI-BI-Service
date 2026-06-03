<template>
  <div class="relative bg-white/90 backdrop-blur-md border border-slate-100 rounded-2xl shadow-sm p-4 sm:p-5 overflow-hidden flex flex-col h-[330px] hover:shadow-md hover:border-slate-200/80 transition-all duration-300">
    
    <div class="flex items-center justify-between mb-3">
      <div>
        <h4 class="text-xs sm:text-[13px] font-bold text-slate-800 tracking-tight">Capital Growth</h4>
        <p class="text-[10px] font-medium text-slate-400">Total Equity balance over periods of {{ props.selectedYear }}</p>
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
import { LineChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import * as echarts from 'echarts/core'

use([CanvasRenderer, LineChart, GridComponent, TooltipComponent])

const props = defineProps({
  rawLedgerData: { type: Object, required: true },
  selectedYear: { type: String, required: true },
  formatShortMoney: { type: Function, required: true },
  formatMoney: { type: Function, required: true },
  baseEchartsOptions: { type: Object, required: true }
})

const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]

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
      type: 'line',
      lineStyle: { color: 'rgba(99, 102, 241, 0.2)', width: 2, type: 'dashed' }
    },
    formatter: (params) => {
      const data = params[0]
      const formattedVal = props.formatMoney(data.value)
      return `
        <div style="font-weight: bold; margin-bottom: 4px;">${data.name} ${props.selectedYear}</div>
        <div style="display: flex; align-items: center; gap: 6px;">
          <span style="display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: #6366f1;"></span>
          <span style="color: #cbd5e1;">Total Equity:</span>
          <span style="font-weight: 800; color: #a5b4fc;">Rp ${formattedVal}</span>
        </div>
      `
    }
  },
  xAxis: {
    type: 'category',
    data: months,
    axisLine: { lineStyle: { color: '#cbd5e1' } },
    axisLabel: { color: '#64748b', fontSize: 10, fontWeight: 600 }
  },
  yAxis: {
    type: 'value',
    splitLine: { lineStyle: { color: '#f1f5f9', type: 'dashed' } },
    axisLabel: { 
      color: '#64748b', 
      fontSize: 10,
      formatter: (value) => props.formatShortMoney(value)
    }
  },
  series: [
    {
      name: 'Total Equity',
      type: 'line',
      smooth: true,
      showSymbol: true,
      symbol: 'circle',
      symbolSize: 7,
      lineStyle: {
        width: 3,
        color: '#6366f1'
      },
      itemStyle: {
        color: '#6366f1',
        borderColor: '#fff',
        borderWidth: 2
      },
      label: {
        show: true,
        position: 'top',
        fontSize: 9,
        fontWeight: 700,
        color: '#4f46e5',
        formatter: (params) => {
          if (!params.value) return ''
          return props.formatShortMoney(params.value)
        }
      },
      areaStyle: {
        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
          { offset: 0, color: 'rgba(99, 102, 241, 0.3)' },
          { offset: 1, color: 'rgba(99, 102, 241, 0.0)' }
        ])
      },
      data: []
    }
  ]
})

// Sync data reactively
watch([() => props.rawLedgerData, () => props.selectedYear], () => {
  const ledgerPayload = props.rawLedgerData?.yearlyData?.[props.selectedYear]
  if (!ledgerPayload) {
    chartOption.value.series[0].data = Array(12).fill(0)
    return
  }

  const today = new Date()
  const todayYear = today.getFullYear()
  const todayMonth = today.getMonth() + 1 // 1-based

  const yearNum = parseInt(props.selectedYear)

  const dataValues = months.map((_, i) => {
    const periodNum = i + 1
    const isFuture = yearNum > todayYear || (yearNum === todayYear && periodNum > todayMonth)
    
    if (isFuture) {
      return 0
    }

    const report = ledgerPayload[i]
    const equityTotal = report?.data?.Equity?.Total || report?.data?.Equity?.total || 0
    const netIncome = report?.netIncome || report?.net_income || 0
    return Math.round((parseFloat(equityTotal) || 0) + (parseFloat(netIncome) || 0))
  })

  chartOption.value.series[0].data = dataValues
}, { immediate: true, deep: true })
</script>

<style scoped>
.v-chart {
  width: 100%;
  height: 100%;
}
</style>
