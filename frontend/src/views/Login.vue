<!-- eslint-disable vue/multi-word-component-names -->
<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
const bimasaktiLogo = new URL('../../../backend/assets/BMS/img/BMS_logo.png', import.meta.url).href

const router = useRouter()
const authStore = useAuthStore()

const username = ref('')
const password = ref('')
const companyId = ref('')
const errorMsg = ref('')
const isLoading = ref(false)

const handleLogin = async () => {
  if (!username.value || !password.value || !companyId.value) {
    errorMsg.value = "Please enter all fields."
    return
  }

  isLoading.value = true
  errorMsg.value = ''

  const result = await authStore.login(username.value, password.value, companyId.value)

  if (result.success) {
    router.push('/dashboard')
  } else {
    errorMsg.value = result.message
    isLoading.value = false
  }
}
</script>

<template>
  <div class="min-h-screen w-full flex items-center justify-center font-sans overflow-hidden relative bg-slate-100">
    
    <!-- Phase 2: Architectural Background Overlay (Reference Image) -->
    <div class="absolute inset-0 z-0 opacity-20 pointer-events-none">
      <img src="https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?q=80&w=2070&auto=format&fit=crop" class="w-full h-full object-cover grayscale" alt="background">
    </div>
    <div class="absolute inset-0 z-0 bg-white/40 backdrop-blur-[2px]"></div>

    <!-- Main Container -->
    <div class="w-full max-w-6xl p-6 md:p-12 flex flex-col md:flex-row items-center justify-between gap-12 relative z-10">
      
      <!-- Left Side: Branding (Reference Image Layout) -->
      <div class="flex flex-col items-center md:items-start text-center md:text-left max-w-md animate-fade-in">
        <div class="mb-6">
          <!-- BIMASAKTI Logo -->
          <img :src="bimasaktiLogo" alt="Bimasakti Logo" class="h-20 w-auto object-contain">
        </div>
        <h1 class="text-4xl md:text-5xl font-black text-sky-950 tracking-tighter mb-4 leading-tight">
          BIMASAKTI<br><span class="text-[#1877F2]">BI SERVICE</span>
        </h1>
        <div class="h-1.5 w-24 bg-[#1877F2] rounded-full mb-6"></div>
        <p class="text-slate-500 font-medium leading-relaxed">
          Unlock your property's potential with advanced business intelligence and strategic financial clarity.
        </p>
      </div>

      <div class="w-full max-w-[420px] animate-slide-up">
        <div class="bg-linear-to-b from-[#1877F2] to-[#1565C0] rounded-[32px] shadow-2xl p-1 pb-1.5">
          <div class="bg-linear-to-b from-[#1877F2]/95 to-[#1565C0]/95 backdrop-blur-sm rounded-[30px] p-8 md:p-10 text-white">
            
            <div class="text-center mb-8">
              <h2 class="text-3xl font-black tracking-tight mb-1">Welcome !</h2>
              <p class="text-sm font-semibold text-white/70 mb-3">Please login first</p>
              <div class="w-12 h-1 bg-white/30 mx-auto rounded-full"></div>
            </div>

            <!-- Phase 3: Form Injection (Functional Preservation) -->
            <form @submit.prevent="handleLogin" class="flex flex-col gap-6">
              
              <!-- Error Alert -->
              <div v-if="errorMsg" class="bg-white/10 border border-white/20 backdrop-blur-md text-white text-xs font-bold px-4 py-3 rounded-xl flex items-center gap-2 animate-bounce">
                <span>⚠️</span> {{ errorMsg }}
              </div>

              <!-- Input Group: Company ID -->
              <div class="flex flex-col gap-2">
                <div class="relative group">
                  <div class="absolute left-4 top-1/2 -translate-y-1/2 text-white/60 group-focus-within:text-white transition-colors">
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-7h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"></path></svg>
                  </div>
                  <input 
                    v-model="companyId" 
                    type="text" 
                    placeholder="Company ID"
                    class="w-full bg-white/20 border border-white/20 rounded-2xl pl-12 pr-4 py-4 text-sm font-bold text-white placeholder-white/50 focus:outline-none focus:bg-white/30 focus:border-white/40 focus:ring-4 focus:ring-white/10 transition-all"
                    required
                  >
                </div>
              </div>

              <!-- Input Group: Username -->
              <div class="flex flex-col gap-2">
                <div class="relative group">
                  <div class="absolute left-4 top-1/2 -translate-y-1/2 text-white/60 group-focus-within:text-white transition-colors">
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"></path></svg>
                  </div>
                  <input 
                    v-model="username" 
                    type="text" 
                    placeholder="Username"
                    class="w-full bg-white/20 border border-white/20 rounded-2xl pl-12 pr-4 py-4 text-sm font-bold text-white placeholder-white/50 focus:outline-none focus:bg-white/30 focus:border-white/40 focus:ring-4 focus:ring-white/10 transition-all"
                    required
                  >
                </div>
              </div>

              <!-- Input Group: Password -->
              <div class="flex flex-col gap-2">
                <div class="relative group">
                  <div class="absolute left-4 top-1/2 -translate-y-1/2 text-white/60 group-focus-within:text-white transition-colors">
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"></path></svg>
                  </div>
                  <input 
                    v-model="password" 
                    type="password" 
                    placeholder="Password"
                    class="w-full bg-white/20 border border-white/20 rounded-2xl pl-12 pr-4 py-4 text-sm font-bold text-white placeholder-white/50 focus:outline-none focus:bg-white/30 focus:border-white/40 focus:ring-4 focus:ring-white/10 transition-all"
                    required
                  >
                </div>
              </div>

              <div class="flex items-center justify-between px-2">
                <label class="flex items-center gap-2 cursor-pointer group">
                  <input type="checkbox" class="w-4 h-4 rounded border-white/30 bg-white/10 text-[#1877F2] focus:ring-white/50">
                  <span class="text-xs font-bold text-white/80 group-hover:text-white transition-colors">Remember Me</span>
                </label>
                <a href="#" class="text-xs font-bold text-white/80 hover:text-white underline decoration-white/20 underline-offset-4 transition-colors">Forgot Password?</a>
              </div>

              <button 
                type="submit" 
                :disabled="isLoading"
                class="mt-2 w-full bg-white text-[#1877F2] hover:bg-blue-50 disabled:bg-white/50 disabled:text-blue-600/50 disabled:cursor-not-allowed font-black text-sm py-4 rounded-2xl shadow-xl shadow-blue-950/20 transition-all active:scale-[0.98] flex items-center justify-center gap-2"
              >
                <svg v-if="isLoading" class="animate-spin h-5 w-5 text-[#1877F2]" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                {{ isLoading ? 'AUTHENTICATING...' : 'SECURE LOGIN' }}
              </button>
              
              <!-- System Info (Reference Image Bottom Section) -->
              <div class="mt-4 bg-sky-950/20 rounded-2xl p-4 border border-white/10">
                <p class="text-[10px] text-center font-bold text-white/70 leading-relaxed uppercase tracking-wider">
                  To protect against unauthorized access, your session will automatically time out after a period of inactivity.
                </p>
              </div>

            </form>
          </div>
        </div>
      </div>
      
    </div>

    <!-- Page Footer -->
    <div class="absolute bottom-6 w-full text-center z-10">
      <p class="text-[11px] font-black text-slate-400 uppercase tracking-widest">© 2026 Bimasakti Executive Financial Systems</p>
    </div>

  </div>
</template>

<style scoped>
.animate-slide-up { animation: slideUp 0.5s cubic-bezier(0.16, 1, 0.3, 1) forwards; }
@keyframes slideUp { 
  from { opacity: 0; transform: translateY(30px); } 
  to { opacity: 1; transform: translateY(0); } 
}
</style>
