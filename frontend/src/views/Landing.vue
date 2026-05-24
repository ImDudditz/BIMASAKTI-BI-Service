<script setup>
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const bimasaktiLogo = new URL('../../../backend/assets/BMS/img/BMS_logo.png', import.meta.url).href
const biDashboardPreview = new URL('../assets/bi_dashboard_preview.png', import.meta.url).href
const propertyOperations = new URL('../assets/property_operations.png', import.meta.url).href
const tenantMobileApp = new URL('../assets/tenant_mobile_app.png', import.meta.url).href

const router = useRouter()
const authStore = useAuthStore()

const handleActionClick = () => {
  if (authStore.isAuthenticated) {
    router.push('/dashboard')
  } else {
    router.push('/login')
  }
}

// Scroll detection for scroll-triggered slide-up/fade-in animations
onMounted(() => {
  document.title = 'BIMASAKTI BI'

  const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        entry.target.classList.add('in-view')
      }
    });
  }, { threshold: 0.1 });

  document.querySelectorAll('.scroll-animate').forEach(el => {
    observer.observe(el)
  });
})

// CSS smooth scroll anchor navigation helper
const scrollToSection = (id) => {
  const element = document.getElementById(id)
  if (element) {
    element.scrollIntoView({ behavior: 'smooth' })
  }
}
</script>

<template>
  <div class="h-full w-full bg-[#f4f6fa] text-slate-800 font-sans overflow-y-auto overflow-x-hidden scroll-smooth relative custom-scroll">
    
    <!-- Phase 2: Architectural Background Overlay (Reference Image) -->
    <div class="absolute top-0 left-0 w-full h-[90vh] z-0 opacity-[0.06] pointer-events-none overflow-hidden">
      <img src="https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?q=80&w=2070&auto=format&fit=crop" class="w-full h-full object-cover grayscale" alt="background">
    </div>
    
    <!-- Fluent Background Blobs for Windows 11 Acrylic Look -->
    <div class="absolute top-[-10%] right-[-10%] w-[50vw] h-[50vw] rounded-full bg-blue-400/10 blur-[120px] pointer-events-none z-0"></div>
    <div class="absolute top-[40%] left-[-10%] w-[45vw] h-[45vw] rounded-full bg-purple-400/10 blur-[100px] pointer-events-none z-0"></div>

    <!-- --- LANDING HEADER --- -->
    <header class="w-full max-w-7xl mx-auto px-6 py-5 flex items-center justify-between relative z-20 border-b border-slate-200/40">
      <!-- Logo removed per request -->
      <div></div>

      <!-- Nav Items -->
      <nav class="hidden md:flex items-center gap-8">
        <button @click="scrollToSection('about')" class="text-sm font-semibold text-slate-600 hover:text-blue-600 transition-colors">System Overview</button>
        <button @click="scrollToSection('features')" class="text-sm font-semibold text-slate-600 hover:text-blue-600 transition-colors">Key Modules</button>
        <button @click="scrollToSection('azure')" class="text-sm font-semibold text-slate-600 hover:text-blue-600 transition-colors">Cloud Infrastructure</button>
      </nav>

      <button 
        @click="handleActionClick" 
        class="bg-blue-600 hover:bg-blue-700 text-white font-semibold text-xs px-5 py-2.5 rounded-lg shadow-sm hover:shadow-md transition-all active:scale-[0.98] relative z-10"
      >
        {{ authStore.isAuthenticated ? 'Access Dashboard' : 'Sign In to Portal' }}
      </button>
    </header>

    <!-- --- HERO SECTION --- -->
    <section class="max-w-6xl mx-auto px-6 pt-16 pb-20 text-center relative z-10 flex flex-col items-center">
      <!-- Centered Hero Logo matching Title Scale -->
      <img :src="bimasaktiLogo" alt="Bimasakti Logo" class="h-24 sm:h-28 md:h-32 w-auto object-contain mb-8">

      <h1 class="text-4xl sm:text-5xl md:text-6xl font-black text-slate-900 tracking-tight leading-none max-w-4xl mb-6">
        Business <span class="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">Dashboard Service</span>
      </h1>

      <p class="text-base sm:text-lg text-slate-500 font-medium max-w-2xl leading-relaxed mb-10">
        Engineered by PT Realta Chakradarma, Bimasakti unifies operational pipelines, billing tax processes, and tenant mobile requests into a robust, Azure-backed business intelligence center.
      </p>

      <div class="flex flex-col sm:flex-row gap-4 mb-16">
        <button 
          @click="handleActionClick" 
          class="bg-blue-600 hover:bg-blue-700 text-white font-semibold text-sm px-8 py-3.5 rounded-lg shadow-lg shadow-blue-500/10 hover:shadow-xl hover:shadow-blue-500/20 active:scale-[0.98] transition-all flex items-center justify-center"
        >
          {{ authStore.isAuthenticated ? 'Access Dashboard' : 'Sign In to Portal' }}
        </button>
        <button 
          @click="scrollToSection('about')" 
          class="bg-white hover:bg-slate-50 text-slate-700 border border-slate-200 font-semibold text-sm px-8 py-3.5 rounded-lg shadow-sm active:scale-[0.98] transition-all"
        >
          System Overview
        </button>
      </div>

      <!-- --- HERO IMAGE PREVIEW --- -->
      <div class="w-full max-w-5xl scroll-animate slide-up bg-white/40 backdrop-blur-md border border-white/50 rounded-3xl p-4 sm:p-5 shadow-2xl relative">
        <div class="rounded-2xl overflow-hidden border border-slate-100 shadow-inner max-h-[480px]">
          <img :src="biDashboardPreview" alt="Bimasakti BI Dashboard Preview" class="w-full h-full object-cover">
        </div>
      </div>
    </section>

    <!-- --- ABOUT / INTRODUCTION SECTION --- -->
    <section id="about" class="bg-white border-y border-slate-200/50 py-24 relative z-10">
      <div class="max-w-6xl mx-auto px-6">
        
        <div class="flex flex-col md:flex-row items-center gap-12 sm:gap-16">
          <div class="flex-1 scroll-animate slide-up">
            <span class="text-xs font-black text-blue-600 uppercase tracking-widest block mb-3">Enterprise Suite</span>
            <h2 class="text-3xl font-black text-slate-900 tracking-tight leading-tight mb-6">
              Complete Operations Control for Property Leaders
            </h2>
            <p class="text-sm text-slate-500 font-medium leading-relaxed mb-6">
              Bimasakti is a comprehensive property management system designed to coordinate all Operational, Financial, and Tenant processes under a single database structure.
            </p>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-6">
              <div class="bg-slate-50 p-4 border border-slate-100 rounded-2xl">
                <h4 class="text-xs font-black text-slate-900 uppercase">🏢 Commercial Malls</h4>
                <p class="text-[11px] text-slate-400 font-semibold mt-1">Track rental units, occupancy analytics, sales campaign, and shopping center operational workflows.</p>
              </div>
              <div class="bg-slate-50 p-4 border border-slate-100 rounded-2xl">
                <h4 class="text-xs font-black text-slate-900 uppercase">🏢 Residential Assets</h4>
                <p class="text-[11px] text-slate-400 font-semibold mt-1">Streamline apartment tenant handovers, utility billings, maintenance requests, and facilities scheduling.</p>
              </div>
            </div>
          </div>

          <div class="flex-1 scroll-animate slide-up rounded-3xl overflow-hidden shadow-2xl border border-slate-200" style="transition-delay: 0.15s;">
            <img :src="propertyOperations" alt="Realta Bimasakti Property Operations" class="w-full h-full object-cover">
          </div>
        </div>

      </div>
    </section>

    <!-- --- FEATURE SEGMENTS (THREE CORE PILLARS) --- -->
    <section id="features" class="py-24 relative z-10 max-w-6xl mx-auto px-6">
      
      <div class="text-center max-w-2xl mx-auto mb-20 scroll-animate fade-in">
        <span class="text-xs font-black text-blue-600 uppercase tracking-widest block mb-3">Modular Architecture</span>
        <h3 class="text-3xl font-black text-slate-900 tracking-tight leading-tight">
          Coordinated operational streams working as a BI Service
        </h3>
        <p class="text-sm text-slate-500 font-medium leading-relaxed mt-4">
          Unify accounting ledgers, engineering schedules, and tenant requests. Check how our system coordinates these operational nodes natively:
        </p>
      </div>

      <!-- Segment 1: Financial & Billing Accounting -->
      <div class="flex flex-col md:flex-row items-center gap-12 sm:gap-16 mb-24">
        <div class="flex-1 scroll-animate slide-up">
          <span class="text-xs font-black text-blue-600 uppercase tracking-widest block mb-2">Module 01</span>
          <h4 class="text-2xl font-black text-slate-900 mb-4">Billing, Accounting, & VAT Automated Processing</h4>
          <p class="text-sm text-slate-500 leading-relaxed font-semibold mb-4">
            Bimasakti BI unifies operational property streams with financial control tools. Automatically generate invoices, reconcile accounting ledger statements, and streamline tax reporting with direct link integrations to e-Faktur PPN.
          </p>
          <ul class="text-xs font-bold text-slate-500 flex flex-col gap-2">
            <li class="flex items-center gap-2">✅ Automatic billing calculation and secure invoicing</li>
            <li class="flex items-center gap-2">✅ General Ledger, Trial Balance, & balance sheets compiler</li>
            <li class="flex items-center gap-2">✅ Integrated e-Faktur tax processing</li>
          </ul>
        </div>
        <div class="flex-1 scroll-animate slide-up rounded-3xl overflow-hidden shadow-2xl border border-slate-200" style="transition-delay: 0.15s;">
          <img :src="biDashboardPreview" alt="Executive Billing & Accounting BI" class="w-full h-full object-cover">
        </div>
      </div>

      <!-- Segment 2: Tenant Mobile Solutions (24/7 Access) -->
      <div class="flex flex-col-reverse md:flex-row items-center gap-12 sm:gap-16">
        <div class="flex-1 scroll-animate slide-up rounded-3xl overflow-hidden shadow-2xl border border-slate-200">
          <img :src="tenantMobileApp" alt="Bimasakti Tenant Mobile Portal App" class="w-full h-full object-cover">
        </div>
        <div class="flex-1 scroll-animate slide-up" style="transition-delay: 0.15s;">
          <span class="text-xs font-black text-purple-600 uppercase tracking-widest block mb-2">Module 02</span>
          <h4 class="text-2xl font-black text-slate-900 mb-4">24/7 Tenant Mobile Portal & Maintenance System</h4>
          <p class="text-sm text-slate-500 leading-relaxed font-semibold mb-4">
            Drastically improve tenant relationships by providing 24/7 service transparency. Through the dedicated mobile application, tenants can verify active billing statements, submit maintenance tickets, and track engineering repair statuses in real-time.
          </p>
          <ul class="text-xs font-bold text-slate-500 flex flex-col gap-2">
            <li class="flex items-center gap-2">✅ Mobile billing transparency and payment gateway</li>
            <li class="flex items-center gap-2">✅ 24/7 Service Desk & engineering ticket submission</li>
            <li class="flex items-center gap-2">✅ Uptime status monitoring for facilities</li>
          </ul>
        </div>
      </div>

    </section>

    <!-- --- CLOUD DEPLOYMENT & AZURE INFRASTRUCTURE --- -->
    <section id="azure" class="bg-slate-900 text-white py-24 relative z-10 border-y border-slate-800">
      <div class="max-w-6xl mx-auto px-6 flex flex-col md:flex-row items-center gap-12 sm:gap-16">
        <div class="flex-1 scroll-animate slide-up">
          <span class="text-xs font-black text-blue-400 uppercase tracking-widest block mb-3">Enterprise Cloud</span>
          <h3 class="text-3xl font-black text-slate-100 tracking-tight leading-tight mb-6">
            Cloud-Based Deployment with Microsoft Azure Security
          </h3>
          <p class="text-sm text-slate-400 font-semibold leading-relaxed mb-6">
            Bimasakti as a Service leverages **Microsoft Azure**'s advanced cloud computing architecture. This cloud collaboration provides maximum data security, strict confidentiality protection, 99.9% system uptime, and minimal downtime.
          </p>
          <div class="flex flex-col gap-4">
            <div class="flex gap-4">
              <span class="text-xl bg-slate-800 p-2 rounded-xl h-10 w-10 flex items-center justify-center border border-slate-700 shrink-0">🛡️</span>
              <div>
                <h4 class="text-sm font-black text-slate-100">Data Integrity Protection</h4>
                <p class="text-xs text-slate-400 leading-relaxed font-semibold mt-1">Advanced SQLite encryption modules and TLS connection handshakes protect tenant transaction datasets.</p>
              </div>
            </div>
            <div class="flex gap-4">
              <span class="text-xl bg-slate-800 p-2 rounded-xl h-10 w-10 flex items-center justify-center border border-slate-700 shrink-0">☁️</span>
              <div>
                <h4 class="text-sm font-black text-slate-100">Azure Backup Recovery</h4>
                <p class="text-xs text-slate-400 leading-relaxed font-semibold mt-1">Automatic backups ensure that historical ledger reports can be recovered instantly in the event of an operational failure.</p>
              </div>
            </div>
          </div>
        </div>

        <div class="flex-1 w-full scroll-animate slide-up bg-slate-950 rounded-3xl p-6 shadow-2xl border border-slate-800/80" style="transition-delay: 0.15s;">
          <div class="flex items-center justify-between border-b border-slate-800 pb-4 mb-4">
            <div class="flex items-center gap-2">
              <span class="w-3 h-3 rounded-full bg-red-500"></span>
              <span class="w-3 h-3 rounded-full bg-yellow-500"></span>
              <span class="w-3 h-3 rounded-full bg-green-500"></span>
            </div>
            <span class="text-[10px] font-bold text-slate-500 font-mono">BimasaktiAzureConsole.sh</span>
          </div>
          <pre class="text-[11px] font-mono text-cyan-400 overflow-x-auto leading-relaxed"><code class="language-bash"># Checking Cloud Deployment Health on Microsoft Azure
$ az webapp show --name BimasaktiService --resource-group RealtaGroup

{
  "state": "Running",
  "location": "Southeast Asia (Singapore)",
  "sku": "Premium_V3",
  "httpsOnly": true,
  "clientCertEnabled": true,
  "containerUptime": "99.987%",
  "securityPatches": "Up-to-date",
  "dataReplication": "Geo-redundant Active"
}
</code></pre>
        </div>
      </div>
    </section>

    <!-- --- EXECUTIVE TESTIMONIAL --- -->
    <section class="bg-blue-600 text-white py-20 relative z-10">
      <div class="max-w-4xl mx-auto px-6 text-center scroll-animate fade-in">
        <span class="text-4xl block mb-6">“</span>
        <blockquote class="text-lg sm:text-xl font-bold leading-relaxed mb-8 font-serif italic">
          Bimasakti Property Management System unifies operational billing records, tenant requests, and accounting ledgers into a robust, Azure-backed dashboard. Realta's software has accelerated our financial oversight.
        </blockquote>
        <div class="w-12 h-1 bg-white/30 mx-auto rounded-full mb-4"></div>
        <cite class="text-xs font-black uppercase tracking-widest text-blue-200">PT Realta Chakradarma &bull; Strategic Enterprise Solutions</cite>
      </div>
    </section>

    <!-- --- CALL TO ACTION --- -->
    <section class="max-w-6xl mx-auto px-6 py-24 text-center scroll-animate slide-up relative z-10 flex flex-col items-center">
      <h3 class="text-3xl sm:text-4xl font-black text-slate-900 tracking-tight leading-none mb-6">
        Ready to orchestrate your properties?
      </h3>
      <p class="text-sm text-slate-500 font-medium max-w-md leading-relaxed mb-8">
        Access the secure portal and begin managing dashboards, billing accounting parameters, and synchronizations.
      </p>
      <button 
        @click="handleActionClick" 
        class="bg-blue-600 hover:bg-blue-700 text-white font-semibold text-sm px-8 py-3.5 rounded-lg shadow-lg shadow-blue-500/10 hover:shadow-xl hover:shadow-blue-500/20 active:scale-[0.98] transition-all"
      >
        {{ authStore.isAuthenticated ? 'Access Dashboard' : 'Sign In to Portal' }}
      </button>
    </section>

    <!-- --- FOOTER --- -->
    <footer class="w-full bg-slate-950 border-t border-slate-900 text-slate-500 py-12 relative z-10">
      <div class="max-w-7xl mx-auto px-6 flex flex-col sm:flex-row justify-between items-center gap-6">
        <div class="flex items-center gap-3">
          <img :src="bimasaktiLogo" alt="Bimasakti Logo" class="h-6 w-auto grayscale opacity-50">
          <span class="text-slate-400 text-xs font-black tracking-wider uppercase">BIMASAKTI PROPERTY SYSTEMS</span>
        </div>
        <p class="text-[10px] font-bold uppercase tracking-widest">&copy; 2026 PT Realta Chakradarma. All rights reserved.</p>
      </div>
    </footer>

  </div>
</template>

<style scoped>
/* Scroll Triggered Animations CSS */
.scroll-animate {
  opacity: 0;
  transition: all 0.8s cubic-bezier(0.16, 1, 0.3, 1);
  will-change: transform, opacity;
}

.scroll-animate.slide-up {
  transform: translateY(40px);
}

.scroll-animate.fade-in {
  transform: scale(0.98);
}

.scroll-animate.in-view {
  opacity: 1;
  transform: translateY(0) scale(1);
}
</style>
