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
  current: null, previousMonth: null, previousYear: null, 
  quarter1: null, quarter2: null, quarter3: null, quarter4: null,
  month1: null, month2: null, month3: null, month4: null, month5: null, month6: null,
  month7: null, month8: null, month9: null, month10: null, month11: null, month12: null
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
  return group.items.reduce((sum, item) => sum + (item.end_budget || 0), 0);
}

const format1BudgetTotals = computed(() => {
  const currentReport = multiReports.value.current;
  const result = {
    groups: {},
    revenueTotal: 0,
    expensesTotal: 0
  };
  
  if (!currentReport || !currentReport.structure) return result;

  const sumSection = (section) => {
    if (!section || !section.groups) return 0;
    let sum = 0;
    Object.entries(section.groups).forEach(([name, group]) => {
      const groupBudget = getGroupBudget(group);
      result.groups[name] = groupBudget;
      sum += groupBudget;
    });
    return sum;
  };

  result.revenueTotal = sumSection(currentReport.structure.Revenue);
  result.expensesTotal = sumSection(currentReport.structure.Expenses);

  return result;
});

// --- COMPUTE FLAT ROW PAIRS FOR T-FORMAT SIDE-BY-SIDE GRID ---
const tFormatRows = computed(() => {
  const currentReport = multiReports.value.current;
  if (!currentReport || !currentReport.structure) return [];

  const revenueList = [];
  const expensesList = [];

  // Revenue groups
  if (currentReport.structure.Revenue && currentReport.structure.Revenue.groups) {
    Object.entries(currentReport.structure.Revenue.groups).forEach(([name, group]) => {
      revenueList.push({
        type: 'group',
        name,
        total: group.total,
        budget: format1BudgetTotals.value.groups[name] || 0,
        items: group.items
      });
    });
  }

  if (currentReport.structure.Expenses && currentReport.structure.Expenses.groups) {
    Object.entries(currentReport.structure.Expenses.groups).forEach(([name, group]) => {
      expensesList.push({
        type: 'group',
        name,
        total: group.total,
        budget: format1BudgetTotals.value.groups[name] || 0,
        items: group.items
      });
    });
  }

  const rowCount = Math.max(revenueList.length, expensesList.length);
  const rows = [];
  for (let index = 0; index < rowCount; index++) {
    rows.push({
      left: revenueList[index] || null,
      right: expensesList[index] || null
    });
  }
  return rows;
});

// --- COMPUTE FLAT ROWS FOR MATRIX VIEWS ---
const matrixRows = computed(() => {
  if (!matrixState.value) return { rows: [], totalRevRow: null, totalExpRow: null };
  const rows = [];
  let rowIdx = 2; // Row 1 is letter headers

  let revStart = null;
  let revEnd = null;
  let expStart = null;
  let expEnd = null;
  let totalRevRow = null;
  let totalExpRow = null;

  Object.entries(matrixState.value.data).forEach(([sectionName, sectionData]) => {
    // Section Header row
    rows.push({
      type: 'section',
      name: sectionName,
      rowNum: rowIdx++
    });

    const secStartIdx = rowIdx;

    Object.entries(sectionData.groups).forEach(([groupName, group]) => {
      rows.push({
        type: 'group',
        name: groupName,
        totals: group.totals,
        budgets: group.budgets,
        items: group.items,
        section: sectionName,
        rowNum: rowIdx++
      });
    });

    const secEndIdx = rowIdx - 1;

    if (sectionName === 'Revenue') {
      revStart = secStartIdx;
      revEnd = secEndIdx;
    } else if (sectionName === 'Expenses') {
      expStart = secStartIdx;
      expEnd = secEndIdx;
    }

    // Subtotal
    const subtotalRow = rowIdx;
    rows.push({
      type: 'subtotal',
      name: `Total ${sectionName}`,
      totals: sectionData.totals,
      budgetTotals: sectionData.budgetTotals,
      section: sectionName,
      rowNum: rowIdx++,
      start: sectionName === 'Revenue' ? revStart : expStart,
      end: sectionName === 'Revenue' ? revEnd : expEnd
    });

    if (sectionName === 'Revenue') {
      totalRevRow = subtotalRow;
    } else if (sectionName === 'Expenses') {
      totalExpRow = subtotalRow;
    }
  });

  return {
    rows,
    totalRevRow,
    totalExpRow
  };
});

const fetchLedger = async (year, period) => {
  try {
    const params = { year, period, preset: activePreset.value, company_id: companyId.value }
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

  const year = parseInt(selectedYear.value);
  const period = parseInt(selectedPeriod.value);
  const periodString = period.toString().padStart(2, '0');

  Object.keys(multiReports.value).forEach(key => multiReports.value[key] = null);

  if (displayFormat.value === '1') {
    multiReports.value.current = await fetchLedger(year, periodString);
  } 
  else if (displayFormat.value === '2') {
    let lastPeriod = period - 1; let lastYear = year;
    if (lastPeriod === 0) { lastPeriod = 12; lastYear = year - 1; }
    const [current, previousMonth, previousYear] = await Promise.all([
      fetchLedger(year, periodString),
      fetchLedger(lastYear.toString(), lastPeriod.toString().padStart(2, '0')),
      fetchLedger((year - 1).toString(), periodString)
    ]);
    multiReports.value.current = current; 
    multiReports.value.previousMonth = previousMonth; 
    multiReports.value.previousYear = previousYear;
  } 
  else if (displayFormat.value === '3') {
    const quarter1Period = period >= 3 ? '03' : periodString;
    const quarter2Period = period >= 6 ? '06' : (period > 3 ? periodString : '06');
    const quarter3Period = period >= 9 ? '09' : (period > 6 ? periodString : '09');
    const quarter4Period = period >= 12 ? '12' : (period > 9 ? periodString : '12');

    const [quarter1, quarter2, quarter3, quarter4] = await Promise.all([
      fetchLedger(year, quarter1Period), 
      fetchLedger(year, quarter2Period), 
      fetchLedger(year, quarter3Period), 
      fetchLedger(year, quarter4Period)
    ]);
    multiReports.value.quarter1 = quarter1; 
    multiReports.value.quarter2 = quarter2; 
    multiReports.value.quarter3 = quarter3; 
    multiReports.value.quarter4 = quarter4;
  }
  else if (displayFormat.value === '4') {
    const results = [];
    for (let index = 1; index <= 12; index += 4) {
      const batch = Array.from({ length: Math.min(4, 13 - index) }, (_, itemIndex) =>
        fetchLedger(year, (index + itemIndex).toString().padStart(2, '0'))
      );
      results.push(...await Promise.all(batch));
    }
    for (let index = 1; index <= 12; index++) { 
      multiReports.value[`month${index}`] = results[index - 1]; 
    }
  }
  isLoading.value = false;
}

const getMergedData = (sectionsArray, reportKeys) => {
  const result = {};
  sectionsArray.forEach(section => {
    const allGroups = new Set();
    reportKeys.forEach(key => {
      if (multiReports.value[key]?.structure[section]) {
        Object.keys(multiReports.value[key].structure[section].groups).forEach(group => allGroups.add(group));
      }
    });

    result[section] = { groups: {}, totals: [], budgetTotals: [] };

    Array.from(allGroups).sort().forEach(groupName => {
      const allItems = new Set();
      reportKeys.forEach(key => {
        if (multiReports.value[key]?.structure[section]?.groups[groupName]) {
          multiReports.value[key].structure[section].groups[groupName].items.forEach(item => allItems.add(item.no + '::' + item.name));
        }
      });

      const itemsList = Array.from(allItems).map(str => {
        const [no, name] = str.split('::');
        const balances = reportKeys.map(key => {
          const group = multiReports.value[key]?.structure[section]?.groups[groupName];
          return group?.items.find(x => x.no === no)?.balance || 0;
        });
        const budgets = reportKeys.map(key => {
          const group = multiReports.value[key]?.structure[section]?.groups[groupName];
          return group?.items.find(x => x.no === no)?.end_budget || 0;
        });
        return { no, name, balances, budgets };
      }).sort((a, b) => a.no.localeCompare(b.no));

      const groupBudgets = reportKeys.map((key, index) => itemsList.reduce((sum, item) => sum + item.budgets[index], 0));

      result[section].groups[groupName] = { 
        items: itemsList, 
        totals: reportKeys.map(key => multiReports.value[key]?.structure[section]?.groups[groupName]?.total || 0),
        budgets: groupBudgets
      };
    });
    
    result[section].totals = reportKeys.map(key => multiReports.value[key]?.structure[section]?.total || 0);
    result[section].budgetTotals = reportKeys.map((key, index) => {
      return Object.values(result[section].groups).reduce((sum, group) => sum + group.budgets[index], 0);
    });
  });
  return result;
};

const matrixState = computed(() => {
  if (displayFormat.value === '1') return null;

  const year = parseInt(selectedYear.value);
  const period = parseInt(selectedPeriod.value);
  const lastPeriod = period === 1 ? 12 : period - 1; 
  const lastYear = period === 1 ? year - 1 : year;
  
  let baseHeaders = [];
  let keys = [];
  const wrapClass = 'min-w-max';
  
  if (displayFormat.value === '2') {
    baseHeaders = [
      { label: `${monthNames[selectedPeriod.value]} ${year}`, sub: 'IDR' },
      { label: `${monthNames[lastPeriod.toString().padStart(2, '0')]} ${lastYear}`, sub: 'IDR' },
      { label: `${monthNames[selectedPeriod.value]} ${year - 1}`, sub: 'IDR' }
    ];
    keys = ['current', 'previousMonth', 'previousYear'];
  } else if (displayFormat.value === '3') {
    const getQuarterSub = (quarter, currentPeriod) => {
      if (quarter === 1) return currentPeriod >= 3 ? 'JAN-MAR' : (currentPeriod === 1 ? 'JAN' : 'JAN-FEB');
      if (quarter === 2) return currentPeriod >= 6 ? 'APR-JUN' : (currentPeriod === 4 ? 'APR' : (currentPeriod === 5 ? 'APR-MAY' : '—'));
      if (quarter === 3) return currentPeriod >= 9 ? 'JUL-SEP' : (currentPeriod === 7 ? 'JUL' : (currentPeriod === 8 ? 'JUL-AUG' : '—'));
      if (quarter === 4) return currentPeriod >= 12 ? 'OCT-DEC' : (currentPeriod === 10 ? 'OCT' : (currentPeriod === 11 ? 'OCT-NOV' : '—'));
    };
    baseHeaders = [
      { label: 'Q1', sub: getQuarterSub(1, period) }, { label: 'Q2', sub: getQuarterSub(2, period) },
      { label: 'Q3', sub: getQuarterSub(3, period) }, { label: 'Q4', sub: getQuarterSub(4, period) }
    ];
    keys = ['quarter1', 'quarter2', 'quarter3', 'quarter4'];
  } else if (displayFormat.value === '4') {
    const months = [
      'January', 'February', 'March', 'April', 'May', 'June', 
      'July', 'August', 'September', 'October', 'November', 'December'
    ];
    baseHeaders = months.map((month) => ({ label: month, sub: 'IDR' }));
    keys = ['month1', 'month2', 'month3', 'month4', 'month5', 'month6', 'month7', 'month8', 'month9', 'month10', 'month11', 'month12'];
  }

  const columnsCount = showBudget.value ? baseHeaders.length * 2 : baseHeaders.length;
  const columnWidth = showBudget.value ? 'minmax(110px, 1fr)' : 'minmax(130px, 1fr)';

  return {
    gridStyle: `grid-template-columns: minmax(200px, 300px) repeat(${columnsCount}, ${columnWidth});`,
    wrapperClass: wrapClass,
    headers: baseHeaders,
    data: getMergedData(['Revenue', 'Expenses'], keys),
    netIncomes: keys.map(key => multiReports.value[key]?.netIncome || 0),
    netIncomeBudgets: keys.map(key => multiReports.value[key]?.netIncomeBudget || 0)
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
      <div v-if="isAuthorized && availableYears.length > 0" class="flex items-center justify-start gap-1.5 w-full">
        
        <div class="flex items-center bg-white border border-sky-200 rounded shadow-sm overflow-hidden shrink-0">
          <select v-model="selectedPeriod" :disabled="displayFormat === '4'" class="bg-transparent text-xs font-semibold text-slate-700 px-2 py-0.5 focus:outline-none cursor-pointer hover:bg-sky-50 transition-colors disabled:opacity-50 disabled:bg-slate-100">
            <option v-for="period in availablePeriods" :key="period" :value="period">{{ monthNames[period] || period }}</option>
          </select>
          <div class="w-px h-3.5 bg-sky-200"></div>
          <select v-model="selectedYear" class="bg-transparent text-xs font-semibold text-slate-700 px-2 py-0.5 focus:outline-none cursor-pointer hover:bg-sky-50 transition-colors">
            <option v-for="year in availableYears" :key="year" :value="year">{{ year }}</option>
          </select>
        </div>

        <div class="flex items-center gap-1 bg-white border border-sky-200 rounded px-2 py-0.5 shadow-sm shrink-0">
          <label class="flex items-center gap-1 cursor-pointer text-xs font-semibold text-[#107c41] whitespace-nowrap">
            <input type="checkbox" v-model="showBudget" class="w-3 h-3 text-[#107c41] rounded border-slate-300 focus:ring-[#107c41] cursor-pointer">
            Budget
          </label>
        </div>

        <select v-model="displayFormat" class="bg-white border border-sky-200 text-xs font-semibold text-slate-700 px-2 py-0.5 rounded focus:outline-none focus:border-sky-500 cursor-pointer transition-colors shadow-sm shrink-0">
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
          <RouterLink to="/dashboard" class="inline-flex items-center gap-2 px-5 py-2.5 bg-[#107c41] hover:bg-[#0b592e] text-white text-sm font-bold rounded-xl shadow-md active:scale-95 transition-all">
            Back to Dashboard
          </RouterLink>
        </div>
      </div>
    </div>

    <div v-else-if="isLoading" class="flex flex-col items-center justify-center flex-1 min-h-0 w-full h-full">
      <div class="w-10 h-10 border-4 border-slate-100 border-t-[#107c41] rounded-full animate-spin"></div>
      <p class="text-[#107c41] font-medium animate-pulse text-sm mt-4">Calculating Income Statement...</p>
    </div>

    <!-- ================= BEAUTIFUL EXCEL GRID VIEW: STANDARD VIEW ================= -->
    <div v-else-if="displayFormat === '1' && multiReports.current" class="p-6 w-full flex flex-col flex-1 min-h-0 overflow-hidden">
      <div class="w-full flex-1 min-h-0 overflow-auto custom-scrollbar">
        <div class="spreadsheet-table bg-white border border-[#cbd5e1] rounded shadow-sm inline-block min-w-max mx-auto relative select-none">
          <table class="border-collapse text-[11px] font-sans text-slate-800 w-full">
            
            <!-- Column letters headers -->
            <thead>
              <tr class="bg-[#f1f5f9] text-slate-500 font-mono text-[9px] font-semibold border-b border-[#cbd5e1]">
                <th class="border-r border-[#cbd5e1] bg-[#e2e8f0] w-9 text-center p-0 font-sans"></th>
                <th class="border-r border-[#cbd5e1] text-center w-60 py-0.5">A</th>
                <th class="border-r border-[#cbd5e1] text-center w-28 py-0.5">B</th>
                <th v-if="showBudget" class="border-r border-[#cbd5e1] text-center w-28 py-0.5">C</th>
                <th class="border-r border-[#cbd5e1] text-center w-6 bg-[#f8fafc] py-0.5">D</th>
                <th class="border-r border-[#cbd5e1] text-center w-60 py-0.5">E</th>
                <th class="border-r border-[#cbd5e1] text-center w-28 py-0.5">F</th>
                <th v-if="showBudget" class="text-center w-28 py-0.5">G</th>
              </tr>
            </thead>

            <!-- Main column description header -->
            <thead>
              <tr class="bg-[#f8fafc] font-black text-slate-700 border-b border-[#cbd5e1] tracking-wider text-[10px] uppercase shadow-sm">
                <th class="excel-col-letter-cell bg-[#f1f5f9] border-r border-[#cbd5e1] w-9 p-0"></th>
                <!-- Revenue Title -->
                <th class="border-r border-[#cbd5e1] px-3.5 py-2 text-left">Revenue</th>
                <th class="border-r border-[#cbd5e1] px-3 py-2 text-right">Balance (IDR)</th>
                <th v-if="showBudget" class="border-r border-[#cbd5e1] px-3 py-2 text-right">Budget (IDR)</th>
                
                <!-- Blank Divider Column -->
                <th class="border-r border-[#cbd5e1] bg-slate-50/70 p-0 w-6"></th>
                
                <!-- Expenses Title -->
                <th class="border-r border-[#cbd5e1] px-3.5 py-2 text-left">Expenses</th>
                <th class="border-r border-[#cbd5e1] px-3 py-2 text-right">Balance (IDR)</th>
                <th v-if="showBudget" class="px-3 py-2 text-right">Budget (IDR)</th>
              </tr>
            </thead>

            <!-- Spreadsheet rows pairs -->
            <tbody>
              <tr 
                v-for="(row, index) in tFormatRows" 
                :key="index" 
                class="transition-colors border-b border-[#cbd5e1]"
              >
                <!-- Row index label -->
                <td 
                  class="excel-row-num bg-[#f1f5f9] text-slate-400 font-mono text-[9px] text-center border-r border-[#cbd5e1] sticky left-0 z-30 select-none py-1 h-6 w-9 font-bold"
                >
                  {{ index + 2 }}
                </td>

                <!-- REVENUE SIDE (Left) -->
                <template v-if="row.left">
                  
                  <!-- Group Item Row -->
                  <template v-if="row.left.type === 'group'">
                    <td 
                      @click="openModal(row.left.name, row.left.items)"
                      class="border-r border-[#cbd5e1] px-4 py-1 font-semibold text-slate-700 tracking-tight hover:text-[#107c41] cursor-pointer"
                    >
                      {{ row.left.name }}
                    </td>
                    <td 
                      class="border-r border-[#cbd5e1] px-3 py-1 text-right font-bold"
                      :class="isNeg(row.left.total) ? 'text-red-600' : 'text-black'"
                    >
                      {{ formatMoney(row.left.total) }}
                    </td>
                    <td 
                      v-if="showBudget"
                      class="border-r border-[#cbd5e1] px-3 py-1 text-right font-semibold text-blue-600"
                    >
                      {{ formatMoney(row.left.budget) }}
                    </td>
                  </template>

                  <!-- Subtotal Row -->
                  <template v-else-if="row.left.type === 'subtotal'">
                    <td 
                      class="border-r border-[#cbd5e1] px-4 py-1.5 bg-[#f8fafc] font-bold text-slate-900 uppercase text-[10px]"
                    >
                      Total Revenue
                    </td>
                    <td 
                      class="border-r border-[#cbd5e1] px-3 py-1.5 text-right font-bold bg-[#f8fafc] border-t border-slate-400 border-b border-b-2 border-slate-400"
                      :class="isNeg(row.left.total) ? 'text-red-600' : 'text-black'"
                    >
                      {{ formatMoney(row.left.total) }}
                    </td>
                    <td 
                      v-if="showBudget"
                      class="border-r border-[#cbd5e1] px-3 py-1.5 text-right font-bold bg-[#f8fafc] border-t border-slate-400 border-b border-b-2 border-slate-400 text-blue-600"
                    >
                      {{ formatMoney(row.left.budget) }}
                    </td>
                  </template>

                </template>
                <template v-else>
                  <!-- Blank revenue cells -->
                  <td class="border-r border-[#cbd5e1] px-4 py-1 bg-slate-50/30"></td>
                  <td class="border-r border-[#cbd5e1] px-3 py-1 bg-slate-50/30"></td>
                  <td v-if="showBudget" class="border-r border-[#cbd5e1] px-3 py-1 bg-slate-50/30"></td>
                </template>

                <!-- Central spacing column divider -->
                <td class="border-r border-[#cbd5e1] bg-slate-100/50 p-0 text-center w-6"></td>

                <!-- EXPENSES SIDE (Right) -->
                <template v-if="row.right">
                  
                  <!-- Section Category row -->
                  <template v-if="row.right.type === 'section'">
                    <td 
                      class="border-r border-[#cbd5e1] px-3 py-1 bg-slate-100/80 font-black text-slate-500 uppercase tracking-widest text-[9.5px]"
                    >
                      {{ row.right.name }}
                    </td>
                    <td class="border-r border-[#cbd5e1] bg-slate-100/40 py-1"></td>
                    <td v-if="showBudget" class="border-r border-[#cbd5e1] bg-slate-100/40 py-1"></td>
                  </template>

                  <!-- Group Item Row -->
                  <template v-else-if="row.right.type === 'group'">
                    <td 
                      @click="openModal(row.right.name, row.right.items)"
                      class="border-r border-[#cbd5e1] px-4 py-1 font-semibold text-slate-700 tracking-tight hover:text-[#107c41] cursor-pointer"
                    >
                      {{ row.right.name }}
                    </td>
                    <td 
                      class="border-r border-[#cbd5e1] px-3 py-1 text-right font-bold"
                      :class="isNeg(row.right.total) ? 'text-red-600' : 'text-black'"
                    >
                      {{ formatMoney(row.right.total) }}
                    </td>
                    <td 
                      v-if="showBudget"
                      class="border-r border-[#cbd5e1] px-3 py-1 text-right font-semibold text-blue-600"
                    >
                      {{ formatMoney(row.right.budget) }}
                    </td>
                  </template>

                  <!-- Subtotals row -->
                  <template v-else-if="row.right.type === 'subtotal'">
                    <td 
                      class="border-r border-[#cbd5e1] px-4 py-1.5 bg-[#f8fafc] font-bold text-slate-900 uppercase text-[10px]"
                    >
                      Total Expenses
                    </td>
                    <td 
                      class="border-r border-[#cbd5e1] px-3 py-1.5 text-right font-bold bg-[#f8fafc] border-t border-slate-400 border-b border-b-2 border-slate-400"
                      :class="isNeg(row.right.total) ? 'text-red-600' : 'text-black'"
                    >
                      {{ formatMoney(row.right.total) }}
                    </td>
                    <td 
                      v-if="showBudget"
                      class="border-r border-[#cbd5e1] px-3 py-1.5 text-right font-bold bg-[#f8fafc] border-t border-slate-400 border-b border-b-2 border-slate-400 text-blue-600"
                    >
                      {{ formatMoney(row.right.budget) }}
                    </td>
                  </template>

                </template>
                <template v-else>
                  <td class="border-r border-[#cbd5e1] px-4 py-1 bg-slate-50/30"></td>
                  <td class="border-r border-[#cbd5e1] px-3 py-1 bg-slate-50/30"></td>
                  <td v-if="showBudget" class="border-r border-[#cbd5e1] px-3 py-1 bg-slate-50/30"></td>
                </template>

              </tr>

              <!-- BOTTOM GRAND TOTAL ROW -->
              <tr 
                class="border-b border-[#cbd5e1] bg-green-50/20"
              >
                <td 
                  class="excel-row-num bg-[#f1f5f9] text-slate-400 font-mono text-[9px] text-center border-r border-[#cbd5e1] sticky left-0 z-30 select-none py-2 font-bold h-7 w-9"
                >
                  {{ tFormatRows.length + 2 }}
                </td>

                <!-- Revenue Total Row (Left) -->
                <td 
                  class="border-r border-[#cbd5e1] px-4 py-2 font-black text-slate-900 uppercase text-[10.5px]"
                >
                  Total Revenue
                </td>
                <td 
                  class="border-r border-[#cbd5e1] px-3 py-2 text-right font-black text-[12px] border-t-[2px] border-t-slate-400 border-b-[4px] border-b-double border-b-slate-400 bg-green-50/10"
                  :class="isNeg(multiReports.current.structure.Revenue.total) ? 'text-red-600' : 'text-black'"
                >
                  {{ formatMoney(multiReports.current.structure.Revenue.total) }}
                </td>
                <td 
                  v-if="showBudget"
                  class="border-r border-[#cbd5e1] px-3 py-2 text-right font-black text-[12px] border-t-[2px] border-t-slate-400 border-b-[4px] border-b-double border-b-slate-400 bg-green-50/10 text-blue-600"
                >
                  {{ formatMoney(format1BudgetTotals.revenueTotal) }}
                </td>

                <!-- Central Spacing divider -->
                <td class="border-r border-[#cbd5e1] bg-slate-100/50 p-0 text-center w-6"></td>

                <!-- Expenses Total Row (Right) -->
                <td 
                  class="border-r border-[#cbd5e1] px-4 py-2 font-black text-slate-900 uppercase text-[10.5px]"
                >
                  Total Expenses
                </td>
                <td 
                  class="border-r border-[#cbd5e1] px-3 py-2 text-right font-black text-[12px] border-t-[2px] border-t-slate-400 border-b-[4px] border-b-double border-b-slate-400 bg-green-50/10"
                  :class="isNeg(multiReports.current.structure.Expenses.total) ? 'text-red-600' : 'text-black'"
                >
                  {{ formatMoney(multiReports.current.structure.Expenses.total) }}
                </td>
                <td 
                  v-if="showBudget"
                  class="px-3 py-2 text-right font-black text-[12px] border-t-[2px] border-t-slate-400 border-b-[4px] border-b-double border-b-slate-400 bg-green-50/10 text-blue-600"
                >
                  {{ formatMoney(format1BudgetTotals.expensesTotal) }}
                </td>

              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- ================= BEAUTIFUL EXCEL GRID VIEW: MATRIX GRID ================= -->
    <div v-else-if="['2', '3', '4'].includes(displayFormat) && matrixState" class="p-6 w-full flex flex-col flex-1 min-h-0 overflow-hidden">
      <div class="w-full flex-1 min-h-0 overflow-auto custom-scrollbar">
        <div class="spreadsheet-table bg-white border border-[#cbd5e1] rounded shadow-sm inline-block min-w-max mx-auto relative select-none">
          <table class="border-collapse text-[11px] font-sans text-slate-800 w-full">
            
            <!-- Column letters headers -->
            <thead>
              <tr class="bg-[#f1f5f9] text-slate-500 font-mono text-[9px] font-semibold border-b border-[#cbd5e1]">
                <th class="border-r border-[#cbd5e1] bg-[#e2e8f0] w-9 min-w-[36px] max-w-[36px] text-center p-0 font-sans sticky left-0 z-30"></th>
                <th class="border-r border-[#cbd5e1] text-center w-60 min-w-[240px] max-w-[240px] py-0.5 sticky left-[36px] z-30 bg-[#f1f5f9]">A</th>
                <template v-for="(header, index) in matrixState.headers" :key="index">
                  <th v-if="!showBudget" class="border-r border-[#cbd5e1] text-center w-36 py-0.5">
                    {{ String.fromCharCode(66 + index) }}
                  </th>
                  <template v-else>
                    <th class="border-r border-[#cbd5e1] text-center w-28 py-0.5 bg-slate-50/20">
                      {{ String.fromCharCode(66 + index * 2) }}
                    </th>
                    <th class="border-r border-[#cbd5e1] text-center w-28 py-0.5 bg-slate-50/20">
                      {{ String.fromCharCode(66 + index * 2 + 1) }}
                    </th>
                  </template>
                </template>
              </tr>
            </thead>

            <!-- Main columns description header -->
            <thead>
              <!-- Top Row: Account & Period Headers -->
              <tr class="bg-[#f8fafc] font-black text-slate-700 border-b border-[#cbd5e1] tracking-wider text-[10px] uppercase shadow-sm">
                <th class="excel-col-letter-cell bg-[#f1f5f9] border-r border-[#cbd5e1] w-9 min-w-[36px] max-w-[36px] p-0 sticky left-0 z-30"></th>
                <th 
                  :rowspan="showBudget ? 2 : 1" 
                  class="border-r border-[#cbd5e1] px-4 py-2 text-left w-60 min-w-[240px] max-w-[240px] sticky left-[36px] z-30 bg-[#f8fafc] text-[#0f2240] shrink-0 font-black align-middle"
                >
                  Account
                </th>
                <template v-for="(header, index) in matrixState.headers" :key="index">
                  <th 
                    :colspan="showBudget ? 2 : 1" 
                    class="border-r border-[#cbd5e1] text-center py-1.5 whitespace-nowrap"
                  >
                    {{ header.label }}
                    <span class="text-slate-500 block text-[8px] font-bold mt-0.5 normal-case">{{ header.sub }}</span>
                  </th>
                </template>
              </tr>

              <!-- Bottom Row: Actual & Budget Split (Only shown if showBudget is true) -->
              <tr 
                v-if="showBudget" 
                class="bg-[#f8fafc] font-black text-slate-700 border-b border-[#cbd5e1] tracking-wider text-[9px] uppercase shadow-sm"
              >
                <!-- Empty spacer for row number column -->
                <th class="excel-col-letter-cell bg-[#f1f5f9] border-r border-[#cbd5e1] w-9 min-w-[36px] max-w-[36px] p-0 sticky left-0 z-30"></th>
                <!-- Account column is covered by rowspan="2" from the top row -->
                <template v-for="(header, index) in matrixState.headers" :key="index">
                  <th class="border-r border-[#cbd5e1] text-center py-1 bg-slate-50/20 text-[#0f2240] font-black w-28">
                    Actual
                  </th>
                  <th class="border-r border-[#cbd5e1] text-center py-1 bg-slate-50/20 text-slate-500 font-bold w-28">
                    Budget
                  </th>
                </template>
              </tr>
            </thead>

            <!-- Table Body -->
            <tbody>
              <tr 
                v-for="row in matrixRows.rows" 
                :key="row.rowNum" 
                class="transition-colors border-b border-[#cbd5e1]"
              >
                <!-- Sticky row num cell on left -->
                <td 
                  class="excel-row-num bg-[#f1f5f9] text-slate-400 font-mono text-[9px] text-center border-r border-[#cbd5e1] sticky left-0 z-30 select-none py-1 h-6 w-9 min-w-[36px] max-w-[36px] font-bold"
                >
                  {{ row.rowNum }}
                </td>

                <!-- Case A: Section headers -->
                <template v-if="row.type === 'section'">
                  <td 
                    class="font-black text-[10px] text-slate-800 uppercase tracking-widest pl-4 py-1.5 border-r border-[#cbd5e1] sticky left-[36px] z-20 bg-[#f1f5f9] min-w-[240px] max-w-[240px]"
                  >
                    {{ row.name }}
                  </td>
                  <!-- Empty trailing cells -->
                  <template v-for="(header, index) in matrixState.headers" :key="index">
                    <td class="border-r border-[#cbd5e1] bg-slate-100/20" v-if="!showBudget"></td>
                    <template v-else>
                      <td class="border-r border-[#cbd5e1] bg-slate-100/20"></td>
                      <td class="border-r border-[#cbd5e1] bg-slate-100/20"></td>
                    </template>
                  </template>
                </template>

                <!-- Case B: Group Balance sheet rows -->
                <template v-else-if="row.type === 'group'">
                  <td 
                    @click="openModal(row.name, row.items, matrixState.headers)"
                    class="font-semibold text-slate-700 tracking-tight px-4 py-1 border-r border-[#cbd5e1] hover:text-[#107c41] cursor-pointer sticky left-[36px] z-20 bg-white min-w-[240px] max-w-[240px]"
                  >
                    {{ row.name }}
                  </td>
                  
                  <template v-for="(total, index) in row.totals" :key="index">
                    <!-- Actual -->
                    <td 
                      class="border-r border-[#cbd5e1] text-right py-1 px-3 font-bold"
                      :class="isNeg(total) ? 'text-red-600' : 'text-black'"
                    >
                      {{ formatMoney(total) }}
                    </td>
                    <!-- Budget -->
                    <td 
                      v-if="showBudget"
                      class="border-r border-[#cbd5e1] text-right bg-slate-50/40 py-1 px-3 font-medium text-blue-600"
                    >
                      {{ formatMoney(row.budgets[index]) }}
                    </td>
                  </template>
                </template>

                <!-- Case C: Subtotals -->
                <template v-else-if="row.type === 'subtotal'">
                  <td 
                    class="font-black text-slate-900 uppercase px-4 py-1.5 border-r border-[#cbd5e1] sticky left-[36px] z-20 bg-[#f8fafc] text-[10px] min-w-[240px] max-w-[240px]"
                  >
                    Total {{ row.name.split(' ')[1] }}
                  </td>

                  <template v-for="(total, index) in row.totals" :key="index">
                    <!-- Actual -->
                    <td 
                      class="border-r border-[#cbd5e1] text-right py-1.5 px-3 font-black bg-[#f8fafc] border-t border-t-slate-400 border-b border-b-2 border-b-slate-400"
                      :class="isNeg(total) ? 'text-red-600' : 'text-black'"
                    >
                      {{ formatMoney(total) }}
                    </td>
                    <!-- Budget -->
                    <td 
                      v-if="showBudget"
                      class="border-r border-[#cbd5e1] text-right bg-[#f8fafc] py-1.5 px-3 font-black border-t border-t-slate-400 border-b border-b-2 border-b-slate-400 text-blue-600"
                    >
                      {{ formatMoney(row.budgetTotals[index]) }}
                    </td>
                  </template>
                </template>

              </tr>

              <!-- BOTTOM DOUBLE UNDERLINE NET INCOME GRAND TOTAL ROW -->
              <tr class="border-b border-[#cbd5e1] bg-green-50/20 font-black">
                <!-- Sticky row num cell on left -->
                <td 
                  class="excel-row-num bg-[#f1f5f9] text-slate-400 font-mono text-[9px] text-center border-r border-[#cbd5e1] sticky left-0 z-30 select-none py-2 font-bold h-7 w-9 min-w-[36px] max-w-[36px]"
                >
                  {{ matrixRows.rows.length + 2 }}
                </td>

                <!-- Grand Total Title (Sticky) -->
                <td 
                  class="font-black text-[#0f2240] uppercase px-4 py-2 border-r border-[#cbd5e1] sticky left-[36px] z-20 bg-[#ecfdf5] text-[10px] tracking-wider min-w-[240px] max-w-[240px]"
                >
                  Net Income
                </td>

                <template v-for="(header, index) in matrixState.headers" :key="index">
                  <!-- Actual Grand Total -->
                  <td 
                    class="border-r border-[#cbd5e1] text-right py-2 px-3 font-black text-[12px] border-t-[2px] border-t-slate-400 border-b-[4px] border-b-double border-b-slate-400 bg-green-50/10"
                    :class="isNeg(matrixState.netIncomes[index]) ? 'text-red-600' : 'text-green-600'"
                  >
                    {{ formatMoney(matrixState.netIncomes[index]) }}
                  </td>
                  <!-- Budget Grand Total -->
                  <td 
                    v-if="showBudget"
                    class="border-r border-[#cbd5e1] text-right bg-green-50/10 py-2 px-3 font-black text-blue-600 text-[12px] border-t-[2px] border-t-slate-400 border-b-[4px] border-b-double border-b-slate-400"
                  >
                    {{ formatMoney(matrixState.netIncomeBudgets[index]) }}
                  </td>
                </template>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- DRILL-DOWN MODAL -->
    <Teleport to="body">
      <transition name="fade">
        <div v-if="isModalOpen" class="fixed inset-0 z-[1000] flex items-center justify-center p-4 sm:p-6">
          <div class="absolute inset-0 bg-slate-900/60 backdrop-blur-sm" @click="closeModal"></div>
          <div class="bg-white/95 backdrop-blur-md w-full max-w-5xl rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh] animate-slide-up border border-[#cbd5e1]">
            
            <div class="flex justify-between items-center px-6 py-4 bg-[#f8fafc] border-b border-slate-200 shrink-0 relative z-10 select-none">
              <div>
                <h3 class="text-base md:text-lg font-black text-slate-900 tracking-tight">{{ activeGroupTitle }}</h3>
                <p class="text-xs text-[#107c41] mt-0.5 font-bold uppercase tracking-widest font-mono">Ledgers breakdown sheet</p>
              </div>
              <button @click="closeModal" class="text-slate-400 hover:text-rose-500 transition-colors bg-white hover:bg-rose-50 p-2 rounded-lg border border-slate-200 shadow-sm outline-none">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
              </button>
            </div>

            <div class="overflow-auto grow p-0 custom-scrollbar relative z-10">
              <table class="w-full text-left border-collapse relative z-10 text-[11px] font-sans text-slate-800">
                <thead class="sticky top-0 bg-[#f8fafc]/90 backdrop-blur-sm z-10 border-b border-[#cbd5e1] shadow-sm font-black uppercase text-slate-600 tracking-wider">
                  <tr>
                    <th class="px-6 py-2.5 min-w-[320px]">Account No & Name</th>
                    <template v-if="activeGroupHeaders.length === 0">
                      <th class="px-6 py-2.5 text-right whitespace-nowrap min-w-48 border-l border-slate-100">Actual Balance</th>
                      <th v-if="showBudget" class="px-6 py-2.5 text-right whitespace-nowrap min-w-48 border-l border-slate-100 bg-slate-50/50">Budget Balance</th>
                    </template>
                    <template v-else v-for="(header, index) in activeGroupHeaders" :key="index">
                      <th class="px-6 py-2.5 text-right whitespace-nowrap min-w-48 border-l border-slate-100">
                        {{ header.label }} 
                        <span class="text-slate-400 block text-[9px] font-medium tracking-normal mt-0.5 normal-case">{{ header.sub }} <span v-if="showBudget">(ACT)</span></span>
                      </th>
                      <th v-if="showBudget" class="px-6 py-2.5 text-right whitespace-nowrap min-w-48 border-l border-slate-200/60 bg-slate-50/50">
                        {{ header.label }} 
                        <span class="text-[#107c41] block text-[9px] font-semibold tracking-normal mt-0.5 normal-case">{{ header.sub }} (BGT)</span>
                      </th>
                    </template>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-200 bg-white">
                  <tr v-for="item in activeGroupItems" :key="item.no" class="hover:bg-slate-50/80 transition-colors">
                    <td class="px-6 py-2 text-slate-700 tracking-tight whitespace-normal break-words">
                      <div class="flex items-start">
                        <span class="font-mono text-slate-400 mr-3 shrink-0 font-bold">{{ item.no }}</span>
                        <span class="font-medium flex-1">{{ item.name }}</span>
                      </div>
                    </td>
                    <template v-if="item.balance !== undefined">
                      <td class="px-6 py-2 border-l border-slate-100 text-right">
                        <strong :class="['font-bold font-mono', isNeg(item.balance) ? 'text-red-500' : 'text-black']">{{ formatMoney(item.balance) }}</strong>
                      </td>
                      <td v-if="showBudget" class="px-6 py-2 border-l border-slate-100 bg-slate-50/30 text-right">
                        <strong class="font-medium font-mono text-blue-600">{{ formatMoney(item.end_budget || 0) }}</strong>
                      </td>
                    </template>
                    <template v-else>
                      <template v-for="(balance, index) in item.balances" :key="index">
                        <td class="px-6 py-2 border-l border-slate-100 text-right">
                          <strong :class="['font-bold font-mono', isNeg(balance) ? 'text-red-500' : 'text-black']">{{ formatMoney(balance) }}</strong>
                        </td>
                        <td v-if="showBudget" class="px-6 py-2 border-l border-slate-100 bg-slate-50/30 text-right">
                          <strong class="font-medium font-mono text-blue-600">{{ formatMoney(item.budgets[index]) }}</strong>
                        </td>
                      </template>
                    </template>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="px-8 py-3 bg-[#f8fafc] border-t border-slate-200 flex justify-end shrink-0 select-none">
              <button @click="closeModal" class="bg-[#107c41] hover:bg-[#0b592e] text-white px-5 py-1.5 rounded text-xs font-bold transition-colors shadow-sm tracking-wide outline-none">Close Breakdown</button>
            </div>

          </div>
        </div>
      </transition>
    </Teleport>

  </ReportLayout>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.animate-slide-up { animation: slideUp 0.3s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes slideUp { from { opacity: 0; transform: translateY(15px) scale(0.98); } to { opacity: 1; transform: translateY(0) scale(1); } }
</style>
