<script setup>
import { ref, onMounted, watch, computed } from 'vue'
import { storeToRefs } from 'pinia'
import api from '@/services/api'
import { useReportFilterStore } from '@/stores/reportFilters'
import { useAuthStore } from '@/stores/auth'
import ReportLayout from '@/components/ReportLayout.vue'

const filterStore = useReportFilterStore()
const authStore = useAuthStore()

const isAuthorized = computed(() => {
  return authStore.isAdmin || authStore.userReports.includes('income_statement')
})
const { selectedYear, selectedPeriod, displayFormat, showBudget, availableYears, availablePeriods, activePreset, companyId } = storeToRefs(filterStore)

const isLoading = ref(true)
const multiReports = ref({ 
  curr: null, prevMo: null, prevYr: null, 
  q1: null, q2: null, q3: null, q4: null,
  m1: null, m2: null, m3: null, m4: null, m5: null, m6: null,
  m7: null, m8: null, m9: null, m10: null, m11: null, m12: null
})

const isModalOpen = ref(false)
const activeGroupTitle = ref('')
const activeGroupItems = ref([])
const activeGroupHeaders = ref([])

const monthNames = {
  "01": "January", "02": "February", "03": "March", "04": "April",
  "05": "May", "06": "June", "07": "July", "08": "August",
  "09": "September", "10": "October", "11": "November", "12": "December"
}

const formatMoney = (val) => {
  const num = Math.round(parseFloat(val) || 0);
  const formatted = new Intl.NumberFormat('id-ID', { minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(Math.abs(num));
  return num < 0 ? `(${formatted})` : formatted;
}

const isNeg = (val) => (parseFloat(val) || 0) < 0;

const getGroupBudget = (group) => {
  if (!group || !group.items) return 0;
  return group.items.reduce((s, i) => s + (i.end_budget || 0), 0);
}

const format1BudgetTotals = computed(() => {
  const curr = multiReports.value.curr;
  const res = {
    groups: {},
    revenueTotal: 0,
    expensesTotal: 0
  };
  
  if (!curr || !curr.structure) return res;

  const sumSection = (section) => {
    if (!section || !section.groups) return 0;
    let sum = 0;
    Object.entries(section.groups).forEach(([name, g]) => {
      const gBgt = getGroupBudget(g);
      res.groups[name] = gBgt;
      sum += gBgt;
    });
    return sum;
  };

  res.revenueTotal = sumSection(curr.structure.Revenue);
  res.expensesTotal = sumSection(curr.structure.Expenses);

  return res;
});

const fetchLedger = async (y, p) => {
  try {
    const params = { year: y, period: p, preset: activePreset.value, company_id: companyId.value }
    const res = await api.get('/reports/ledger', { params })
    if (res.data.status === 'success') {
      return { 
        structure: res.data.data, 
        netIncome: res.data.net_income || 0,
        netIncomeBudget: res.data.net_income_budget || 0
      }
    }
  } catch (err) { console.error("API Error:", err) }
  return null;
}

const loadData = async () => {
  if (!isAuthorized.value) {
    isLoading.value = false;
    return;
  }
  if (!selectedYear.value || !selectedPeriod.value) return; 
  isLoading.value = true;
  closeModal();

  const y = parseInt(selectedYear.value);
  const p = parseInt(selectedPeriod.value);
  const pStr = p.toString().padStart(2, '0');

  Object.keys(multiReports.value).forEach(k => multiReports.value[k] = null);

  if (displayFormat.value === '1') {
    multiReports.value.curr = await fetchLedger(y, pStr);
  } 
  else if (displayFormat.value === '2') {
    let lastMoP = p - 1; let lastMoY = y;
    if (lastMoP === 0) { lastMoP = 12; lastMoY = y - 1; }
    const [curr, prevMo, prevYr] = await Promise.all([
      fetchLedger(y, pStr),
      fetchLedger(lastMoY.toString(), lastMoP.toString().padStart(2, '0')),
      fetchLedger((y - 1).toString(), pStr)
    ]);
    multiReports.value.curr = curr; multiReports.value.prevMo = prevMo; multiReports.value.prevYr = prevYr;
  } 
  else if (displayFormat.value === '3') {
    const q1p = p >= 3 ? '03' : pStr;
    const q2p = p >= 6 ? '06' : (p > 3 ? pStr : '06');
    const q3p = p >= 9 ? '09' : (p > 6 ? pStr : '09');
    const q4p = p >= 12 ? '12' : (p > 9 ? pStr : '12');

    const [q1, q2, q3, q4] = await Promise.all([
      fetchLedger(y, q1p), fetchLedger(y, q2p), fetchLedger(y, q3p), fetchLedger(y, q4p)
    ]);
    multiReports.value.q1 = q1; multiReports.value.q2 = q2; multiReports.value.q3 = q3; multiReports.value.q4 = q4;
  }
  else if (displayFormat.value === '4') {
    const results = [];
    for (let i = 1; i <= 12; i += 4) {
      const batch = Array.from({ length: Math.min(4, 13 - i) }, (_, j) =>
        fetchLedger(y, (i + j).toString().padStart(2, '0'))
      );
      results.push(...await Promise.all(batch));
    }
    for (let i = 1; i <= 12; i++) { multiReports.value[`m${i}`] = results[i-1]; }
  }
  isLoading.value = false;
}

const getMergedData = (sectionsArr, reportKeys) => {
  const result = {};
  sectionsArr.forEach(sec => {
    const allGroups = new Set();
    reportKeys.forEach(k => {
      if (multiReports.value[k]?.structure[sec]) {
        Object.keys(multiReports.value[k].structure[sec].groups).forEach(g => allGroups.add(g));
      }
    });

    result[sec] = { groups: {}, totals: [], budgetTotals: [] };

    Array.from(allGroups).sort().forEach(grpName => {
      const allItems = new Set();
      reportKeys.forEach(k => {
        if (multiReports.value[k]?.structure[sec]?.groups[grpName]) {
          multiReports.value[k].structure[sec].groups[grpName].items.forEach(i => allItems.add(i.no + '::' + i.name));
        }
      });

      const itemsList = Array.from(allItems).map(str => {
        const [no, name] = str.split('::');
        const balances = reportKeys.map(k => {
          const grp = multiReports.value[k]?.structure[sec]?.groups[grpName];
          return grp?.items.find(x => x.no === no)?.balance || 0;
        });
        const budgets = reportKeys.map(k => {
          const grp = multiReports.value[k]?.structure[sec]?.groups[grpName];
          return grp?.items.find(x => x.no === no)?.end_budget || 0;
        });
        return { no, name, balances, budgets };
      }).sort((a, b) => a.no.localeCompare(b.no));

      const grpBudgets = reportKeys.map((k, idx) => itemsList.reduce((sum, item) => sum + item.budgets[idx], 0));

      result[sec].groups[grpName] = { 
        items: itemsList, 
        totals: reportKeys.map(k => multiReports.value[k]?.structure[sec]?.groups[grpName]?.total || 0),
        budgets: grpBudgets
      };
    });
    
    result[sec].totals = reportKeys.map(k => multiReports.value[k]?.structure[sec]?.total || 0);
    result[sec].budgetTotals = reportKeys.map((k, idx) => {
      return Object.values(result[sec].groups).reduce((sum, grp) => sum + grp.budgets[idx], 0);
    });
  });
  return result;
};

const matrixState = computed(() => {
  if (displayFormat.value === '1') return null;

  const y = parseInt(selectedYear.value);
  const p = parseInt(selectedPeriod.value);
  const lastMoP = p === 1 ? 12 : p - 1; const lastMoY = p === 1 ? y - 1 : y;
  
  let baseHeaders = [];
  let keys = [];
  const wrapClass = 'min-w-max';
  
  if (displayFormat.value === '2') {
    baseHeaders = [
      { label: monthNames[selectedPeriod.value], sub: y.toString() },
      { label: monthNames[lastMoP.toString().padStart(2, '0')], sub: lastMoY.toString() },
      { label: monthNames[selectedPeriod.value], sub: (y - 1).toString() }
    ];
    keys = ['curr', 'prevMo', 'prevYr'];
  } else if (displayFormat.value === '3') {
    const getQSub = (q, cp) => {
      if (q === 1) return cp >= 3 ? 'JAN-MAR' : (cp === 1 ? 'JAN' : 'JAN-FEB');
      if (q === 2) return cp >= 6 ? 'APR-JUN' : (cp === 4 ? 'APR' : (cp === 5 ? 'APR-MAY' : '—'));
      if (q === 3) return cp >= 9 ? 'JUL-SEP' : (cp === 7 ? 'JUL' : (cp === 8 ? 'JUL-AUG' : '—'));
      if (q === 4) return cp >= 12 ? 'OCT-DEC' : (cp === 10 ? 'OCT' : (cp === 11 ? 'OCT-NOV' : '—'));
    };
    baseHeaders = [
      { label: 'Q1', sub: getQSub(1, p) }, { label: 'Q2', sub: getQSub(2, p) },
      { label: 'Q3', sub: getQSub(3, p) }, { label: 'Q4', sub: getQSub(4, p) }
    ];
    keys = ['q1', 'q2', 'q3', 'q4'];
  } else if (displayFormat.value === '4') {
    const mths = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    baseHeaders = mths.map((m, i) => ({ label: m, sub: i + 1 <= p ? 'FINAL' : '—' }));
    keys = ['m1', 'm2', 'm3', 'm4', 'm5', 'm6', 'm7', 'm8', 'm9', 'm10', 'm11', 'm12'];
  }

  const columnsCount = showBudget.value ? baseHeaders.length * 2 : baseHeaders.length;
  const columnWidth = showBudget.value ? 'minmax(110px, 1fr)' : 'minmax(130px, 1fr)';

  return {
    gridStyle: `grid-template-columns: minmax(200px, 300px) repeat(${columnsCount}, ${columnWidth});`,
    wrapperClass: wrapClass,
    headers: baseHeaders,
    data: getMergedData(['Revenue', 'Expenses'], keys),
    netIncomes: keys.map(k => multiReports.value[k]?.netIncome || 0),
    netIncomeBudgets: keys.map(k => multiReports.value[k]?.netIncomeBudget || 0)
  };
});

const openModal = (groupName, items, headers = []) => {
  activeGroupTitle.value = groupName;
  activeGroupItems.value = items;
  activeGroupHeaders.value = headers;
  isModalOpen.value = true;
}

const closeModal = () => {
  isModalOpen.value = false;
  setTimeout(() => { activeGroupItems.value = []; activeGroupTitle.value = ''; }, 200);
}

watch([selectedYear, selectedPeriod, displayFormat, showBudget, activePreset, companyId], loadData);
onMounted(async () => { await filterStore.fetchFilters(); loadData(); });
</script>

<template>
  <ReportLayout 
    title="Income Statement Report" 
    :subtitle="`As of ${monthNames[selectedPeriod]} ${selectedYear}`"
  >
    <template #controls>
      <div v-if="isAuthorized && availableYears.length > 0" class="flex items-center justify-start gap-2 w-full">
        
        <div class="flex items-center bg-white border border-sky-200 rounded shadow-sm overflow-hidden shrink-0">
          <select v-model="selectedPeriod" :disabled="displayFormat === '4'" class="bg-transparent text-[13px] font-medium text-slate-700 px-2.5 py-1 focus:outline-none cursor-pointer hover:bg-sky-50 transition-colors disabled:opacity-50 disabled:bg-slate-100">
            <option v-for="p in availablePeriods" :key="p" :value="p">{{ monthNames[p] || p }}</option>
          </select>
          <div class="w-px h-4 bg-sky-200"></div>
          <select v-model="selectedYear" class="bg-transparent text-[13px] font-medium text-slate-700 px-2.5 py-1 focus:outline-none cursor-pointer hover:bg-sky-50 transition-colors">
            <option v-for="y in availableYears" :key="y" :value="y">{{ y }}</option>
          </select>
        </div>

        <div class="flex items-center gap-1.5 bg-white border border-sky-200 rounded px-2.5 py-1 shadow-sm shrink-0">
          <label class="flex items-center gap-1.5 cursor-pointer text-[13px] font-medium text-sky-900 whitespace-nowrap">
            <input type="checkbox" v-model="showBudget" class="w-3.5 h-3.5 text-sky-600 rounded border-sky-300 focus:ring-sky-500 cursor-pointer">
            Budget
          </label>
        </div>

        <select v-model="displayFormat" class="bg-white border border-sky-200 text-[13px] font-medium text-slate-700 px-2.5 py-1 rounded focus:outline-none focus:border-sky-500 cursor-pointer transition-colors shadow-sm shrink-0">
          <option value="1">Standard View</option>
          <option value="2">Detail Comparison</option>
          <option value="3">Quarterly Report</option>
          <option value="4">Yearly Report</option>
        </select>
        
      </div>
    </template>

    <!-- Access Restricted View -->
    <div v-if="!isAuthorized" class="flex-1 flex flex-col items-center justify-center py-20 px-4 text-center">
      <div class="max-w-md w-full bg-white/85 backdrop-blur-md rounded-2xl shadow-xl border border-slate-200/60 p-8 space-y-6 animate-slide-up">
        <div class="w-16 h-16 bg-rose-50 border border-rose-100 rounded-full flex items-center justify-center mx-auto text-rose-500 shadow-inner">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-8 h-8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M16.5 10.5V6.75a4.5 4.5 0 10-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 002.25-2.25v-6.75a2.25 2.25 0 00-2.25-2.25H6.75a2.25 2.25 0 00-2.25 2.25v6.75a2.25 2.25 0 002.25 2.25z" />
          </svg>
        </div>
        <div class="space-y-2">
          <h3 class="text-xl font-black text-slate-800 tracking-tight">Access Restricted</h3>
          <p class="text-sm text-slate-500 font-medium leading-relaxed">
            You do not have permission to view the Income Statement Report. Please contact your system administrator to request access.
          </p>
        </div>
        <div class="pt-2">
          <RouterLink to="/dashboard" class="inline-flex items-center gap-2 px-5 py-2.5 bg-sky-600 hover:bg-sky-700 text-white text-sm font-bold rounded-xl shadow-md hover:shadow-sky-500/20 active:scale-95 transition-all duration-200">
            Back to Dashboard
          </RouterLink>
        </div>
      </div>
    </div>

    <div v-else-if="isLoading" class="flex flex-col items-center justify-center flex-1 min-h-0 w-full h-full">
      <div class="w-10 h-10 border-4 border-sky-100 border-t-sky-600 rounded-full animate-spin"></div>
      <p class="text-sky-600 font-medium animate-pulse text-sm mt-4">Calculating Income Statement...</p>
    </div>

    <!-- ================= FORMAT 1: STANDARD ================= -->
    <div v-else-if="displayFormat === '1' && multiReports.curr" class="max-w-screen-2xl mx-auto px-4 sm:px-6 lg:px-8 py-6 w-full flex-1 min-h-0 lg:w-3/4 2xl:w-2/3">
      <div class="relative bg-white/90 backdrop-blur-md rounded-xl shadow-sm border border-white/50 flex flex-col h-full overflow-hidden w-full">
      
        <div class="px-5 py-4 border-b border-slate-200/60 bg-white/50 shrink-0 flex justify-between items-center relative z-10">
          <h3 class="text-sky-950 font-black text-lg uppercase tracking-tight">Profit & Loss Statement</h3>
          <div class="flex items-center gap-4 shrink-0">
            <span class="w-[110px] xl:w-[130px] text-right text-sm font-bold text-sky-600 uppercase">Actual</span>
            <span v-if="showBudget" class="w-[110px] xl:w-[130px] text-right text-sm font-bold text-sky-600 uppercase border-l border-sky-200 pl-4">Budget</span>
          </div>
        </div>
        
        <div class="grow overflow-y-auto custom-scrollbar p-0 relative z-10">
          <div class="bg-slate-50/80 border-b border-slate-100 px-5 py-2">
            <span class="text-[14px] font-bold text-sky-800 uppercase tracking-wide">Revenue</span>
          </div>
          <div class="flex flex-col">
            <div v-for="(group, name) in multiReports.curr.structure.Revenue.groups" :key="name" @click="openModal(name, group.items)" class="flex justify-between items-center py-1.5 px-5 hover:bg-white/60 border-b border-slate-100 cursor-pointer transition-colors group/item">
              <span class="text-[13px] text-slate-700 tracking-tight group-hover/item:text-sky-700 font-semibold flex-1 pr-4 whitespace-normal break-words">{{ name }}</span>
              <div class="flex items-center gap-4 shrink-0">
                <div class="flex justify-end items-center w-[110px] xl:w-[130px]">
                  <strong :class="['text-[13.5px] font-semibold tracking-tight whitespace-nowrap text-right', isNeg(group.total) ? 'text-red-600' : 'text-slate-800']">{{ formatMoney(group.total) }}</strong>
                </div>
                <div v-if="showBudget" class="flex justify-end items-center w-[110px] xl:w-[130px] border-l border-slate-200 pl-4">
                  <strong :class="['text-[13.5px] font-semibold tracking-tight whitespace-nowrap text-right', isNeg(format1BudgetTotals.groups[name]) ? 'text-red-600' : 'text-slate-500']">{{ formatMoney(format1BudgetTotals.groups[name]) }}</strong>
                </div>
              </div>
            </div>
          </div>
          
          <!-- Revenue Subtotal -->
          <div class="bg-white/60 px-5 py-3 border-t-[3px] border-double border-slate-300 flex justify-between items-center mb-3">
            <span class="font-bold text-slate-900 uppercase text-[14px] tracking-wide flex-1 pr-4 whitespace-normal break-words">Total Revenue</span>
            <div class="flex items-center gap-4 shrink-0">
              <div class="flex justify-end items-center w-[110px] xl:w-[130px]">
                  <span :class="['font-bold text-[16px] tracking-tight whitespace-nowrap text-right', isNeg(multiReports.curr.structure.Revenue.total) ? 'text-red-600' : 'text-slate-900']">{{ formatMoney(multiReports.curr.structure.Revenue.total) }}</span>
              </div>
              <div v-if="showBudget" class="flex justify-end items-center w-[110px] xl:w-[130px] border-l border-sky-200 pl-4">
                  <span :class="['font-bold text-[16px] tracking-tight whitespace-nowrap text-right', isNeg(format1BudgetTotals.revenueTotal) ? 'text-red-600' : 'text-slate-700']">
                    {{ formatMoney(format1BudgetTotals.revenueTotal) }}
                  </span>
              </div>
            </div>
          </div>
          
          <div class="bg-slate-50/80 border-y border-slate-100 px-5 py-2">
            <span class="text-[14px] font-bold text-sky-800 uppercase tracking-wide">Expenses</span>
          </div>
          <div class="flex flex-col">
            <div v-for="(group, name) in multiReports.curr.structure.Expenses.groups" :key="name" @click="openModal(name, group.items)" class="flex justify-between items-center py-1.5 px-5 hover:bg-white/60 border-b border-slate-100 cursor-pointer transition-colors group/item">
              <span class="text-[13px] text-slate-700 tracking-tight group-hover/item:text-sky-700 font-semibold flex-1 pr-4 whitespace-normal break-words">{{ name }}</span>
              <div class="flex items-center gap-4 shrink-0">
                <div class="flex justify-end items-center w-[110px] xl:w-[130px]">
                  <strong :class="['text-[13.5px] font-semibold tracking-tight whitespace-nowrap text-right', isNeg(group.total) ? 'text-red-600' : 'text-slate-800']">{{ formatMoney(group.total) }}</strong>
                </div>
                <div v-if="showBudget" class="flex justify-end items-center w-[110px] xl:w-[130px] border-l border-slate-200 pl-4">
                  <strong :class="['text-[13.5px] font-semibold tracking-tight whitespace-nowrap text-right', isNeg(format1BudgetTotals.groups[name]) ? 'text-red-600' : 'text-slate-500']">{{ formatMoney(format1BudgetTotals.groups[name]) }}</strong>
                </div>
              </div>
            </div>
          </div>
          
          <!-- Expenses Subtotal -->
          <div class="bg-white/60 px-5 py-3 border-t-[3px] border-double border-slate-300 flex justify-between items-center mb-1">
            <span class="font-bold text-slate-900 uppercase text-[14px] tracking-wide flex-1 pr-4 whitespace-normal break-words">Total Expenses</span>
            <div class="flex items-center gap-4 shrink-0">
              <div class="flex justify-end items-center w-[110px] xl:w-[130px]">
                  <span :class="['font-bold text-[16px] tracking-tight whitespace-nowrap text-right', isNeg(multiReports.curr.structure.Expenses.total) ? 'text-red-600' : 'text-slate-900']">{{ formatMoney(multiReports.curr.structure.Expenses.total) }}</span>
              </div>
              <div v-if="showBudget" class="flex justify-end items-center w-[110px] xl:w-[130px] border-l border-sky-200 pl-4">
                  <span :class="['font-bold text-[16px] tracking-tight whitespace-nowrap text-right', isNeg(format1BudgetTotals.expensesTotal) ? 'text-red-600' : 'text-slate-700']">
                    {{ formatMoney(format1BudgetTotals.expensesTotal) }}
                  </span>
              </div>
            </div>
          </div>
        </div>
        
        <!-- Net Income Grand Total -->
        <div class="bg-sky-50/50 px-5 py-5 border-t-[3px] border-double border-sky-300 shrink-0 flex justify-between items-center relative z-10">
          <span class="font-black text-[15px] text-sky-950 uppercase tracking-wider flex-1 pr-4 whitespace-normal break-words">Net Income</span>
          <div class="flex items-center gap-4 shrink-0">
            <div class="flex justify-end items-center w-[110px] xl:w-[130px]">
              <span :class="['font-black text-xl tracking-tight whitespace-nowrap text-right', isNeg(multiReports.curr.netIncome) ? 'text-red-600' : 'text-sky-950']">{{ formatMoney(multiReports.curr.netIncome) }}</span>
            </div>
            <div v-if="showBudget" class="flex justify-end items-center w-[110px] xl:w-[130px] border-l border-sky-200 pl-4">
              <span :class="['font-black text-xl tracking-tight whitespace-nowrap text-right', isNeg(multiReports.curr.netIncomeBudget) ? 'text-red-600' : 'text-sky-800']">{{ formatMoney(multiReports.curr.netIncomeBudget) }}</span>
            </div>
          </div>
        </div>

      </div>
    </div>

    <!-- ================= FORMAT 2, 3 & 4: UNIFIED MATRIX VIEWS ================= -->
    <div v-else-if="['2', '3', '4'].includes(displayFormat) && matrixState" class="max-w-screen-2xl mx-auto px-4 sm:px-6 lg:px-8 py-6 flex flex-col flex-1 min-h-0 relative z-10 w-full">
      
      <!-- SINGLE MASTER SCROLL CONTAINER -->
      <div class="overflow-auto custom-scrollbar rounded-xl shadow-sm border border-slate-200/60 bg-white/90 backdrop-blur-md flex-1 min-h-0 w-full relative">
        <div :class="['flex flex-col min-w-max min-h-full', matrixState.wrapperClass]">
          
          <!-- STICKY TOP HEADER -->
          <div class="grid gap-4 text-sky-900 font-bold text-xs uppercase tracking-wider pr-6 items-stretch" :style="matrixState.gridStyle">
            <div class="sticky top-0 left-0 z-[50] bg-white/95 backdrop-blur-md border-b-2 border-slate-300 shadow-[4px_4px_8px_-4px_rgba(0,0,0,0.1)] pl-6 py-3 flex items-center h-full">Account</div>
            <template v-for="(hd, i) in matrixState.headers" :key="i">
              <div v-if="!showBudget" class="sticky top-0 z-[40] bg-white/95 backdrop-blur-md border-b-2 border-slate-300 flex flex-col justify-center text-center whitespace-nowrap py-3 shadow-[0_4px_6px_-1px_rgba(0,0,0,0.05)]">{{ hd.label }} <span class="text-slate-500 block text-[10px] mt-0.5">{{ hd.sub }}</span></div>
              <template v-else>
                <div class="sticky top-0 z-[40] bg-white/95 backdrop-blur-md border-b-2 border-slate-300 flex flex-col justify-center text-center whitespace-nowrap py-3 shadow-[0_4px_6px_-1px_rgba(0,0,0,0.05)]">{{ hd.label }} <span class="text-slate-500 block text-[10px] mt-0.5">{{ hd.sub }} (ACT)</span></div>
                <div class="sticky top-0 z-[40] bg-white/95 backdrop-blur-md border-b-2 border-slate-300 flex flex-col justify-center text-center whitespace-nowrap py-3 shadow-[0_4px_6px_-1px_rgba(0,0,0,0.05)]">{{ hd.label }} <span class="text-sky-600 block text-[10px] mt-0.5">{{ hd.sub }} (BGT)</span></div>
              </template>
            </template>
          </div>
          
          <!-- SCROLLABLE BODY -->
          <div class="flex flex-col grow p-0">
            <div v-for="(sectionData, secName) in matrixState.data" :key="secName">
              
              <div class="bg-slate-100/90 grid gap-4 pr-6 border-b border-slate-200 z-[10] shadow-[4px_0_8px_-4px_rgba(0,0,0,0.05)] items-stretch" :style="matrixState.gridStyle">
                <div class="font-black text-[18px] text-sky-950 uppercase tracking-tight pl-6 py-3 sticky left-0 z-20 bg-slate-100 shadow-[4px_0_8px_-4px_rgba(0,0,0,0.1)] h-full flex items-center">
                  {{ secName }}
                </div>
              </div>
              
              <div class="flex flex-col">
                <div v-for="(group, grpName) in sectionData.groups" :key="grpName">
                  <div @click="openModal(grpName, group.items, matrixState.headers)" class="grid gap-4 pr-6 border-b border-slate-100 hover:bg-slate-50/80 cursor-pointer transition-colors items-stretch group/item" :style="matrixState.gridStyle">
                    <!-- Sticky Left Column inside rows -->
                    <div class="font-semibold text-[13px] text-slate-700 tracking-tight pr-4 pl-6 group-hover/item:text-sky-800 sticky left-0 z-[20] bg-white/95 backdrop-blur-md shadow-[4px_0_8px_-4px_rgba(0,0,0,0.1)] group-hover/item:bg-slate-50/95 py-2 flex items-center whitespace-normal break-words h-full">
                      {{ grpName }}
                    </div>
                    
                    <template v-for="(tot, i) in group.totals" :key="i">
                      <div class="flex justify-end items-center w-full py-2 px-1">
                        <span :class="['text-[13.5px] whitespace-nowrap font-semibold tracking-tight text-right w-full', isNeg(tot) ? 'text-red-600' : 'text-slate-800']">{{ formatMoney(tot) }}</span>
                      </div>
                      <div v-if="showBudget" class="flex justify-end items-center w-full bg-slate-50/50 px-2 py-2 rounded">
                        <span :class="['text-[13.5px] whitespace-nowrap font-medium tracking-tight text-right w-full', isNeg(group.budgets[i]) ? 'text-red-600' : 'text-slate-600']">{{ formatMoney(group.budgets[i]) }}</span>
                      </div>
                    </template>
                  </div>
                </div>
                
                <div v-if="secName === 'Equity'" class="grid gap-4 pr-6 border-b border-slate-100 items-stretch hover:bg-slate-50/80 transition-colors group/item" :style="matrixState.gridStyle">
                    <div class="text-sky-700 font-semibold italic text-[13px] tracking-tight pr-4 pl-6 sticky left-0 z-[20] bg-white/95 backdrop-blur-md shadow-[4px_0_8px_-4px_rgba(0,0,0,0.1)] group-hover/item:bg-slate-50/95 py-2 flex items-center whitespace-normal break-words h-full">
                      Current Year Earnings
                    </div>
                    <template v-for="(ni, i) in matrixState.netIncomes" :key="i">
                      <div class="flex justify-end items-center w-full py-2 px-1">
                        <span :class="['text-[13.5px] whitespace-nowrap font-semibold tracking-tight text-right w-full', isNeg(ni) ? 'text-red-600' : 'text-slate-800']">{{ formatMoney(ni) }}</span>
                      </div>
                      <div v-if="showBudget" class="flex justify-end items-center w-full bg-slate-50/50 px-2 py-2 rounded">
                        <span :class="['text-[13.5px] whitespace-nowrap font-medium tracking-tight text-right w-full', isNeg(matrixState.netIncomeBudgets[i]) ? 'text-red-600' : 'text-slate-500']">{{ formatMoney(matrixState.netIncomeBudgets[i]) }}</span>
                      </div>
                    </template>
                </div>

                <div class="grid gap-4 pr-6 bg-slate-50/60 border-t-[3px] border-double border-slate-300 items-stretch group/item" :style="matrixState.gridStyle">
                  <div class="font-bold text-[14px] text-slate-900 uppercase pr-4 pl-6 sticky left-0 z-[20] bg-slate-50/95 backdrop-blur-md shadow-[4px_0_8px_-4px_rgba(0,0,0,0.1)] py-3 flex items-center whitespace-normal break-words h-full">
                    Total {{ secName }}
                  </div>
                  <template v-for="(tot, i) in sectionData.totals" :key="i">
                    <div class="flex justify-end items-center w-full py-3 px-1">
                      <span :class="['text-[16px] font-bold tracking-tight whitespace-nowrap text-right w-full', isNeg(secName === 'Equity' ? tot + matrixState.netIncomes[i] : tot) ? 'text-red-600' : 'text-sky-950']">
                        {{ formatMoney(secName === 'Equity' ? tot + matrixState.netIncomes[i] : tot) }}
                      </span>
                    </div>
                    <div v-if="showBudget" class="flex justify-end items-center w-full bg-white/60 px-2 border border-sky-100/50 py-3 rounded">
                      <span :class="['text-[16px] font-bold tracking-tight whitespace-nowrap text-right w-full', isNeg(secName === 'Equity' ? sectionData.budgetTotals[i] + matrixState.netIncomeBudgets[i] : sectionData.budgetTotals[i]) ? 'text-red-600' : 'text-sky-800']">
                        {{ formatMoney(secName === 'Equity' ? sectionData.budgetTotals[i] + matrixState.netIncomeBudgets[i] : sectionData.budgetTotals[i]) }}
                      </span>
                    </div>
                  </template>
                </div>
              </div>
            </div>
          </div>

          <!-- STICKY BOTTOM FOOTER -->
          <div class="grid gap-4 items-stretch pr-6 mt-auto" :style="matrixState.gridStyle">
            <div class="sticky bottom-0 left-0 z-[50] bg-sky-50/95 backdrop-blur-md shadow-[4px_-4px_8px_-4px_rgba(0,0,0,0.1)] border-t-[3px] border-double border-sky-400 font-black text-[15px] uppercase text-sky-950 tracking-wider pr-4 pl-6 py-4 flex items-center whitespace-normal break-words h-full">
              Net Income
            </div>
            <template v-for="(ni, i) in matrixState.netIncomes" :key="i">
              <div class="sticky bottom-0 z-[40] bg-sky-50/95 backdrop-blur-md border-t-[3px] border-double border-sky-400 shadow-[0_-4px_6px_-1px_rgba(0,0,0,0.05)] flex justify-end items-center w-full py-4 px-1">
                <span :class="['text-xl font-black tracking-tight whitespace-nowrap text-right w-full', isNeg(ni) ? 'text-red-600' : 'text-sky-950']">
                  {{ formatMoney(ni) }}
                </span>
              </div>
              <div v-if="showBudget" class="sticky bottom-0 z-[40] bg-sky-50/95 backdrop-blur-md border-t-[3px] border-double border-sky-400 shadow-[0_-4px_6px_-1px_rgba(0,0,0,0.05)] flex justify-end items-center w-full px-2 py-4 border-r border-sky-200">
                <span :class="['text-xl font-black tracking-tight whitespace-nowrap text-right w-full', isNeg(matrixState.netIncomeBudgets[i]) ? 'text-red-600' : 'text-sky-800']">
                  {{ formatMoney(matrixState.netIncomeBudgets[i]) }}
                </span>
              </div>
            </template>
          </div>

        </div>
      </div>
    </div>

    <!-- DRILL-DOWN MODAL -->
    <transition name="fade">
      <div v-if="isModalOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
        <div class="absolute inset-0 bg-slate-900/60 backdrop-blur-sm" @click="closeModal"></div>
        <div class="bg-white/95 backdrop-blur-md w-full max-w-5xl rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh] animate-slide-up border border-white/50">
          <div class="flex justify-between items-center px-6 py-4 bg-white/50 border-b border-slate-200/60 shrink-0 relative z-10">
            <div>
              <h3 class="text-xl font-black text-sky-950 tracking-tight">{{ activeGroupTitle }}</h3>
              <p class="text-xs text-sky-600 mt-0.5 font-bold uppercase tracking-widest">Account Breakdown</p>
            </div>
            <button @click="closeModal" class="text-slate-400 hover:text-rose-500 transition-colors bg-white hover:bg-rose-50 p-2 rounded-lg border border-slate-200 shadow-sm">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>
          <div class="overflow-auto grow p-0 custom-scrollbar relative z-10">
            <table class="w-full text-left border-collapse relative z-10">
              <thead class="sticky top-0 bg-white/90 backdrop-blur-sm z-10 border-b border-slate-200/60 shadow-sm">
                <tr class="text-xs font-bold text-sky-800 uppercase tracking-wider">
                  <th class="px-6 py-3 min-w-[320px]">Account</th>
                  <template v-if="activeGroupHeaders.length === 0">
                    <th class="px-6 py-3 text-right whitespace-nowrap min-w-48">Actual Balance</th>
                    <th v-if="showBudget" class="px-6 py-3 text-right whitespace-nowrap min-w-48 border-l border-slate-200/60 bg-slate-50/50">Budget Balance</th>
                  </template>
                  <template v-else v-for="(hd, i) in activeGroupHeaders" :key="i">
                    <th class="px-6 py-3 text-right whitespace-nowrap min-w-48">{{ hd.label }} <span class="text-slate-500 block text-[10px] font-medium">{{ hd.sub }} <span v-if="showBudget" class="ml-1">(ACT)</span></span></th>
                    <th v-if="showBudget" class="px-6 py-3 text-right whitespace-nowrap min-w-48 border-l border-slate-200/60 bg-slate-50/50">{{ hd.label }} <span class="text-sky-500 block text-[10px] font-medium">{{ hd.sub }} (BGT)</span></th>
                  </template>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 relative z-10">
                <tr v-for="item in activeGroupItems" :key="item.no" class="hover:bg-white/60 transition-colors">
                  <td class="px-6 py-2.5 text-[13px] text-slate-700 tracking-tight whitespace-normal break-words">
                    <div class="flex items-start">
                      <span class="font-mono text-slate-400 mr-2 shrink-0">{{ item.no }}</span>
                      <span class="font-medium flex-1">{{ item.name }}</span>
                    </div>
                  </td>
                  <template v-if="item.balance !== undefined">
                    <td class="px-6 py-2.5">
                      <div class="flex justify-end items-center w-full">
                        <span :class="['text-[13.5px] font-semibold tracking-tight whitespace-nowrap text-right w-full', isNeg(item.balance) ? 'text-red-500' : 'text-slate-900']">{{ formatMoney(item.balance) }}</span>
                      </div>
                    </td>
                    <td v-if="showBudget" class="px-6 py-2.5 border-l border-slate-100 bg-slate-50/30">
                      <div class="flex justify-end items-center w-full">
                        <span :class="['text-[13.5px] font-medium tracking-tight whitespace-nowrap text-right w-full', isNeg(item.end_budget) ? 'text-red-500' : 'text-slate-600']">{{ formatMoney(item.end_budget || 0) }}</span>
                      </div>
                    </td>
                  </template>
                  <template v-else v-for="(bal, i) in item.balances" :key="i">
                    <td class="px-6 py-2.5">
                      <div class="flex justify-end items-center w-full">
                        <span :class="['text-[13.5px] font-semibold tracking-tight whitespace-nowrap text-right w-full', isNeg(bal) ? 'text-red-500' : 'text-slate-900']">{{ formatMoney(bal) }}</span>
                      </div>
                    </td>
                    <td v-if="showBudget" class="px-6 py-2.5 border-l border-slate-100 bg-slate-50/30">
                      <div class="flex justify-end items-center w-full">
                        <span :class="['text-[13.5px] font-medium tracking-tight whitespace-nowrap text-right w-full', isNeg(item.budgets[i]) ? 'text-red-500' : 'text-slate-600']">{{ formatMoney(item.budgets[i]) }}</span>
                      </div>
                    </td>
                  </template>
                </tr>
              </tbody>
            </table>
          </div>
          <div class="px-8 py-4 bg-white/60 border-t border-slate-200/60 flex justify-end shrink-0 rounded-b-xl relative z-10">
            <button @click="closeModal" class="bg-sky-600 hover:bg-sky-700 text-white px-6 py-2 rounded-xl text-sm font-bold transition-colors shadow-sm tracking-wide">Close Breakdown</button>
          </div>
        </div>
      </div>
    </transition>

  </ReportLayout>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.animate-slide-up { animation: slideUp 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes slideUp { from { opacity: 0; transform: translateY(30px) scale(0.97); } to { opacity: 1; transform: translateY(0) scale(1); } }
</style>
