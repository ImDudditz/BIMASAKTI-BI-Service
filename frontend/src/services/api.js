import axios from 'axios'

// 1. Dynamically determine the backend URL based on environment mode
// In development, we use Vite's server proxy on '/api' to avoid SSL certificate trust issues on different ports.
const backendUrl = import.meta.env.PROD
  ? (import.meta.env.VITE_API_BASE_URL || '/api')
  : '/api'

// 3. Create the modular Axios instance
const api = axios.create({
  baseURL: backendUrl,
  // Tell Axios to automatically include cookies (such as our HttpOnly JWT token)
  // in all cross-domain API requests. (Equivalent to fetch's credentials: 'include')
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json'
  }
})

// Add an interceptor to catch global errors
api.interceptors.response.use(
  response => response,
  error => {
    console.error("API Engine Error:", error)
    return Promise.reject(error)
  }
)

export default api