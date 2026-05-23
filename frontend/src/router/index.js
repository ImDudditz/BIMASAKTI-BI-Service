import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

import fsDashboard from '@/views/Dashboard/Financial/fsDashboard.vue'
import BalanceSheet from '@/views/Reports/Financial/BalanceSheet.vue'
import IncomeStatement from '@/views/Reports/Financial/IncomeStatement.vue'
// THE FIX: Added the /Settings/ directory to the import path
import AccountMapping from '@/views/Settings/AccountMapping.vue'
import Login from '@/views/Login.vue'

const routes = [
  {
    path: '/login',
    name: 'Login',
    component: Login,
    meta: { requiresAuth: false }
  },
  {
    path: '/',
    redirect: '/dashboard'
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
    path: '/admin',
    name: 'AdminAccess',
    component: () => import('../views/AdminPanels/AdminAccess.vue'),
    meta: { requiresAuth: true, requiresAdmin: true }
  }
]

const router = createRouter({
  history: createWebHistory(),
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
    return { name: 'Dashboard' }
  }
  
  if (to.name === 'Login' && authStore.isAuthenticated) {
    // Prevent logged-in users from seeing the login page
    return { name: 'Dashboard' }
  }
})

export default router