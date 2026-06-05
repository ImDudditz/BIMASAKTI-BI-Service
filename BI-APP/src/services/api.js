import axios from 'axios'
import router from '@/router'
import { useAuthStore } from '@/stores/auth'

// 1. Dynamically determine the backend URL based on environment mode
// In development, we use Vite's server proxy on '/api' to avoid SSL certificate trust issues on different ports.
const backendUrl =
  window.CONFIG && window.CONFIG.API_BASE_URL
    ? window.CONFIG.API_BASE_URL
    : import.meta.env.PROD
      ? import.meta.env.VITE_API_BASE_URL || '/api'
      : '/api'

// 3. Create the modular Axios instance
const api = axios.create({
  baseURL: backendUrl,
  // Tell Axios to automatically include cookies (such as our HttpOnly JWT token)
  // in all cross-domain API requests. (Equivalent to fetch's credentials: 'include')
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
})

// Add an interceptor to catch global errors and handle session expiration redirects
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    console.error('API Engine Error:', error)

    if (error.response && error.response.status === 401) {
      try {
        const authStore = useAuthStore()
        await authStore.logout()
      } catch (storeError) {
        console.error('Failed to clear auth state on 401:', storeError)
      }

      // Kick back to Landing Page
      router.push('/')
    }

    return Promise.reject(error)
  },
)

export default api
