<script setup>
import { computed } from 'vue'

const props = defineProps({
  rawLedgerData: { type: Object, required: true },
  formatMoney: { type: Function, required: true },
})

const kpiData = computed(() => {
  const current = props.rawLedgerData?.current
  const previous = props.rawLedgerData?.previous

  const currData = current?.data
  const revenue = currData?.Revenue?.Total || currData?.Revenue?.total || 0
  const expense = currData?.Expenses?.Total || currData?.Expenses?.total || 0

  const thisMonthNetIncome = current?.net_income || 0
  const lastMonthNetIncome = previous?.net_income || 0

  const netIncome = thisMonthNetIncome

  let netGrowth = 0
  if (lastMonthNetIncome !== 0) {
    netGrowth = ((thisMonthNetIncome - lastMonthNetIncome) / Math.abs(lastMonthNetIncome)) * 100
  } else if (thisMonthNetIncome !== 0) {
    netGrowth = thisMonthNetIncome > 0 ? 100 : -100
  }

  return {
    revenue,
    expense,
    netIncome,
    netGrowth,
  }
})
</script>

<template>
  <div
    class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 w-full animate-slide-up"
  >
    <!-- CARD: REVENUE -->
    <div
      class="group relative bg-white/80 backdrop-blur-md rounded-2xl shadow-sm border border-slate-100 hover:border-sky-300 p-3.5 sm:p-4 overflow-hidden transition-all duration-300 hover:-translate-y-0.5 hover:shadow-md flex flex-col justify-between"
    >
      <div>
        <div class="flex items-center justify-between mb-2">
          <p class="text-[10px] sm:text-xs font-bold text-slate-500 uppercase tracking-wider">
            Revenue
          </p>
          <div
            class="w-7 h-7 rounded-lg bg-sky-50 flex items-center justify-center text-sky-500 group-hover:bg-sky-500 group-hover:text-white transition-colors duration-300"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              stroke-width="2"
              stroke="currentColor"
              class="w-3.5 h-3.5"
            >
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                d="M2.25 18 9 11.25l4.306 4.306a11.95 11.95 0 0 1 5.814-5.518l2.74-1.22m0 0-5.94-2.281m5.94 2.28-2.28 5.941"
              />
            </svg>
          </div>
        </div>
        <h3
          class="text-lg sm:text-xl md:text-2xl font-black text-slate-800 tracking-tight truncate"
        >
          Rp {{ props.formatMoney(kpiData.revenue) }}
        </h3>
      </div>
    </div>

    <!-- CARD: EXPENSES -->
    <div
      class="group relative bg-white/80 backdrop-blur-md rounded-2xl shadow-sm border border-slate-100 hover:border-rose-300 p-3.5 sm:p-4 overflow-hidden transition-all duration-300 hover:-translate-y-0.5 hover:shadow-md flex flex-col justify-between"
    >
      <div>
        <div class="flex items-center justify-between mb-2">
          <p class="text-[10px] sm:text-xs font-bold text-slate-500 uppercase tracking-wider">
            Expenses
          </p>
          <div
            class="w-7 h-7 rounded-lg bg-rose-50 flex items-center justify-center text-rose-500 group-hover:bg-rose-500 group-hover:text-white transition-colors duration-300"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              stroke-width="2"
              stroke="currentColor"
              class="w-3.5 h-3.5"
            >
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                d="M2.25 6 9 12.75l4.306-4.307a11.95 11.95 0 0 1 5.814 5.519l2.74 1.22m0 0-5.94 2.28m5.94-2.28-2.28 5.941"
              />
            </svg>
          </div>
        </div>
        <h3
          class="text-lg sm:text-xl md:text-2xl font-black text-slate-800 tracking-tight truncate"
        >
          Rp {{ props.formatMoney(kpiData.expense) }}
        </h3>
      </div>
    </div>

    <!-- CARD: INCOME OR LOSS -->
    <div
      :class="[
        'group relative bg-white/80 backdrop-blur-md rounded-2xl shadow-sm border p-3.5 sm:p-4 overflow-hidden transition-all duration-300 hover:-translate-y-0.5 hover:shadow-md flex flex-col justify-between',
        kpiData.netIncome < 0
          ? 'border-slate-100 hover:border-red-300'
          : 'border-slate-100 hover:border-emerald-300',
      ]"
    >
      <div>
        <div class="flex items-center justify-between mb-2">
          <p class="text-[10px] sm:text-xs font-bold text-slate-500 uppercase tracking-wider">
            {{ kpiData.netIncome < 0 ? 'Loss' : 'Income' }}
          </p>
          <div
            :class="[
              'w-7 h-7 rounded-lg flex items-center justify-center transition-colors duration-300',
              kpiData.netIncome < 0
                ? 'bg-red-50 text-red-500 group-hover:bg-red-500 group-hover:text-white'
                : 'bg-emerald-50 text-emerald-500 group-hover:bg-emerald-500 group-hover:text-white',
            ]"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              stroke-width="2"
              stroke="currentColor"
              class="w-3.5 h-3.5"
            >
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                d="M12 6v12m-3-2.818.879.659c1.171.879 3.07.879 4.242 0 1.172-.879 1.172-2.303 0-3.182C13.536 12.219 12.768 12 12 12c-.725 0-1.45-.22-2.003-.659-1.106-.879-1.106-2.303 0-3.182s2.9-.879 4.006 0l.415.33M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
              />
            </svg>
          </div>
        </div>
        <h3
          :class="[
            'text-lg sm:text-xl md:text-2xl font-black tracking-tight truncate',
            kpiData.netIncome < 0 ? 'text-red-600' : 'text-emerald-600',
          ]"
        >
          <span v-if="kpiData.netIncome < 0">-</span>Rp
          {{ props.formatMoney(Math.abs(kpiData.netIncome)) }}
        </h3>
      </div>
    </div>

    <!-- CARD: NET GROWTH -->
    <div
      :class="[
        'group relative bg-white/80 backdrop-blur-md rounded-2xl shadow-sm border p-3.5 sm:p-4 overflow-hidden transition-all duration-300 hover:-translate-y-0.5 hover:shadow-md flex flex-col justify-between',
        kpiData.netGrowth < 0
          ? 'border-slate-100 hover:border-rose-300'
          : 'border-slate-100 hover:border-purple-300',
      ]"
    >
      <div>
        <div class="flex items-center justify-between mb-2">
          <p class="text-[10px] sm:text-xs font-bold text-slate-500 uppercase tracking-wider">
            Net Growth
          </p>
          <div
            :class="[
              'w-7 h-7 rounded-lg flex items-center justify-center transition-colors duration-300',
              kpiData.netGrowth < 0
                ? 'bg-rose-50 text-rose-500 group-hover:bg-rose-500 group-hover:text-white'
                : 'bg-purple-50 text-purple-500 group-hover:bg-purple-500 group-hover:text-white',
            ]"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              stroke-width="2"
              stroke="currentColor"
              class="w-3.5 h-3.5"
            >
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                d="M19.5 12c0-1.232-.046-2.453-.138-3.662a4.006 4.006 0 0 0-3.7-3.7 48.656 48.656 0 0 0-7.324 0 4.006 4.006 0 0 0-3.7 3.7c-.017.22-.032.441-.046.662M19.5 12l3-3m-3 3-3-3M3 12a48.654 48.654 0 0 1 1.04-6.62M3 12l3 3m-3-3-3 3"
              />
            </svg>
          </div>
        </div>
        <h3
          :class="[
            'text-lg sm:text-xl md:text-2xl font-black tracking-tight flex items-center gap-1.5 truncate',
            kpiData.netGrowth < 0 ? 'text-rose-600' : 'text-purple-600',
          ]"
        >
          <span>{{ kpiData.netGrowth >= 0 ? '+' : '' }}{{ kpiData.netGrowth.toFixed(1) }}%</span>

          <!-- TREND ARROW -->
          <span class="text-sm shrink-0">
            <svg
              v-if="kpiData.netGrowth >= 0"
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              stroke-width="3"
              stroke="currentColor"
              class="w-3.5 h-3.5"
            >
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                d="M4.5 19.5l15-15m0 0H8.25m11.25 0v11.25"
              />
            </svg>
            <svg
              v-else
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              stroke-width="3"
              stroke="currentColor"
              class="w-3.5 h-3.5"
            >
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                d="M4.5 4.5l15 15m0 0V8.25m-15 11.25h11.25"
              />
            </svg>
          </span>
        </h3>
      </div>
    </div>
  </div>
</template>
