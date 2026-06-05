<script setup>
import { ref, onMounted, computed } from 'vue'
import { storeToRefs } from 'pinia'
import api from '@/services/api'
import { useReportFilterStore } from '@/stores/reportFilters'
import { useGlobalModalStore } from '@/stores/globalModal'
import ReportLayout from '@/components/ReportLayout.vue'

const modalStore = useGlobalModalStore()

const filterStore = useReportFilterStore()
const { companyId, activePreset, presetNames, accountMappings } = storeToRefs(filterStore)

const isLoading = ref(false)
const accounts = ref([])
const range = ref({ from: '', to: '', section: 'Assets', group: '' })
const groupToRemove = ref('')

const searchQuery = ref('')
const selectedFilter = ref('all')
const currentPage = ref(1)
const pageSize = ref(50)

const filteredAccounts = computed(() => {
  return accounts.value.filter((acc) => {
    const q = searchQuery.value.toLowerCase().trim()
    const matchesQuery =
      !q || acc.account_no.toLowerCase().includes(q) || acc.account_name.toLowerCase().includes(q)

    const mapped = isMapped(acc.account_no)
    let matchesFilter = true
    if (selectedFilter.value === 'mapped') matchesFilter = mapped
    else if (selectedFilter.value === 'unmapped') matchesFilter = !mapped

    return matchesQuery && matchesFilter
  })
})

const totalPages = computed(() => {
  const count = filteredAccounts.value.length
  return count > 0 ? Math.ceil(count / pageSize.value) : 1
})

const paginatedAccounts = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  const end = start + pageSize.value
  return filteredAccounts.value.slice(start, end)
})

const localUnmappedCount = computed(() => {
  let count = 0
  Object.values(accountMappings.value).forEach((mapping) => {
    if (!mapping || !mapping.group || mapping.group === 'Uncategorized') {
      count++
    }
  })
  return count
})

const unmappedAccounts = computed(() => {
  return accounts.value.filter((acc) => {
    const mapping = accountMappings.value[acc.account_no]
    return !mapping || !mapping.group || mapping.group === 'Uncategorized'
  })
})

const existingGroups = computed(() => {
  const groups = new Set()
  Object.values(accountMappings.value).forEach((mapping) => {
    if (mapping && mapping.group && mapping.group !== 'Uncategorized') {
      groups.add(mapping.group)
    }
  })
  return Array.from(groups).sort()
})

const fetchCOA = async () => {
  isLoading.value = true
  try {
    const res = await api.get('/reports/coa', { params: { company_id: companyId.value } })
    accounts.value = res.data.sort((a, b) => a.account_no.localeCompare(b.account_no))

    await filterStore.fetchMappings()

    accounts.value.forEach((acc) => {
      if (!accountMappings.value[acc.account_no]) {
        let defSec = 'Expenses'
        if (acc.account_no.startsWith('1')) defSec = 'Assets'
        else if (acc.account_no.startsWith('2')) defSec = 'Liabilities'
        else if (acc.account_no.startsWith('3')) defSec = 'Equity'
        else if (acc.account_no.startsWith('4')) defSec = 'Revenue'

        accountMappings.value[acc.account_no] = {
          section: defSec,
          group: 'Uncategorized',
        }
      }
    })
  } catch (err) {
    console.error('Failed to load COA', err)
  } finally {
    isLoading.value = false
  }
}

const applyRange = () => {
  if (!range.value.from || !range.value.to || !range.value.group) return
  accounts.value.forEach((acc) => {
    if (acc.account_no >= range.value.from && acc.account_no <= range.value.to) {
      accountMappings.value[acc.account_no] = {
        section: range.value.section,
        group: range.value.group,
      }
    }
  })
  range.value.from = ''
  range.value.to = ''
  range.value.group = ''
}

const removeGroup = () => {
  if (!groupToRemove.value) return
  modalStore.showConfirm(
    'Delete Group',
    `Are you sure you want to delete the group "${groupToRemove.value}"? All associated accounts will return to Uncategorized.`,
    '🗑️',
    true,
    () => {
      Object.keys(accountMappings.value).forEach((accNo) => {
        if (accountMappings.value[accNo].group === groupToRemove.value) {
          accountMappings.value[accNo].group = 'Uncategorized'
        }
      })
      groupToRemove.value = ''
    },
  )
}

const renamePreset = async () => {
  const current = presetNames.value[activePreset.value]
  modalStore.showPrompt(
    'Rename Profile',
    `Enter a new name for the profile currently named "${current}":`,
    current,
    async (newName) => {
      if (newName && newName.trim()) {
        presetNames.value[activePreset.value] = newName.trim()
        await api.post('/preset-names', {
          company_id: companyId.value,
          preset: 'global',
          data: presetNames.value,
        })
      }
    },
  )
}

const clearPreset = async () => {
  const current = presetNames.value[activePreset.value]
  modalStore.showConfirm(
    'Clear Profile',
    `Are you sure you want to clear "${current}"? This will reset all accounts in this profile to Uncategorized.`,
    '⚠️',
    true,
    async () => {
      isLoading.value = true
      try {
        await api.delete('/mappings', {
          params: { company_id: companyId.value, preset: activePreset.value },
        })

        const defaultName =
          activePreset.value === 'preset1'
            ? 'Preset 1'
            : activePreset.value === 'preset2'
              ? 'Preset 2'
              : 'Preset 3'
        presetNames.value[activePreset.value] = defaultName

        await api.post('/preset-names', {
          company_id: companyId.value,
          preset: 'global',
          data: presetNames.value,
        })
      } catch (err) {
        console.error('Failed to clear profile:', err)
      }
      await fetchCOA()
    },
  )
}

const saveMappings = async () => {
  isLoading.value = true
  try {
    await api.post('/mappings', {
      company_id: companyId.value,
      preset: activePreset.value,
      data: accountMappings.value,
    })
    modalStore.showAlert('Success', 'Mappings saved successfully!')
  } catch (err) {
    modalStore.showAlert('Error', 'Failed to save mappings.', true)
    console.error(err)
  } finally {
    isLoading.value = false
  }
}

const isMapped = (accNo) => {
  const mapping = accountMappings.value[accNo]
  return mapping && mapping.group && mapping.group !== 'Uncategorized'
}

const switchPreset = () => {
  fetchCOA()
}

onMounted(fetchCOA)
</script>

<template>
  <ReportLayout title="Account Mapping" subtitle="SETUP CHART OF ACCOUNTS STRUCTURE">
    <template #actions>
      <div class="flex items-center gap-2">
        <button
          @click="saveMappings"
          class="bg-emerald-600 hover:bg-emerald-700 text-white px-5 py-1.5 rounded-lg text-sm font-bold shadow-md transition-all active:scale-95 flex items-center gap-2"
        >
          <span>💾</span> Save All Mappings
        </button>
      </div>
    </template>

    <div class="overflow-y-auto custom-scrollbar flex-1 min-h-0 w-full relative z-10">
      <div
        class="max-w-[1920px] mx-auto px-4 sm:px-6 lg:px-8 py-4 flex lg:flex-row flex-col gap-5 h-full w-full min-h-0 relative z-10"
      >
        <!-- LEFT SIDEBAR: BULK WIZARD (TIGHTENED) -->
        <aside
          class="w-full lg:w-[300px] xl:w-[340px] shrink-0 flex flex-col gap-4 overflow-y-auto custom-scrollbar h-full lg:pr-1"
        >
          <div
            class="bg-white/90 backdrop-blur-md p-4 rounded-xl border border-white/50 shadow-sm flex flex-col gap-3.5 h-max"
          >
            <h4
              class="text-sky-900 font-black text-[13px] uppercase flex items-center gap-2 border-b border-slate-200 pb-2 tracking-wide"
            >
              <span class="p-1 bg-sky-100 rounded text-base">🚀</span> Bulk Wizard
            </h4>

            <!-- Profile Settings -->
            <div class="flex flex-col gap-1 relative z-10">
              <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wider"
                >Active Profile</label
              >
              <div
                class="flex items-center bg-white border border-sky-200 rounded shadow-sm w-full"
              >
                <select
                  v-model="activePreset"
                  @change="switchPreset"
                  class="bg-transparent text-[13px] font-bold text-sky-900 px-2 py-1 outline-none cursor-pointer flex-1 min-w-0 truncate"
                >
                  <option value="preset1">{{ presetNames.preset1 }}</option>
                  <option value="preset2">{{ presetNames.preset2 }}</option>
                  <option value="preset3">{{ presetNames.preset3 }}</option>
                </select>
                <div class="w-px h-5 bg-sky-200"></div>
                <button
                  @click="renamePreset"
                  title="Rename Profile"
                  class="px-2 hover:bg-sky-50 transition-colors text-[13px] shrink-0"
                >
                  ✏️
                </button>
                <div class="w-px h-5 bg-sky-200"></div>
                <button
                  @click="clearPreset"
                  title="Clear Profile"
                  class="px-2 hover:bg-rose-50 transition-colors text-[13px] shrink-0"
                >
                  🗑️
                </button>
              </div>
            </div>

            <!-- Apply Range -->
            <div class="flex flex-col gap-2 pt-2 border-t border-slate-100 relative z-10">
              <div class="flex flex-col gap-0.5">
                <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wider"
                  >From Account</label
                >
                <select
                  v-model="range.from"
                  class="border border-slate-200 rounded py-1 px-2 text-[13px] focus:ring-2 ring-sky-500 outline-none w-full truncate bg-white/50"
                >
                  <option v-for="a in unmappedAccounts" :key="a.account_no" :value="a.account_no">
                    {{ a.account_no }} - {{ a.account_name }}
                  </option>
                </select>
              </div>
              <div class="flex flex-col gap-0.5">
                <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wider"
                  >To Account</label
                >
                <select
                  v-model="range.to"
                  class="border border-slate-200 rounded py-1 px-2 text-[13px] focus:ring-2 ring-sky-500 outline-none w-full truncate bg-white/50"
                >
                  <option v-for="a in unmappedAccounts" :key="a.account_no" :value="a.account_no">
                    {{ a.account_no }} - {{ a.account_name }}
                  </option>
                </select>
              </div>
              <div class="flex flex-col gap-0.5">
                <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wider"
                  >Statement Section</label
                >
                <select
                  v-model="range.section"
                  class="border border-slate-200 rounded py-1 px-2 text-[13px] outline-none bg-white/50"
                >
                  <option>Assets</option>
                  <option>Liabilities</option>
                  <option>Equity</option>
                  <option>Revenue</option>
                  <option>Expenses</option>
                </select>
              </div>
              <div class="flex flex-col gap-0.5">
                <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wider"
                  >New Group Name</label
                >
                <input
                  v-model="range.group"
                  type="text"
                  placeholder="e.g. Cash & Bank"
                  class="border border-slate-200 rounded py-1 px-2 text-[13px] focus:ring-2 ring-sky-500 outline-none bg-white/50"
                />
              </div>
              <button
                @click="applyRange"
                class="w-full mt-0.5 bg-sky-600 hover:bg-sky-700 text-white font-bold py-1.5 rounded text-[13px] shadow-sm transition-all active:scale-95"
              >
                Apply Range
              </button>
            </div>

            <!-- Delete Group -->
            <div class="flex flex-col gap-2 pt-2 border-t border-slate-100 relative z-10">
              <div class="flex flex-col gap-0.5">
                <label class="text-[10px] font-bold text-slate-500 uppercase tracking-wider"
                  >Delete Existing Group</label
                >
                <select
                  v-model="groupToRemove"
                  class="border border-slate-200 rounded py-1 px-2 text-[13px] focus:ring-2 ring-rose-500 outline-none w-full truncate bg-white/50"
                >
                  <option value="" disabled>Select group...</option>
                  <option v-for="g in existingGroups" :key="g" :value="g">{{ g }}</option>
                </select>
              </div>
              <button
                @click="removeGroup"
                :disabled="!groupToRemove"
                class="w-full bg-rose-100 text-rose-700 hover:bg-rose-200 disabled:opacity-50 font-bold py-1.5 rounded text-[13px] shadow-sm transition-all active:scale-95"
              >
                Remove Group
              </button>
            </div>
          </div>
        </aside>

        <!-- CENTER: MAIN TABLE CONTENT -->
        <main
          class="flex-1 flex flex-col min-w-0 bg-white/90 backdrop-blur-md rounded-xl border border-white/50 shadow-sm overflow-hidden h-full relative"
        >
          <div v-if="isLoading" class="flex flex-col items-center justify-center h-full w-full">
            <div
              class="w-10 h-10 border-4 border-sky-100 border-t-sky-600 rounded-full animate-spin"
            ></div>
          </div>

          <template v-else>
            <!-- Search, Filter & Pagination Toolbar -->
            <div
              class="bg-slate-50 border-b border-slate-200 px-5 py-3 flex flex-wrap items-center justify-between gap-3 shrink-0"
            >
              <div class="flex items-center gap-3">
                <!-- Search input -->
                <div class="relative">
                  <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-xs"
                    >🔍</span
                  >
                  <input
                    v-model="searchQuery"
                    @input="currentPage = 1"
                    type="text"
                    placeholder="Search by name or number..."
                    class="pl-8 pr-3 py-1.5 text-xs font-bold border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-sky-500 w-56 bg-white transition-all"
                  />
                </div>

                <!-- Filter select -->
                <select
                  v-model="selectedFilter"
                  @change="currentPage = 1"
                  class="border border-slate-200 rounded-lg px-2.5 py-1.5 text-xs font-bold text-slate-600 bg-white outline-none cursor-pointer focus:ring-2 focus:ring-sky-500"
                >
                  <option value="all">All Accounts</option>
                  <option value="mapped">Mapped Only</option>
                  <option value="unmapped">Unmapped Only</option>
                </select>
              </div>

              <!-- Pagination Controls -->
              <div
                class="flex items-center gap-3 text-xs font-bold text-slate-600"
                v-if="totalPages > 1"
              >
                <button
                  @click="currentPage = Math.max(1, currentPage - 1)"
                  :disabled="currentPage === 1"
                  class="p-1 px-2.5 border border-slate-200 rounded hover:bg-slate-100 disabled:opacity-40 disabled:hover:bg-transparent transition-colors active:scale-95 cursor-pointer"
                >
                  ◀ Prev
                </button>
                <span>Page {{ currentPage }} of {{ totalPages }}</span>
                <button
                  @click="currentPage = Math.min(totalPages, currentPage + 1)"
                  :disabled="currentPage === totalPages"
                  class="p-1 px-2.5 border border-slate-200 rounded hover:bg-slate-100 disabled:opacity-40 disabled:hover:bg-transparent transition-colors active:scale-95 cursor-pointer"
                >
                  Next ▶
                </button>
              </div>
            </div>

            <div class="overflow-auto custom-scrollbar flex-1 w-full h-full relative z-10">
              <table class="w-full text-left border-collapse min-w-[700px]">
                <thead
                  class="bg-sky-50/95 text-sky-900 font-black text-[11px] uppercase tracking-wider border-b border-slate-300 sticky top-0 z-20 backdrop-blur-md shadow-sm"
                >
                  <tr>
                    <th class="px-5 py-3 w-32">Account No</th>
                    <th class="px-5 py-3">Account Name</th>
                    <th class="px-5 py-3 w-48">Statement Section</th>
                    <th class="px-5 py-3 w-64">Group Category</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-white">
                  <tr
                    v-for="acc in paginatedAccounts"
                    :key="acc.account_no"
                    class="transition-colors group"
                    :class="
                      isMapped(acc.account_no)
                        ? 'bg-emerald-50/80 hover:bg-emerald-100'
                        : 'bg-rose-50/80 hover:bg-rose-100'
                    "
                  >
                    <td
                      class="px-5 py-2 text-[13px] font-mono font-bold"
                      :class="isMapped(acc.account_no) ? 'text-emerald-700' : 'text-rose-700'"
                    >
                      {{ acc.account_no }}
                    </td>
                    <td
                      class="px-5 py-2 text-[13px] font-bold"
                      :class="isMapped(acc.account_no) ? 'text-slate-700' : 'text-slate-800'"
                    >
                      {{ acc.account_name }}
                    </td>
                    <td class="px-5 py-2">
                      <select
                        v-model="accountMappings[acc.account_no].section"
                        class="bg-transparent border-none text-[13px] font-bold focus:ring-0 cursor-pointer outline-none w-full p-0"
                        :class="isMapped(acc.account_no) ? 'text-emerald-800' : 'text-rose-800'"
                      >
                        <option>Assets</option>
                        <option>Liabilities</option>
                        <option>Equity</option>
                        <option>Revenue</option>
                        <option>Expenses</option>
                      </select>
                    </td>
                    <td class="px-5 py-2">
                      <input
                        v-model.lazy="accountMappings[acc.account_no].group"
                        type="text"
                        class="w-full bg-white/70 border border-white/50 rounded py-1.5 px-2.5 text-[13px] font-bold focus:border-sky-500 focus:bg-white outline-none transition-all"
                        :class="
                          isMapped(acc.account_no)
                            ? 'text-emerald-900 focus:border-emerald-500'
                            : 'border-rose-300 bg-white text-rose-900 font-black focus:border-rose-500'
                        "
                      />
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </template>
        </main>

        <!-- RIGHT SIDEBAR: GUIDE (TIGHTENED) -->
        <aside
          class="w-full lg:w-[260px] xl:w-[280px] shrink-0 flex flex-col gap-4 overflow-y-auto custom-scrollbar h-full lg:pl-1"
        >
          <div
            class="bg-white/90 backdrop-blur-md p-4 rounded-xl border border-white/50 shadow-sm flex flex-col gap-3.5 h-max"
          >
            <h4
              class="text-sky-900 font-black text-[13px] uppercase flex items-center gap-2 border-b border-slate-200 pb-2 tracking-wide"
            >
              <span class="p-1 bg-sky-100 rounded text-base">📖</span> Mapping Guide
            </h4>

            <div class="flex flex-col gap-3.5 text-[13px] text-slate-600">
              <!-- Dynamic Tracker Badge -->
              <div
                class="flex items-center gap-3 p-2.5 rounded-lg border transition-all duration-500"
                :class="
                  localUnmappedCount > 0
                    ? 'bg-rose-50 border-rose-100'
                    : 'bg-emerald-50 border-emerald-100 shadow-emerald-100/50 shadow-sm'
                "
              >
                <div
                  class="text-3xl font-black"
                  :class="localUnmappedCount > 0 ? 'text-rose-500' : 'text-emerald-500'"
                >
                  {{ localUnmappedCount }}
                </div>
                <div class="flex flex-col justify-center">
                  <span
                    class="font-bold leading-tight text-[14px]"
                    :class="localUnmappedCount > 0 ? 'text-rose-900' : 'text-emerald-900'"
                    >Unmapped</span
                  >
                  <span
                    class="text-[10px] font-bold uppercase tracking-wider mt-0.5"
                    :class="localUnmappedCount > 0 ? 'text-rose-500' : 'text-emerald-600'"
                    >Remaining</span
                  >
                </div>
              </div>

              <ul class="space-y-2 list-none font-medium mt-0.5 leading-snug">
                <li class="flex gap-2 items-start">
                  <span class="text-sky-500">●</span
                  ><span>Select active <b>Profile</b> (left wizard).</span>
                </li>
                <li class="flex gap-2 items-start">
                  <span class="text-sky-500">●</span
                  ><span>Use <b>Apply Range</b> for bulk category assignments.</span>
                </li>
                <li class="flex gap-2 items-start">
                  <span class="text-sky-500">●</span
                  ><span>Use <b>Remove Group</b> to revert errors.</span>
                </li>
                <li class="flex gap-2 items-start">
                  <span class="text-emerald-600">●</span
                  ><span class="font-bold text-slate-800"
                    >Always Save Mappings before leaving!</span
                  >
                </li>
              </ul>
            </div>
          </div>
        </aside>
      </div>
    </div>
  </ReportLayout>
</template>
