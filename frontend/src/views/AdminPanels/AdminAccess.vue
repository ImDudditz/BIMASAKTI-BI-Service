<script setup>
import { ref, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import api from '@/services/api'
import ReportLayout from '@/components/ReportLayout.vue'

const authStore = useAuthStore()

const users = ref([])
const selectedUser = ref(null)
const userPermissions = ref({ widgets: {}, reports: {} })
const isLoading = ref(false)

// Configured widget groups with dynamic discovery globs and detailed static metadata
const widgetGroups = [
  {
    id: 'financial',
    name: 'Financial Dashboard Widgets',
    files: import.meta.glob('../../components/widgets/Financial/*Widget.vue'),
    staticWidgets: [
      { key: 'kpi_cards', filename: 'KpiCardsWidget.vue', label: 'KPI Cards', description: 'Displays a high-level summary of key performance indicators for the year-to-date (YTD), including Revenue, Expenses, Income/Loss, and Net Growth.' },
      { key: 'capital_growth', filename: 'CapitalGrowthWidget.vue', label: 'Capital Growth', description: 'Displays capital growth trends and equity balance over time using ledger data.' },
      { key: 'operating_cash_flow', filename: 'OperatingCashFlowWidget.vue', label: 'Operating Cash Inflow vs Outflow', description: 'Visualizes operating cash inflow and outflow comparisons over time.' }
    ]
  },
  {
    id: 'operation',
    name: 'Operation Dashboard Widgets',
    files: import.meta.glob('../../components/widgets/Operation/*Widget.vue'),
    staticWidgets: [
      { key: 'operation_metrics', filename: 'OperationMetricsWidget.vue', label: 'Operation Metrics', description: 'Real-time tenant activity and daily foot traffic statistics including active tenants count and occupancy rate.' },
      { key: 'lease_expirations', filename: 'LeaseExpirationsWidget.vue', label: 'Lease Expirations', description: 'Projected contract terminations and lease expirations timeline over the next 6 months.' }
    ]
  },
  {
    id: 'service_maintenance',
    name: 'Service & Maintenance Widgets',
    files: import.meta.glob('../../components/widgets/ServiceAndMaintenance/*.vue'),
    staticWidgets: [
      { key: 'tickets_kpi', filename: 'TicketsKPI.vue', label: 'Tickets KPI', description: 'Overview of maintenance ticket performance indicators including total, complaints, maintenance, requests, and closed tickets.' },
      { key: 'maintenance_status', filename: 'MaintenanceStatusWidget.vue', label: 'Maintenance Status', description: 'Active service tickets and system uptime reliability breakdown across machinery and facilities.' },
      { key: 'tickets_by_category', filename: 'TicketsByCategoryWidget.vue', label: 'Tickets by Category', description: 'Total volume breakdown of active support tickets by department/category (e.g. Mechanical, Electrical, Civil).' }
    ]
  }
]

// Build the groupedWidgets array dynamically
const groupedWidgets = []

widgetGroups.forEach(group => {
  const widgets = []
  
  // First, add the pre-defined static widgets that exist in the directory
  group.staticWidgets.forEach(sw => {
    const globIsEmpty = Object.keys(group.files).length === 0
    const hasFile = globIsEmpty || Object.keys(group.files).some(path => path.endsWith(sw.filename))
    if (hasFile) {
      widgets.push({
        key: sw.key,
        label: sw.label,
        description: sw.description
      })
    }
  })
  
  // Now, dynamically discover any new widgets not covered by the static list
  Object.keys(group.files).forEach(path => {
    const filename = path.split('/').pop()
    const isStatic = group.staticWidgets.some(sw => sw.filename === filename)
    if (!isStatic) {
      const baseName = filename.replace(/Widget\.vue$/, '')
      // PascalCase / camelCase to snake_case
      const key = baseName.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toLowerCase()
      // PascalCase / camelCase to Title Case
      const label = baseName.replace(/([a-z0-9])([A-Z])/g, '$1 $2')
      
      widgets.push({
        key,
        label,
        description: 'Custom widget automatically registered from components directory.'
      })
    }
  })

  if (widgets.length > 0) {
    groupedWidgets.push({
      id: group.id,
      name: group.name,
      widgets
    })
  }
})

// Flatten all widgets to make backend loading/saving logic seamless and completely unmutated
const allWidgets = groupedWidgets.flatMap(g => g.widgets)

const staticReports = [
  { key: 'balance_sheet', label: 'Balance Sheet', description: 'Enables access to the Balance Sheet financial report under the Reports menu.' },
  { key: 'income_statement', label: 'Income Statement', description: 'Enables access to the Income Statement profit & loss report under the Reports menu.' }
]

const fetchUsers = async () => {
  isLoading.value = true
  try {
    const res = await api.get('/admin/users', { 
      params: { 
        company_id: authStore.user.company_id,
        admin_username: authStore.user.username 
      } 
    })
    users.value = res.data
  } catch {
    // Ignore error
  } finally {
    isLoading.value = false
  }
}

const selectUser = async (user) => {
  selectedUser.value = user
  isLoading.value = true
  try {
    const res = await api.get(`/admin/users/${user.id}/permissions`, {
      params: { 
        company_id: authStore.user.company_id,
        admin_username: authStore.user.username 
      }
    })
    
    const perms = { 
      widgets: {}, 
      reports: {} 
    }
    
    // Initialize widgets with DB records or fallback defaults (first 5 financial widgets)
    const hasWidgetConfig = res.data.widgets && Object.keys(res.data.widgets).length > 0
    allWidgets.forEach(w => {
      if (user.role === 'admin') {
        perms.widgets[w.key] = true
      } else {
        if (hasWidgetConfig) {
          perms.widgets[w.key] = res.data.widgets[w.key] === true
        } else {
          const defaultFinancials = ['kpi_cards', 'capital_growth', 'operating_cash_flow', 'revenue_budget', 'expense_budget']
          perms.widgets[w.key] = defaultFinancials.includes(w.key)
        }
      }
    })

    // Initialize reports with DB records or fallback defaults (both reports active by default)
    const hasReportConfig = res.data.reports && Object.keys(res.data.reports).length > 0
    staticReports.forEach(r => {
      if (user.role === 'admin') {
        perms.reports[r.key] = true
      } else {
        if (hasReportConfig) {
          perms.reports[r.key] = res.data.reports[r.key] === true
        } else {
          perms.reports[r.key] = true
        }
      }
    })
    
    userPermissions.value = perms
  } catch {
    // Ignore error
  } finally {
    isLoading.value = false
  }
}

const savePermissions = async () => {
  if (!selectedUser.value) return
  isLoading.value = true
  
  // If selectedUser is admin, make sure we force all widgets to be true in the payload
  const finalWidgets = { ...userPermissions.value.widgets }
  if (selectedUser.value.role === 'admin') {
    allWidgets.forEach(w => {
      finalWidgets[w.key] = true
    })
  }

  // Force all reports to be true for admin
  const finalReports = { ...userPermissions.value.reports }
  if (selectedUser.value.role === 'admin') {
    staticReports.forEach(r => {
      finalReports[r.key] = true
    })
  }
  
  const payload = {
    widgets: finalWidgets,
    reports: finalReports
  }
  
  try {
    await api.post(`/admin/users/${selectedUser.value.id}/permissions`, payload, {
      params: { 
        company_id: authStore.user.company_id,
        admin_username: authStore.user.username 
      }
    })
    alert("Permissions saved successfully")
  } catch {
    alert("Error saving permissions")
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  if (authStore.isAdmin) {
    fetchUsers()
  }
})
</script>

<template>
  <ReportLayout title="Admin Panel" subtitle="User Access Control">
    <div class="flex h-full w-full bg-white/90 backdrop-blur-md rounded-xl border border-white/50 shadow-sm overflow-hidden min-h-0">
      
      <!-- Left Column: User List -->
      <div class="w-1/3 border-r border-slate-200 flex flex-col bg-slate-50/50">
        <div class="p-4 border-b border-slate-200 shrink-0">
          <h3 class="font-bold text-sky-900">Tenant Users</h3>
          <p class="text-xs text-slate-500">Select a user to manage access</p>
        </div>
        <div class="flex-1 overflow-y-auto custom-scrollbar p-2 space-y-1">
          <div v-if="isLoading && !users.length" class="text-center py-4 text-sm text-slate-500">Loading...</div>
          <button 
            v-for="u in users" :key="u.id"
            @click="selectUser(u)"
            class="w-full text-left px-4 py-3 rounded-lg transition-all duration-200 border"
            :class="selectedUser?.id === u.id ? 'bg-sky-50 border-sky-200 shadow-sm' : 'border-transparent hover:bg-slate-100'"
          >
            <div class="flex items-center justify-between">
              <span class="font-semibold text-slate-700" :class="{'text-sky-700': selectedUser?.id === u.id}">{{ u.username }}</span>
              <span class="text-[10px] px-2 py-0.5 rounded-full bg-slate-200 text-slate-600 font-bold uppercase tracking-wider" v-if="u.role === 'admin'">Admin</span>
            </div>
          </button>
        </div>
      </div>

      <!-- Right Column: Permissions Panel -->
      <div class="w-2/3 flex flex-col bg-white">
        <div v-if="!selectedUser" class="flex-1 flex items-center justify-center text-slate-400">
          <p>Please select a user from the list to view permissions.</p>
        </div>
        
        <template v-else>
          <div class="p-5 border-b border-slate-100 flex justify-between items-center bg-white sticky top-0 z-10 shadow-sm shrink-0">
            <div>
              <h2 class="text-xl font-black text-slate-800">Permissions: {{ selectedUser.username }}</h2>
              <p class="text-sm text-slate-500">Configure access to executive dashboard widgets</p>
            </div>
            <button 
              @click="savePermissions"
              :disabled="isLoading"
              class="bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 text-white px-5 py-2 rounded-lg text-sm font-bold shadow-md transition-all active:scale-95 flex items-center gap-2"
            >
              <span>{{ isLoading ? '⏳ Saving...' : '💾 Save Permissions' }}</span>
            </button>
          </div>

          <div class="flex-1 overflow-y-auto custom-scrollbar p-6 space-y-8 bg-slate-50/20">
            
            <!-- Grouped Dashboard Widgets -->
            <section v-for="group in groupedWidgets" :key="group.id" class="space-y-4">
              <h3 class="text-xs font-black text-sky-900 uppercase tracking-widest border-b border-slate-100 pb-2 flex items-center gap-2">
                <span class="w-1.5 h-3.5 bg-sky-600 rounded-sm"></span>
                {{ group.name }}
              </h3>
              
              <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
                <div 
                  v-for="widget in group.widgets" 
                  :key="widget.key" 
                  class="relative flex flex-col justify-between p-5 rounded-xl border border-slate-200/80 bg-white hover:bg-slate-50/50 hover:border-sky-300/80 transition-all duration-300 shadow-sm hover:shadow-md"
                  :class="{'border-sky-200 bg-sky-50/10': selectedUser?.role === 'admin'}"
                >
                  <div class="flex items-start justify-between gap-4">
                    <div class="flex-1">
                      <h4 class="font-bold text-slate-800 text-sm flex items-center gap-2">
                        {{ widget.label }}
                        <span 
                          v-if="selectedUser?.role === 'admin'" 
                          class="inline-flex items-center text-[9px] font-bold text-sky-700 bg-sky-100 px-2 py-0.5 rounded-full uppercase tracking-wider"
                        >
                          Required
                        </span>
                      </h4>
                      <p class="text-xs text-slate-500 mt-1 leading-relaxed">{{ widget.description }}</p>
                    </div>
                    
                    <div class="shrink-0 pt-0.5">
                      <!-- Locked checkmark icon for admins -->
                      <div 
                        v-if="selectedUser?.role === 'admin'" 
                        class="w-8 h-8 rounded-full bg-sky-100/80 flex items-center justify-center text-sky-600 shadow-sm" 
                        title="Required for Admin Role"
                      >
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-4 h-4">
                          <path fill-rule="evenodd" d="M10 1a4.5 4.5 0 00-4.5 4.5V9H5a2 2 0 00-2 2v6a2 2 0 002 2h10a2 2 0 002-2v-6a2 2 0 00-2-2h-.5V5.5A4.5 4.5 0 0010 1zm3 8V5.5a3 3 0 10-6 0V9h6z" clip-rule="evenodd" />
                        </svg>
                      </div>
                      
                      <!-- Standard toggle switch for other users -->
                      <label v-else class="relative inline-flex items-center cursor-pointer">
                        <input 
                          type="checkbox" 
                          v-model="userPermissions.widgets[widget.key]" 
                          class="sr-only peer"
                        >
                        <div class="w-9 h-5 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-emerald-500"></div>
                      </label>
                    </div>
                  </div>
                </div>
              </div>
            </section>

            <!-- Report Access Permissions -->
            <section class="space-y-4">
              <h3 class="text-xs font-black text-sky-900 uppercase tracking-widest border-b border-slate-100 pb-2 flex items-center gap-2">
                <span class="w-1.5 h-3.5 bg-emerald-600 rounded-sm"></span>
                Report Access Permissions
              </h3>
              
              <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
                <div 
                  v-for="report in staticReports" 
                  :key="report.key" 
                  class="relative flex flex-col justify-between p-5 rounded-xl border border-slate-200/80 bg-white hover:bg-slate-50/50 hover:border-emerald-300/80 transition-all duration-300 shadow-sm hover:shadow-md"
                  :class="{'border-emerald-200 bg-emerald-50/10': selectedUser?.role === 'admin'}"
                >
                  <div class="flex items-start justify-between gap-4">
                    <div class="flex-1">
                      <h4 class="font-bold text-slate-800 text-sm flex items-center gap-2">
                        {{ report.label }}
                        <span 
                          v-if="selectedUser?.role === 'admin'" 
                          class="inline-flex items-center text-[9px] font-bold text-emerald-700 bg-emerald-100 px-2 py-0.5 rounded-full uppercase tracking-wider"
                        >
                          Required
                        </span>
                      </h4>
                      <p class="text-xs text-slate-500 mt-1 leading-relaxed">{{ report.description }}</p>
                    </div>
                    
                    <div class="shrink-0 pt-0.5">
                      <!-- Locked checkmark icon for admins -->
                      <div 
                        v-if="selectedUser?.role === 'admin'" 
                        class="w-8 h-8 rounded-full bg-emerald-100/80 flex items-center justify-center text-emerald-600 shadow-sm" 
                        title="Required for Admin Role"
                      >
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-4 h-4">
                          <path fill-rule="evenodd" d="M10 1a4.5 4.5 0 00-4.5 4.5V9H5a2 2 0 00-2 2v6a2 2 0 002 2h10a2 2 0 002-2v-6a2 2 0 00-2-2h-.5V5.5A4.5 4.5 0 0010 1zm3 8V5.5a3 3 0 10-6 0V9h6z" clip-rule="evenodd" />
                        </svg>
                      </div>
                      
                      <!-- Standard toggle switch for other users -->
                      <label v-else class="relative inline-flex items-center cursor-pointer">
                        <input 
                          type="checkbox" 
                          v-model="userPermissions.reports[report.key]" 
                          class="sr-only peer"
                        >
                        <div class="w-9 h-5 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-emerald-500"></div>
                      </label>
                    </div>
                  </div>
                </div>
              </div>
            </section>

          </div>
        </template>
      </div>

    </div>
  </ReportLayout>
</template>
