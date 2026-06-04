import { createRouter, createWebHashHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

import fsDashboard from '@/views/Dashboard/Financial/fsDashboard.vue'
import BalanceSheet from '@/views/Reports/Financial/BalanceSheet.vue'
import IncomeStatement from '@/views/Reports/Financial/IncomeStatement.vue'
// THE FIX: Added the /Settings/ directory to the import path
import AccountMapping from '@/views/Settings/AccountMapping.vue'
import Login from '@/views/Login.vue'
import Landing from '@/views/Landing.vue'

const routes = [
  {
    path: '/login',
    name: 'Login',
    component: Login,
    meta: { requiresAuth: false }
  },
  {
    path: '/',
    name: 'Landing',
    component: Landing,
    meta: { requiresAuth: false }
  },
  {
    path: '/dashboard',
    name: 'Dashboard',
    component: fsDashboard,
    meta: { requiresAuth: true }
  },
  {
    path: '/dashboard/operation',
    name: 'OpsDashboard',
    component: () => import('@/views/Dashboard/Operation/OpsDashboard.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/dashboard/maintenance',
    name: 'SmDashboard',
    component: () => import('@/views/Dashboard/ServiceAndMaintenance/SmDashboard.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/balance-sheet',
    name: 'BalanceSheet',
    component: BalanceSheet,
    meta: { requiresAuth: true }
  },
  {
    path: '/income-statement',
    name: 'IncomeStatement',
    component: IncomeStatement,
    meta: { requiresAuth: true }
  },
  {
    path: '/settings',
    name: 'AccountMapping',
    component: AccountMapping,
    meta: { requiresAuth: true }
  },
  {
    path: '/overview',
    name: 'Overview',
    component: () => import('@/views/Overview.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/print',
    name: 'PrintReports',
    component: () => import('@/views/Reports/Print.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/tenancy-report',
    name: 'TenancyReport',
    component: () => import('@/views/Reports/TenancyReportPlaceholder.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/services-report',
    name: 'ServicesReport',
    component: () => import('@/views/Reports/ServicesReportPlaceholder.vue'),
    meta: { requiresAuth: true }
  }
]

const router = createRouter({
  history: createWebHashHistory(import.meta.env.BASE_URL),
  routes
})

// GLOBAL ROUTER GUARD
router.beforeEach((to, _from) => {
  const authStore = useAuthStore()
  
  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    // Force to login if attempting to access a secured route without a token
    return { name: 'Login' }
  } 
  
  if (to.meta.requiresAdmin && !authStore.isAdmin) {
    // Redirect non-admins away from admin pages
    return { name: 'Overview' }
  }
  
  if (to.name === 'Login' && authStore.isAuthenticated) {
    // Prevent logged-in users from seeing the login page
    return { name: 'Overview' }
  }
})

export default router