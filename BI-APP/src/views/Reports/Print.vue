<script setup>
defineOptions({ name: 'PrintReport' })
import { ref } from 'vue'
import { storeToRefs } from 'pinia'
import api from '@/services/api'
import { useReportFilterStore } from '@/stores/reportFilters'
import { useGlobalModalStore } from '@/stores/globalModal'
import ReportLayout from '@/components/ReportLayout.vue'

const filterStore = useReportFilterStore()
const {
  companyName,
  companyId,
  selectedYear,
  selectedPeriod,
  availableYears,
  availablePeriods,
  activePreset,
} = storeToRefs(filterStore)

const modalStore = useGlobalModalStore()

const monthNames = {
  '01': 'January',
  '02': 'February',
  '03': 'March',
  '04': 'April',
  '05': 'May',
  '06': 'June',
  '07': 'July',
  '08': 'August',
  '09': 'September',
  10: 'October',
  11: 'November',
  12: 'December',
}

const fetchLedger = async (y, p) => {
  try {
    const res = await api.get('/reports/ledger', {
      params: { year: y, period: p, preset: activePreset.value, company_id: companyId.value },
    })
    if (res.data.status === 'success') {
      return { structure: res.data.data, netIncome: res.data.net_income || 0 }
    }
  } catch (err) {
    console.error('Export API Error:', err)
  }
  return { structure: {}, netIncome: 0 }
}

const chunkFetch = async (factories, size = 4) => {
  const results = []
  for (let i = 0; i < factories.length; i += size) {
    const batch = factories.slice(i, i + size).map((fn) => fn())
    const batchResults = await Promise.all(batch)
    results.push(...batchResults)
  }
  return results
}

const buildExportPayload = async () => {
  const y = parseInt(selectedYear.value)
  const p = parseInt(selectedPeriod.value)

  let lastMoP = p - 1
  let lastMoY = y
  if (lastMoP === 0) {
    lastMoP = 12
    lastMoY = y - 1
  }

  const pStr = p.toString().padStart(2, '0')
  const lastMoPStr = lastMoP.toString().padStart(2, '0')

  const basePromises = [
    fetchLedger(y.toString(), pStr),
    fetchLedger(lastMoY.toString(), lastMoPStr),
    fetchLedger((y - 1).toString(), pStr),
  ]

  const qFactories = [
    () => fetchLedger(y.toString(), '03'),
    () => fetchLedger(y.toString(), '06'),
    () => fetchLedger(y.toString(), '09'),
    () => fetchLedger(y.toString(), '12'),
  ]

  const yFactories = Array.from(
    { length: 12 },
    (_, i) => () => fetchLedger(y.toString(), (i + 1).toString().padStart(2, '0')),
  )

  const [curr, prevMo, prevYr] = await Promise.all(basePromises)
  const quarterlyData = await chunkFetch(qFactories, 4)
  const yearlyData = await chunkFetch(yFactories, 4)

  return {
    currData: curr,
    prevMoData: prevMo,
    prevYrData: prevYr,
    quarterlyData: quarterlyData,
    yearlyData: yearlyData,
    company: companyName.value,
    year: selectedYear.value,
    periodName: monthNames[pStr],
    lastMonthName: monthNames[lastMoPStr],
    lastMonthYear: lastMoY.toString(),
    lastYearName: monthNames[pStr],
    lastYearYear: (y - 1).toString(),
    pStr,
  }
}

const isExportingExcel = ref(false)
const isExportingPdf = ref(false)

const triggerExcelExport = async () => {
  if (!selectedYear.value || !selectedPeriod.value) {
    modalStore.showAlert('Missing Filters', 'Please ensure a Year and Period are selected.', true)
    return
  }

  isExportingExcel.value = true
  modalStore.showAlert(
    'Exporting...',
    'Compiling your global financial report. This may take a few moments as we gather quarterly and yearly data.',
    false,
  )

  try {
    const payload = await buildExportPayload()
    const pStr = payload.pStr
    delete payload.pStr

    const response = await api.post('/export/excel', payload, {
      responseType: 'blob',
    })

    const filename = `${companyName.value}_${monthNames[pStr]}_${selectedYear.value}.xlsx`.replace(
      / /g,
      '_',
    )

    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', filename)
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url)

    modalStore.closeModal()
  } catch (error) {
    console.error('Global Export Failed:', error)
    modalStore.showAlert(
      'Export Failed',
      'The server could not generate the Excel file. Please try again.',
      true,
    )
  } finally {
    isExportingExcel.value = false
  }
}

const triggerPdfExport = async () => {
  if (!selectedYear.value || !selectedPeriod.value) {
    modalStore.showAlert('Missing Filters', 'Please ensure a Year and Period are selected.', true)
    return
  }

  isExportingPdf.value = true
  modalStore.showAlert(
    'Exporting...',
    'Compiling your global financial report. This may take a few moments as we gather quarterly and yearly data.',
    false,
  )

  try {
    const payload = await buildExportPayload()
    const pStr = payload.pStr
    delete payload.pStr

    const response = await api.post('/export/pdf', payload, {
      responseType: 'blob',
    })

    const filename = `${companyName.value}_${monthNames[pStr]}_${selectedYear.value}.pdf`.replace(
      / /g,
      '_',
    )

    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', filename)
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url)

    modalStore.closeModal()
  } catch (error) {
    console.error('Global Export Failed:', error)
    modalStore.showAlert(
      'Export Failed',
      'The server could not generate the PDF file. Please try again.',
      true,
    )
  } finally {
    isExportingPdf.value = false
  }
}
</script>

<template>
  <ReportLayout title="Print Reports" subtitle="Configure Period & Download Report Files">
    <div
      class="flex-1 flex flex-col items-center justify-center p-8 max-w-screen-2xl mx-auto w-full h-full min-h-0"
    >
      <div
        class="max-w-lg w-full bg-white/70 backdrop-blur-md border border-white/80 rounded-[32px] p-8 shadow-2xl space-y-6"
      >
        <div
          class="w-16 h-16 bg-emerald-50 border border-emerald-100 rounded-full flex items-center justify-center mx-auto text-3xl text-emerald-600 shadow-inner"
        >
          🖨️
        </div>

        <div class="text-center space-y-1">
          <h3 class="text-lg font-black text-slate-900 tracking-tight">
            Print Financial Statements
          </h3>
          <p class="text-xs text-slate-400 font-bold">
            Select parameters below to download comprehensive global statements
          </p>
        </div>

        <!-- Param Selectors -->
        <div class="bg-slate-50/50 border border-slate-200/40 rounded-2xl p-5 flex flex-col gap-4">
          <span class="text-[10px] font-black text-slate-400 uppercase tracking-wider block"
            >Report Parameters</span
          >

          <div class="grid grid-cols-2 gap-4">
            <div class="flex flex-col gap-1.5">
              <label class="text-[11px] font-bold text-slate-500">Select Month / Period</label>
              <select
                v-model="selectedPeriod"
                class="bg-white border border-slate-200 rounded-xl px-3 py-2 text-xs font-black text-slate-700 outline-none cursor-pointer focus:ring-2 focus:ring-cyan-500 shadow-sm w-full"
              >
                <option v-for="p in availablePeriods" :key="p" :value="p">
                  {{ monthNames[p] || p }}
                </option>
              </select>
            </div>

            <div class="flex flex-col gap-1.5">
              <label class="text-[11px] font-bold text-slate-500">Select Year</label>
              <select
                v-model="selectedYear"
                class="bg-white border border-slate-200 rounded-xl px-3 py-2 text-xs font-black text-slate-700 outline-none cursor-pointer focus:ring-2 focus:ring-cyan-500 shadow-sm w-full"
              >
                <option v-for="y in availableYears" :key="y" :value="y">{{ y }}</option>
              </select>
            </div>
          </div>
        </div>

        <!-- Export Buttons -->
        <div class="grid grid-cols-2 gap-4">
          <button
            @click="triggerExcelExport"
            :disabled="isExportingExcel || isExportingPdf"
            class="flex items-center justify-center gap-2.5 px-4 py-3 text-xs font-black text-emerald-800 bg-[#e6f4ea] border border-emerald-200 hover:bg-emerald-100 hover:border-emerald-300 rounded-2xl transition-all shadow-sm active:scale-95 disabled:opacity-50"
          >
            <span>📊</span>
            <span>Export to Excel</span>
          </button>

          <button
            @click="triggerPdfExport"
            :disabled="isExportingExcel || isExportingPdf"
            class="flex items-center justify-center gap-2.5 px-4 py-3 text-xs font-black text-rose-800 bg-[#fce8e6] border border-rose-200 hover:bg-rose-100 hover:border-rose-300 rounded-2xl transition-all shadow-sm active:scale-95 disabled:opacity-50"
          >
            <span>📕</span>
            <span>Export to PDF</span>
          </button>
        </div>
      </div>
    </div>
  </ReportLayout>
</template>
