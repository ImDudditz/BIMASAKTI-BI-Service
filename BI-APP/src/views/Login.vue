<!-- eslint-disable vue/multi-word-component-names -->
<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
const bimasaktiLogo = new URL('../../../BI-API/assets/BMS/img/BMS_logo.png', import.meta.url).href

const router = useRouter()
const authStore = useAuthStore()

const username = ref('')
const password = ref('')
const companyId = ref('')
const errorMsg = ref('')
const isLoading = ref(false)

const handleLogin = async () => {
  if (!username.value || !password.value || !companyId.value) {
    errorMsg.value = 'Please enter all fields.'
    return
  }

  isLoading.value = true
  errorMsg.value = ''

  const result = await authStore.login(username.value, password.value, companyId.value)

  if (result.success) {
    router.push('/overview')
  } else {
    errorMsg.value = result.message
    isLoading.value = false
  }
}
</script>

<template>
  <div
    class="min-h-screen w-full flex items-center justify-center font-sans overflow-hidden relative bg-[#f4f6fa]"
  >
    <!-- Fluent Background Blobs for Windows 11 Acrylic Look -->
    <div
      class="absolute top-[-10%] right-[-10%] w-[50vw] h-[50vw] rounded-full bg-blue-500/10 blur-[120px] pointer-events-none z-0"
    ></div>
    <div
      class="absolute bottom-[-10%] left-[-10%] w-[45vw] h-[45vw] rounded-full bg-purple-500/10 blur-[100px] pointer-events-none z-0"
    ></div>

    <!-- Main Container -->
    <div
      class="w-full max-w-6xl p-6 md:p-12 flex flex-col md:flex-row items-center justify-between gap-12 relative z-10"
    >
      <!-- Left Side: Branding (Windows 11 fluent styling) -->
      <div
        class="flex flex-col items-center md:items-start text-center md:text-left max-w-md animate-fade-in"
      >
        <div class="mb-6 cursor-pointer" @click="router.push('/')">
          <!-- BIMASAKTI Logo -->
          <img :src="bimasaktiLogo" alt="Bimasakti Logo" class="h-20 w-auto object-contain" />
        </div>
        <h1
          class="text-4xl md:text-5xl font-black text-slate-900 tracking-tighter mb-4 leading-tight"
        >
          BIMASAKTI<br /><span
            class="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent"
            >BI SERVICE</span
          >
        </h1>
        <div class="h-1.5 w-24 bg-blue-600 rounded-full mb-6"></div>
        <p class="text-slate-500 font-semibold leading-relaxed">
          Unlock your property's potential with advanced business intelligence and strategic
          financial clarity.
        </p>
      </div>

      <!-- Right Side: Windows 11 Fluent Dialog Box -->
      <div class="w-full max-w-[420px] animate-slide-up">
        <div
          class="bg-white/70 backdrop-blur-xl border border-white/50 rounded-[32px] shadow-2xl p-8 md:p-10 text-slate-800"
        >
          <div class="text-center mb-8">
            <h2 class="text-3xl font-black tracking-tight text-slate-900 mb-1">Welcome!</h2>
            <p class="text-xs font-bold text-slate-400 mb-3">Please sign in to secure portal</p>
            <div class="w-12 h-1 bg-slate-200 mx-auto rounded-full"></div>
          </div>

          <!-- Phase 3: Form Injection (Functional Preservation) -->
          <form @submit.prevent="handleLogin" class="flex flex-col gap-5">
            <!-- Error Alert -->
            <div
              v-if="errorMsg"
              class="bg-red-50 border border-red-200 text-red-700 text-xs font-bold px-4 py-3 rounded-2xl flex items-center gap-2 animate-bounce"
            >
              <span>⚠️</span> {{ errorMsg }}
            </div>

            <!-- Input Group: Company ID -->
            <div class="flex flex-col gap-2">
              <div class="relative group">
                <div
                  class="absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 group-focus-within:text-blue-600 transition-colors"
                >
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      stroke-width="2"
                      d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-7h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
                    ></path>
                  </svg>
                </div>
                <input
                  v-model="companyId"
                  type="text"
                  placeholder="Company ID"
                  class="w-full bg-white/80 border border-slate-200/60 rounded-2xl pl-12 pr-4 py-4 text-sm font-bold text-slate-800 placeholder-slate-400 focus:outline-none focus:bg-white focus:border-blue-500 focus:ring-4 focus:ring-blue-100 transition-all"
                  required
                />
              </div>
            </div>

            <!-- Input Group: Username -->
            <div class="flex flex-col gap-2">
              <div class="relative group">
                <div
                  class="absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 group-focus-within:text-blue-600 transition-colors"
                >
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      stroke-width="2"
                      d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"
                    ></path>
                  </svg>
                </div>
                <input
                  v-model="username"
                  type="text"
                  placeholder="Username"
                  class="w-full bg-white/80 border border-slate-200/60 rounded-2xl pl-12 pr-4 py-4 text-sm font-bold text-slate-800 placeholder-slate-400 focus:outline-none focus:bg-white focus:border-blue-500 focus:ring-4 focus:ring-blue-100 transition-all"
                  required
                />
              </div>
            </div>

            <!-- Input Group: Password -->
            <div class="flex flex-col gap-2">
              <div class="relative group">
                <div
                  class="absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 group-focus-within:text-blue-600 transition-colors"
                >
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      stroke-width="2"
                      d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"
                    ></path>
                  </svg>
                </div>
                <input
                  v-model="password"
                  type="password"
                  placeholder="Password"
                  class="w-full bg-white/80 border border-slate-200/60 rounded-2xl pl-12 pr-4 py-4 text-sm font-bold text-slate-800 placeholder-slate-400 focus:outline-none focus:bg-white focus:border-blue-500 focus:ring-4 focus:ring-blue-100 transition-all"
                  required
                />
              </div>
            </div>

            <div class="flex items-center justify-between px-2">
              <label class="flex items-center gap-2 cursor-pointer group">
                <input
                  type="checkbox"
                  class="w-4 h-4 rounded border-slate-300 bg-white text-blue-600 focus:ring-blue-500"
                />
                <span
                  class="text-xs font-bold text-slate-500 group-hover:text-slate-700 transition-colors"
                  >Remember Me</span
                >
              </label>
              <a
                href="#"
                class="text-xs font-bold text-slate-500 hover:text-blue-600 underline decoration-slate-200 underline-offset-4 transition-colors"
                >Forgot Password?</a
              >
            </div>

            <button
              type="submit"
              :disabled="isLoading"
              class="mt-2 w-full bg-gradient-to-r from-blue-600 to-blue-700 hover:from-blue-700 hover:to-blue-800 text-white font-black text-sm py-4 rounded-2xl shadow-xl shadow-blue-500/10 active:scale-[0.98] transition-all flex items-center justify-center gap-2 disabled:opacity-50"
            >
              <svg
                v-if="isLoading"
                class="animate-spin h-5 w-5 text-white"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
              >
                <circle
                  class="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  stroke-width="4"
                ></circle>
                <path
                  class="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                ></path>
              </svg>
              {{ isLoading ? 'AUTHENTICATING...' : 'SECURE LOGIN' }}
            </button>

            <!-- System Info (Reference Image Bottom Section) -->
            <div class="mt-4 bg-slate-50 rounded-2xl p-4 border border-slate-100">
              <p
                class="text-[10px] text-center font-bold text-slate-400 leading-relaxed uppercase tracking-wider"
              >
                To protect against unauthorized access, your session will automatically time out
                after a period of inactivity.
              </p>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Page Footer -->
    <div class="absolute bottom-6 w-full text-center z-10">
      <p class="text-[11px] font-black text-slate-400 uppercase tracking-widest">
        © 2026 Bimasakti Executive Financial Systems
      </p>
    </div>
  </div>
</template>

<style scoped>
.animate-slide-up {
  animation: slideUp 0.5s cubic-bezier(0.16, 1, 0.3, 1) forwards;
}
@keyframes slideUp {
  from {
    opacity: 0;
    transform: translateY(30px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>
