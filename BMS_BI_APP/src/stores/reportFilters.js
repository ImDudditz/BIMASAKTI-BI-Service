import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import api from '@/services/api'

export const useReportFilterStore = defineStore('reportFilters', () => {
  const today = new Date()
  const currentYear = String(today.getFullYear())
  const currentPeriod = String(today.getMonth() + 1).padStart(2, '0')

  const companyId = ref(sessionStorage.getItem('reportCompanyId') || 'ASHMD')
  const companyName = ref(sessionStorage.getItem('reportCompanyName') || 'PT. Agung Sedayu Propertindo')

  const selectedYear = ref(sessionStorage.getItem('reportYear') || '')
  const selectedPeriod = ref(sessionStorage.getItem('reportPeriod') || '')
  const displayFormat = ref(sessionStorage.getItem('reportFormat') || '1')
  const showBudget = ref(sessionStorage.getItem('reportShowBudget') !== 'false')

  const activePreset = ref(sessionStorage.getItem('reportActivePreset') || 'preset1')
  
  const savedNames = sessionStorage.getItem('reportPresetNames')
  const defaultNames = { preset1: 'Preset 1', preset2: 'Preset 2', preset3: 'Preset 3' }
  const presetNames = ref(savedNames ? JSON.parse(savedNames) : defaultNames)
  
  const accountMappings = ref({})

  const availableYears = ref([])
  const availablePeriods = ref([])
  const lastFetchedCompanyId = ref('')

  watch([selectedYear, selectedPeriod, displayFormat, showBudget, companyId, companyName, activePreset, presetNames], 
    ([newYear, newPeriod, newFmt, newShowBud, newCid, newCnm, newPreset, newNames]) => {
    sessionStorage.setItem('reportYear', newYear)
    sessionStorage.setItem('reportPeriod', newPeriod)
    sessionStorage.setItem('reportFormat', newFmt)
    sessionStorage.setItem('reportShowBudget', newShowBud)
    sessionStorage.setItem('reportCompanyId', newCid)
    sessionStorage.setItem('reportCompanyName', newCnm)
    sessionStorage.setItem('reportActivePreset', newPreset)
    sessionStorage.setItem('reportPresetNames', JSON.stringify(newNames))
  }, { deep: true })

  const fetchFilters = async () => {
    const isCompanyChanged = lastFetchedCompanyId.value && lastFetchedCompanyId.value !== companyId.value
    if (!isCompanyChanged && availableYears.value.length > 0 && availablePeriods.value.length > 0) return
    try {
      const res = await api.get('/filters', { params: { company_id: companyId.value } })
      if (res.data.status === 'success') {
        availableYears.value = res.data.years
        availablePeriods.value = res.data.periods
        if (res.data.company_name) companyName.value = res.data.company_name;
        if (res.data.company_id) companyId.value = res.data.company_id;

        // Default to the latest available year and period if not already set or invalid, or if company changed
        if (res.data.years.length > 0) {
          if (isCompanyChanged || !selectedYear.value || !res.data.years.includes(selectedYear.value)) {
            selectedYear.value = res.data.latest_year || res.data.years[0]
          }
        } else if (!selectedYear.value) {
          selectedYear.value = currentYear
        }

        if (res.data.periods.length > 0) {
          if (isCompanyChanged || !selectedPeriod.value || !res.data.periods.includes(selectedPeriod.value)) {
            selectedPeriod.value = res.data.latest_period || res.data.periods[res.data.periods.length - 1]
          }
        } else if (!selectedPeriod.value) {
          selectedPeriod.value = currentPeriod
        }
        
        lastFetchedCompanyId.value = companyId.value

        const namesRes = await api.get('/preset-names', { params: { company_id: companyId.value }})
        if (namesRes.data) presetNames.value = namesRes.data
      }
    } catch (err) { console.error("Failed to load global filters:", err) }
  }

  const isSidebarOpen = ref(false)

  const fetchMappings = async () => {
    try {
      const res = await api.get('/mappings', { params: { company_id: companyId.value, preset: activePreset.value } })
      accountMappings.value = res.data || {}
    } catch (err) { console.error("Failed to load account mappings:", err) }
  }

  return { 
    companyId, companyName, 
    selectedYear, selectedPeriod, displayFormat, showBudget, 
    availableYears, availablePeriods,
    activePreset, presetNames, accountMappings,
    fetchFilters, fetchMappings,
    isSidebarOpen
  }
})