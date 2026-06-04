
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/services/api'
import { useReportFilterStore } from './reportFilters'

/**
 * Safely parses the user from localStorage.
 * Prevents crashes if localStorage is blocked or not available in the environment.
 * @returns {object|null}
 */
const getSafeStoredUser = () => {
  try {
    const stored = localStorage.getItem('user')
    return stored ? JSON.parse(stored) : null
  } catch (error) {
    console.warn('[Auth Store] Failed to access localStorage user:', error)
    return null
  }
}

/**
 * Safely writes/deletes the user in localStorage.
 * Prevents crashes if localStorage is blocked or not available in the environment.
 * @param {object|null} userData
 */
const setSafeStoredUser = (userData) => {
  try {
    if (userData) {
      localStorage.setItem('user', JSON.stringify(userData))
    } else {
      localStorage.removeItem('user')
    }
  } catch (error) {
    console.warn('[Auth Store] Failed to write localStorage user:', error)
  }
}

export const useAuthStore = defineStore('auth', () => {
  // Since the JWT token is now locked in a secure HttpOnly cookie, we do not store
  // or manage it in client-side JavaScript. Instead, we maintain authentication
  // status by keeping non-sensitive user metadata to control user interface layouts.
  const user = ref(getSafeStoredUser())
  const isAuthenticated = ref(!!user.value)
  const isAdmin = computed(() => user.value?.role === 'admin')

  const userWidgets = ref([])
  const userReports = ref([])

  /**
   * Fetches the user widgets and reports permissions in parallel.
   * Uses Promise.allSettled to guarantee that one service's failure does not block the other.
   */
  const fetchPermissions = async () => {
    const companyId = user.value?.company_id
    const username = user.value?.username
    if (!companyId || !username) return

    try {
      const [widgetsResult, reportsResult] = await Promise.allSettled([
        api.get('/dashboard/my-widgets', { params: { company_id: companyId, username } }),
        api.get('/dashboard/my-reports', { params: { company_id: companyId, username } })
      ])

      if (widgetsResult.status === 'fulfilled') {
        userWidgets.value = (widgetsResult.value.data || []).map(w => w.widget_key)
      } else {
        console.error('[Auth Store] Failed to fetch user widgets:', widgetsResult.reason)
        userWidgets.value = []
      }

      if (reportsResult.status === 'fulfilled') {
        userReports.value = reportsResult.value.data || []
      } else {
        console.error('[Auth Store] Failed to fetch user reports:', reportsResult.reason)
        userReports.value = []
      }
    } catch (error) {
      console.error('[Auth Store] Unexpected error in fetchPermissions:', error)
    }
  }

  /**
   * Performs authentication login.
   * @param {string} username 
   * @param {string} password 
   * @param {string|number} companyId 
   * @returns {Promise<{success: boolean, message?: string}>}
   */
  const login = async (username, password, companyId) => {
    try {
      const payload = {
        username,
        password,
        company_id: companyId
      }

      // Post login request as application/json. Axios automatically accepts and manages the secure HttpOnly cookie
      // since 'withCredentials: true' is defined on our modular api client.
      const response = await api.post('/auth/login', payload)

      const userData = response.data?.user
      if (!userData) {
        throw new Error('Authentication response is missing user profile data.')
      }

      user.value = userData
      isAuthenticated.value = true

      // Store non-sensitive user profile strictly for visual rendering and persistence across reloads
      setSafeStoredUser(userData)

      // Inject the user's specific company_id into the global filter store
      const filterStore = useReportFilterStore()
      if (filterStore) {
        filterStore.companyId = userData.company_id
        filterStore.companyName = userData.company_name || ''
      }

      // Fetch user's active permissions right after login
      await fetchPermissions()

      return { success: true }
    } catch (error) {
      console.error('[Auth Store] Login failed:', error)
      let msg = 'Connection failed. Please try again.'

      if (error.response?.data?.detail) {
        msg = error.response.data.detail
      } else if (error.response?.data?.title) {
        msg = error.response.data.title
      } else if (error.message) {
        msg = error.message
      }

      return { success: false, message: msg }
    }
  }

  /**
   * Logs out the user by terminating the backend session and purging frontend cache and state.
   */
  const logout = async () => {
    try {
      // 1. Terminate the HttpOnly cookie session in the backend
      await api.post('/auth/logout')
    } catch (error) {
      console.error('[Auth Store] Backend session clear failed. Running frontend clear fallback:', error)
    } finally {
      // 2. Clear all local user states and purge cache in all circumstances
      user.value = null
      isAuthenticated.value = false
      userWidgets.value = []
      userReports.value = []
      setSafeStoredUser(null)
    }
  }

  return { user, isAuthenticated, isAdmin, userWidgets, userReports, fetchPermissions, login, logout }
})
