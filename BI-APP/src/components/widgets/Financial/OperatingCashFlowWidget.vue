<template>
  <div class="relative bg-white/90 backdrop-blur-md border border-slate-100 rounded-2xl shadow-sm p-4 sm:p-5 overflow-hidden flex flex-col h-[330px] hover:shadow-md hover:border-slate-200/80 transition-all duration-300">
    
    <div class="flex items-center justify-between mb-3">
      <div>
        <h4 class="text-xs sm:text-[13px] font-bold text-slate-800 tracking-tight">Operating Cash Inflow vs Outflow</h4>
        <p class="text-[10px] font-medium text-slate-400">Monthly operating cash flow comparison for {{ props.selectedYear }}</p>
      </div>
    </div>

    <!-- Loading Indicator -->
    <div v-if="isLoading" class="grow flex flex-col items-center justify-center min-h-0 w-full">
      <div class="w-8 h-8 border-3 border-emerald-100 border-t-emerald-600 rounded-full animate-spin"></div>
      <p class="text-slate-400 text-xs mt-2 animate-pulse">Loading cash flows...</p>
    </div>

    <!-- Chart Block -->
    <div v-else class="grow relative min-h-0 w-full">
      <v-chart class="w-full h-full" :option="chartOption" autoresize />
    </div>
  </div>
</template>

<script setup>
import { ref, watch, onMounted, defineProps } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import * as echarts from 'echarts/core'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'

use([CanvasRenderer, BarChart, GridComponent, TooltipComponent, LegendComponent])

const props = defineProps({
  selectedYear: { type: String, required: true },
  formatShortMoney: { type: Function, required: true },
  formatMoney: { type: Function, required: true },
  baseEchartsOptions: { type: Object, required: true }
})

const authStore = useAuthStore()
const isLoading = ref(true)
const rawCashFlowData = ref([])

const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]

const chartOption = ref({
  textStyle: { fontFamily: "'Inter', sans-serif" },
  grid: { top: '18%', left: '3%', right: '3%', bottom: '5%', containLabel: true },
  legend: {
    data: ['Cash Inflow from Operating', 'Cash Outflow from Operating'],
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
      shadowStyle: { color: 'rgba(16, 185, 129, 0.03)' }
    },
    formatter: (params) => {
      let result = `<div style="font-weight: bold; margin-bottom: 6px;">${params[0].name} ${props.selectedYear}</div>`
      params.forEach(param => {
        const formattedVal = props.formatMoney(param.value)
        const dotColor = param.seriesName.includes('Inflow') ? '#10b981' : '#f43f5e'
        const textColor = param.seriesName.includes('Inflow') ? '#a7f3d0' : '#fecdd3'
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
      name: 'Cash Inflow from Operating',
      type: 'bar',
      barMaxWidth: 16,
      itemStyle: {
        borderRadius: [4, 4, 0, 0],
        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
          { offset: 0, color: '#10b981' },
          { offset: 1, color: '#34d399' }
        ])
      },
      data: []
    },
    {
      name: 'Cash Outflow from Operating',
      type: 'bar',
      barMaxWidth: 16,
      itemStyle: {
        borderRadius: [4, 4, 0, 0],
        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
          { offset: 0, color: '#f43f5e' },
          { offset: 1, color: '#fb7185' }
        ])
      },
      data: []
    }
  ]
})

const fetchCashFlow = async () => {
  const company_id = authStore.user?.company_id;
  if (!company_id || !props.selectedYear) return;
  
  isLoading.value = true;
  try {
    const res = await api.get('/v1/dashboard/cashbook/cashflow', {
      params: { company_id, year: props.selectedYear }
    });
    rawCashFlowData.value = res.data || [];
  } catch (error) {
    console.error("Failed to fetch cash flow data", error);
  } finally {
    isLoading.value = false;
  }
}

const syncChart = () => {
  const today = new Date()
  const todayYear = today.getFullYear()
  const todayMonth = today.getMonth() + 1 // 1-based

  const yearNum = parseInt(props.selectedYear)

  const inflows = Array(12).fill(0)
  const outflows = Array(12).fill(0)

  months.forEach((_, i) => {
    const periodNum = i + 1
    const isFuture = yearNum > todayYear || (yearNum === todayYear && periodNum > todayMonth)

    if (isFuture) {
      inflows[i] = 0
      outflows[i] = 0
      return
    }

    const periodStr = `${props.selectedYear}-${periodNum.toString().padStart(2, '0')}`
    
    // Find matching items in raw data
    const periodItems = rawCashFlowData.value.filter(item => {
      const p = item.period ?? item.Period ?? '';
      return p === periodStr;
    })
    
    periodItems.forEach(item => {
      const amt = Math.round(parseFloat(item.actual_amount ?? item.actualAmount) || 0)
      const subGrp = (item.sub_group ?? item.subGroup ?? '').toString()
      
      // Match IO (Cash Inflow from Operating) and OO (Cash Outflow from Operating)
      if (subGrp.includes('Cash Inflow') || subGrp.includes('IO')) {
        inflows[i] += amt
      } else if (subGrp.includes('Cash Outflow') || subGrp.includes('OO')) {
        // Outflow represents money going out, so we plot its absolute/positive value for visual comparison
        outflows[i] += Math.abs(amt)
      }
    })
  })

  chartOption.value.series[0].data = inflows
  chartOption.value.series[1].data = outflows
}

// Watchers with robust fallback options
watch([() => props.selectedYear, () => authStore.user?.company_id], fetchCashFlow, { immediate: true })
watch(rawCashFlowData, syncChart, { deep: true, immediate: true })

onMounted(fetchCashFlow)
</script>

<style scoped>
.v-chart {
  width: 100%;
  height: 100%;
}
</style>
