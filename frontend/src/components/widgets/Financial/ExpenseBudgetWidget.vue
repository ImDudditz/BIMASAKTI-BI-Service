<template>
  <div class="relative bg-white/90 backdrop-blur-md border border-slate-100 rounded-2xl shadow-sm p-6 overflow-hidden flex flex-col h-[380px] hover:shadow-md hover:border-slate-200/80 transition-all duration-300">
    
    <div class="flex items-center justify-between mb-4">
      <div>
        <h4 class="text-sm font-bold text-slate-800 tracking-tight">Expense vs Budget</h4>
        <p class="text-[11px] font-medium text-slate-400">Monthly PTD expenses against budget for {{ props.selectedYear }}</p>
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
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import * as echarts from 'echarts/core'

use([CanvasRenderer, BarChart, GridComponent, TooltipComponent, LegendComponent])

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
  grid: { top: '18%', left: '3%', right: '3%', bottom: '5%', containLabel: true },
  legend: {
    data: ['Actual Expense', 'Budget Expense'],
    top: '0%',
    right: '0%',
    icon: 'circle',
    textStyle: { color: '#64748b', fontSize: 10, fontWeight: 600 }
  },
  tooltip: {
    trigger: 'axis',
    backgroundColor: 'rgba(15, 23, 42, 0.95)',
    borderWidth: 0,
    padding: [10, 14],
    textStyle: { color: '#fff', fontSize: 12 },
    axisPointer: {
      type: 'shadow',
      shadowStyle: { color: 'rgba(249, 115, 22, 0.03)' }
    },
    formatter: (params) => {
      let result = `<div style="font-weight: bold; margin-bottom: 6px;">${params[0].name} ${props.selectedYear}</div>`
      params.forEach(param => {
        const formattedVal = props.formatMoney(param.value)
        const isActual = param.seriesName === 'Actual Expense'
        const dotColor = isActual ? '#f97316' : '#94a3b8'
        const textColor = isActual ? '#fed7aa' : '#e2e8f0'
        result += `
          <div style="display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 4px;">
            <div style="display: flex; align-items: center; gap: 6px;">
              <span style="display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: ${dotColor};"></span>
              <span style="color: #cbd5e1;">${param.seriesName}:</span>
            </div>
            <span style="font-weight: 800; color: ${textColor};">Rp ${formattedVal}</span>
          </div>
        `
      })
      return result
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
      name: 'Actual Expense',
      type: 'bar',
      barMaxWidth: 18,
      itemStyle: {
        borderRadius: [4, 4, 0, 0],
        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
          { offset: 0, color: '#f97316' },
          { offset: 1, color: '#fb923c' }
        ])
      },
      data: []
    },
    {
      name: 'Budget Expense',
      type: 'bar',
      barMaxWidth: 18,
      itemStyle: {
        borderRadius: [4, 4, 0, 0],
        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
          { offset: 0, color: '#94a3b8' },
          { offset: 1, color: '#cbd5e1' }
        ])
      },
      data: []
    }
  ]
})

// Sync chart data reactively from rawLedgerData.yearlyData
watch([() => props.rawLedgerData, () => props.selectedYear], () => {
  const ledgerPayload = props.rawLedgerData?.yearlyData?.[props.selectedYear]
  if (!ledgerPayload) {
    chartOption.value.series[0].data = Array(12).fill(0)
    chartOption.value.series[1].data = Array(12).fill(0)
    return
  }

  const today = new Date()
  const todayYear = today.getFullYear()
  const todayMonth = today.getMonth() + 1 // 1-based
  const yearNum = parseInt(props.selectedYear)

  // 1. Calculate accumulated actual values for all months
  const accumActuals = months.map((_, i) => {
    const periodNum = i + 1
    const isFuture = yearNum > todayYear || (yearNum === todayYear && periodNum > todayMonth)
    if (isFuture) return 0

    const report = ledgerPayload[i]
    // ptd_amount → Expenses section Total (sum of period actual balances)
    const expensesTotal = report?.data?.Expenses?.Total || report?.data?.Expenses?.total || 0
    return Math.abs(parseFloat(expensesTotal) || 0)
  })

  // 2. Subtract previous month's value to get per-period (non-accumulated) actual values
  const actualData = accumActuals.map((val, i) => {
    const periodNum = i + 1
    const isFuture = yearNum > todayYear || (yearNum === todayYear && periodNum > todayMonth)
    if (isFuture) return 0

    if (i === 0) return Math.round(val)
    const periodVal = val - accumActuals[i - 1]
    return Math.round(Math.abs(periodVal))
  })

  // 3. Calculate accumulated budget values for all months
  const accumBudgets = months.map((_, i) => {
    const periodNum = i + 1
    const isFuture = yearNum > todayYear || (yearNum === todayYear && periodNum > todayMonth)
    if (isFuture) return 0

    const report = ledgerPayload[i]
    if (!report?.data?.Expenses?.Groups) return 0

    // end_budget → Sum of all Expenses items' end_budget
    let budgetTotal = 0
    const groups = report.data.Expenses.Groups
    for (const groupKey of Object.keys(groups)) {
      const items = groups[groupKey]?.Items || groups[groupKey]?.items || []
      for (const item of items) {
        budgetTotal += Math.abs(parseFloat(item.end_budget ?? 0) || 0)
      }
    }
    return budgetTotal
  })

  // 4. Subtract previous month's value to get per-period (non-accumulated) budget values
  const budgetData = accumBudgets.map((val, i) => {
    const periodNum = i + 1
    const isFuture = yearNum > todayYear || (yearNum === todayYear && periodNum > todayMonth)
    if (isFuture) return 0

    if (i === 0) return Math.round(val)
    const periodVal = val - accumBudgets[i - 1]
    return Math.round(Math.abs(periodVal))
  })

  chartOption.value.series[0].data = actualData
  chartOption.value.series[1].data = budgetData
}, { immediate: true, deep: true })
</script>

<style scoped>
.v-chart {
  width: 100%;
  height: 100%;
}
</style>
