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

  // Load Lora Google Font dynamically
  if (!document.getElementById('google-font-lora')) {
    const link = document.createElement('link')
    link.id = 'google-font-lora'
    link.rel = 'stylesheet'
    link.href = 'https://fonts.googleapis.com/css2?family=Lora:ital,wght@0,400..700;1,400..700&display=swap'
    document.head.appendChild(link)
  }

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
  <div class="h-full w-full bg-[#fcfcfb] text-slate-800 font-sans overflow-y-auto overflow-x-hidden scroll-smooth relative custom-scroll">
    
    <!-- --- LANDING HEADER --- -->
    <header class="w-full max-w-7xl mx-auto px-6 py-6 flex items-center justify-between relative z-20 border-b border-slate-200/80 bg-white/40 backdrop-blur-sm">
      <!-- Logo removed per request -->
      <div></div>

      <!-- Nav Items -->
      <nav class="hidden md:flex items-center gap-8">
        <button @click="scrollToSection('about')" class="text-xs font-bold uppercase tracking-wider text-slate-600 hover:text-slate-900 transition-colors">System Overview</button>
        <button @click="scrollToSection('features')" class="text-xs font-bold uppercase tracking-wider text-slate-600 hover:text-slate-900 transition-colors">Key Modules</button>
        <button @click="scrollToSection('azure')" class="text-xs font-bold uppercase tracking-wider text-slate-600 hover:text-slate-900 transition-colors">Cloud Infrastructure</button>
      </nav>

      <button 
        @click="handleActionClick" 
        class="bg-slate-900 hover:bg-slate-800 text-white font-bold text-[10px] uppercase tracking-widest px-5 py-3 rounded-none shadow-sm hover:shadow transition-all active:scale-[0.98] relative z-10 border border-slate-900"
      >
        {{ authStore.isAuthenticated ? 'Access Dashboard' : 'Sign In to Portal' }}
      </button>
    </header>

    <!-- --- HERO SECTION --- -->
    <section class="max-w-5xl mx-auto px-6 pt-24 pb-20 text-center relative z-10 flex flex-col items-center">
      <!-- Centered Hero Logo matching Title Scale -->
      <img :src="bimasaktiLogo" alt="Bimasakti Logo" class="h-20 sm:h-24 md:h-28 w-auto object-contain mb-8 opacity-95">

      <h1 class="text-4xl sm:text-5xl md:text-6xl font-serif font-black text-slate-900 tracking-tight leading-tight max-w-4xl mb-6">
        Business Dashboard Service
      </h1>

      <p class="text-base sm:text-lg text-slate-500 font-medium max-w-2xl leading-relaxed mb-10 font-sans">
        Engineered by PT Realta Chakradarma, Bimasakti unifies operational pipelines, billing tax processes, and tenant mobile requests into a robust, Azure-backed business intelligence center.
      </p>

      <div class="flex flex-col sm:flex-row gap-4 mb-16">
        <button 
          @click="handleActionClick" 
          class="bg-slate-900 hover:bg-slate-800 text-white font-bold text-xs uppercase tracking-widest px-8 py-4 rounded-none shadow-md hover:shadow-lg active:scale-[0.98] transition-all flex items-center justify-center border border-slate-900"
        >
          {{ authStore.isAuthenticated ? 'Access Dashboard' : 'Sign In to Portal' }}
        </button>
        <button 
          @click="scrollToSection('about')" 
          class="bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 font-bold text-xs uppercase tracking-widest px-8 py-4 rounded-none active:scale-[0.98] transition-all"
        >
          System Overview
        </button>
      </div>

      <!-- --- HERO IMAGE PREVIEW --- -->
      <div class="w-full max-w-4xl scroll-animate slide-up bg-white p-2 border border-slate-200 shadow-md relative">
        <div class="border border-slate-100 overflow-hidden max-h-[480px]">
          <img :src="biDashboardPreview" alt="Bimasakti BI Dashboard Preview" class="w-full h-full object-cover">
        </div>
      </div>
    </section>

    <!-- --- ABOUT / INTRODUCTION SECTION --- -->
    <section id="about" class="bg-[#fcfcf9] border-y border-slate-200/80 py-24 relative z-10">
      <div class="max-w-6xl mx-auto px-6">
        
        <div class="flex flex-col md:flex-row items-center gap-12 sm:gap-16">
          <div class="flex-1 scroll-animate slide-up">
            <span class="text-xs font-bold text-slate-400 uppercase tracking-widest block mb-3">Enterprise Suite</span>
            <h2 class="text-3xl sm:text-4xl font-serif font-bold text-slate-900 tracking-tight leading-tight mb-6">
              Complete Operations Control for Property Leaders
            </h2>
            <p class="text-sm text-slate-500 font-medium leading-relaxed mb-8">
              Bimasakti is a comprehensive property management system designed to coordinate all Operational, Financial, and Tenant processes under a single database structure.
            </p>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-6">
              <div class="bg-white p-6 border border-slate-200/80 rounded-none shadow-sm hover:shadow transition-shadow">
                <h4 class="text-xs font-bold text-slate-900 uppercase tracking-wider mb-2 flex items-center gap-2">
                  <span class="text-slate-400">01 /</span> Commercial Malls
                </h4>
                <p class="text-[11px] text-slate-500 leading-relaxed mt-2 font-medium">Track rental units, occupancy analytics, sales campaign, and shopping center operational workflows.</p>
              </div>
              <div class="bg-white p-6 border border-slate-200/80 rounded-none shadow-sm hover:shadow transition-shadow">
                <h4 class="text-xs font-bold text-slate-900 uppercase tracking-wider mb-2 flex items-center gap-2">
                  <span class="text-slate-400">02 /</span> Residential Assets
                </h4>
                <p class="text-[11px] text-slate-500 leading-relaxed mt-2 font-medium">Streamline apartment tenant handovers, utility billings, maintenance requests, and facilities scheduling.</p>
              </div>
            </div>
          </div>

          <div class="flex-1 scroll-animate slide-up bg-white p-2 border border-slate-200 shadow-md" style="transition-delay: 0.15s;">
            <div class="border border-slate-100 overflow-hidden">
              <img :src="propertyOperations" alt="Realta Bimasakti Property Operations" class="w-full h-full object-cover">
            </div>
          </div>
        </div>

      </div>
    </section>

    <!-- --- FEATURE SEGMENTS (THREE CORE PILLARS) --- -->
    <section id="features" class="py-24 relative z-10 max-w-6xl mx-auto px-6">
      
      <div class="text-center max-w-2xl mx-auto mb-20 scroll-animate fade-in">
        <span class="text-xs font-bold text-slate-400 uppercase tracking-widest block mb-3">Modular Architecture</span>
        <h3 class="text-3xl font-serif font-bold text-slate-900 tracking-tight leading-tight">
          Coordinated operational streams working as a BI Service
        </h3>
        <p class="text-sm text-slate-500 font-medium leading-relaxed mt-4">
          Unify accounting ledgers, engineering schedules, and tenant requests. Check how our system coordinates these operational nodes natively:
        </p>
      </div>

      <!-- Segment 1: Financial & Billing Accounting -->
      <div class="flex flex-col md:flex-row items-center gap-12 sm:gap-16 mb-24">
        <div class="flex-1 scroll-animate slide-up">
          <span class="text-xs font-bold text-slate-400 uppercase tracking-widest block mb-2">Module 01</span>
          <h4 class="text-2xl font-serif font-bold text-slate-900 mb-4">Billing, Accounting, & VAT Automated Processing</h4>
          <p class="text-sm text-slate-500 leading-relaxed font-semibold mb-6">
            Bimasakti BI unifies operational property streams with financial control tools. Automatically generate invoices, reconcile accounting ledger statements, and streamline tax reporting with direct link integrations to e-Faktur PPN.
          </p>
          <ul class="text-xs font-bold text-slate-500 flex flex-col gap-3">
            <li class="flex items-center gap-2"><span class="text-slate-400 font-bold">&#8212;</span> Automatic billing calculation and secure invoicing</li>
            <li class="flex items-center gap-2"><span class="text-slate-400 font-bold">&#8212;</span> General Ledger, Trial Balance, & balance sheets compiler</li>
            <li class="flex items-center gap-2"><span class="text-slate-400 font-bold">&#8212;</span> Integrated e-Faktur tax processing</li>
          </ul>
        </div>
        <div class="flex-1 scroll-animate slide-up bg-white p-2 border border-slate-200 shadow-md" style="transition-delay: 0.15s;">
          <div class="border border-slate-100 overflow-hidden">
            <img :src="biDashboardPreview" alt="Executive Billing & Accounting BI" class="w-full h-full object-cover">
          </div>
        </div>
      </div>

      <!-- Segment 2: Tenant Mobile Solutions (24/7 Access) -->
      <div class="flex flex-col-reverse md:flex-row items-center gap-12 sm:gap-16">
        <div class="flex-1 scroll-animate slide-up bg-white p-2 border border-slate-200 shadow-md">
          <div class="border border-slate-100 overflow-hidden">
            <img :src="tenantMobileApp" alt="Bimasakti Tenant Mobile Portal App" class="w-full h-full object-cover">
          </div>
        </div>
        <div class="flex-1 scroll-animate slide-up" style="transition-delay: 0.15s;">
          <span class="text-xs font-bold text-slate-400 uppercase tracking-widest block mb-2">Module 02</span>
          <h4 class="text-2xl font-serif font-bold text-slate-900 mb-4">24/7 Tenant Mobile Portal & Maintenance System</h4>
          <p class="text-sm text-slate-500 leading-relaxed font-semibold mb-6">
            Drastically improve tenant relationships by providing 24/7 service transparency. Through the dedicated mobile application, tenants can verify active billing statements, submit maintenance tickets, and track engineering repair statuses in real-time.
          </p>
          <ul class="text-xs font-bold text-slate-500 flex flex-col gap-3">
            <li class="flex items-center gap-2"><span class="text-slate-400 font-bold">&#8212;</span> Mobile billing transparency and payment gateway</li>
            <li class="flex items-center gap-2"><span class="text-slate-400 font-bold">&#8212;</span> 24/7 Service Desk & engineering ticket submission</li>
            <li class="flex items-center gap-2"><span class="text-slate-400 font-bold">&#8212;</span> Uptime status monitoring for facilities</li>
          </ul>
        </div>
      </div>

    </section>

    <!-- --- CLOUD DEPLOYMENT & AZURE INFRASTRUCTURE --- -->
    <section id="azure" class="bg-slate-900 text-white py-24 relative z-10 border-y border-slate-800">
      <div class="max-w-6xl mx-auto px-6 flex flex-col md:flex-row items-center gap-12 sm:gap-16">
        <div class="flex-1 scroll-animate slide-up">
          <span class="text-xs font-bold text-slate-400 uppercase tracking-widest block mb-3">Enterprise Cloud</span>
          <h3 class="text-3xl font-serif font-bold text-slate-100 tracking-tight leading-tight mb-6">
            Cloud-Based Deployment with Microsoft Azure Security
          </h3>
          <p class="text-sm text-slate-400 font-semibold leading-relaxed mb-8">
            Bimasakti as a Service leverages **Microsoft Azure**'s advanced cloud computing architecture. This cloud collaboration provides maximum data security, strict confidentiality protection, 99.9% system uptime, and minimal downtime.
          </p>
          <div class="flex flex-col gap-4">
            <div class="flex gap-4">
              <span class="text-sm font-bold bg-slate-800 text-blue-400 p-2 border border-slate-700 shrink-0 h-10 w-10 flex items-center justify-center font-mono">01</span>
              <div>
                <h4 class="text-sm font-bold text-slate-100">Data Integrity Protection</h4>
                <p class="text-xs text-slate-400 leading-relaxed font-semibold mt-1">Advanced SQLite encryption modules and TLS connection handshakes protect tenant transaction datasets.</p>
              </div>
            </div>
            <div class="flex gap-4">
              <span class="text-sm font-bold bg-slate-800 text-blue-400 p-2 border border-slate-700 shrink-0 h-10 w-10 flex items-center justify-center font-mono">02</span>
              <div>
                <h4 class="text-sm font-bold text-slate-100">Azure Backup Recovery</h4>
                <p class="text-xs text-slate-400 leading-relaxed font-semibold mt-1">Automatic backups ensure that historical ledger reports can be recovered instantly in the event of an operational failure.</p>
              </div>
            </div>
          </div>
        </div>

        <div class="flex-1 w-full scroll-animate slide-up bg-slate-950 p-6 shadow-xl border border-slate-800" style="transition-delay: 0.15s;">
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
    <section class="bg-[#f7f7f5] border-y border-slate-200/80 text-slate-800 py-24 relative z-10">
      <div class="max-w-4xl mx-auto px-6 text-center scroll-animate fade-in">
        <span class="text-5xl font-serif text-slate-300 block mb-2">“</span>
        <blockquote class="text-xl sm:text-2xl font-serif font-medium leading-relaxed mb-8 text-slate-900 italic">
          Bimasakti Property Management System unifies operational billing records, tenant requests, and accounting ledgers into a robust, Azure-backed dashboard. Realta's software has accelerated our financial oversight.
        </blockquote>
        <div class="w-12 h-0.5 bg-slate-300 mx-auto mb-4"></div>
        <cite class="text-xs font-bold uppercase tracking-widest text-slate-500">PT Realta Chakradarma &bull; Strategic Enterprise Solutions</cite>
      </div>
    </section>

    <!-- --- CALL TO ACTION --- -->
    <section class="max-w-6xl mx-auto px-6 py-24 text-center scroll-animate slide-up relative z-10 flex flex-col items-center">
      <h3 class="text-3xl sm:text-4xl font-serif font-bold text-slate-900 tracking-tight leading-none mb-6">
        Ready to orchestrate your properties?
      </h3>
      <p class="text-sm text-slate-500 font-medium max-w-md leading-relaxed mb-8">
        Access the secure portal and begin managing dashboards, billing accounting parameters, and synchronizations.
      </p>
      <button 
        @click="handleActionClick" 
        class="bg-slate-900 hover:bg-slate-800 text-white font-bold text-xs uppercase tracking-widest px-8 py-4 rounded-none shadow-md hover:shadow-lg active:scale-[0.98] transition-all border border-slate-900"
      >
        {{ authStore.isAuthenticated ? 'Access Dashboard' : 'Sign In to Portal' }}
      </button>
    </section>

    <!-- --- FOOTER --- -->
    <footer class="w-full bg-[#0b0f19] border-t border-slate-900 text-slate-500 py-12 relative z-10">
      <div class="max-w-7xl mx-auto px-6 flex flex-col sm:flex-row justify-between items-center gap-6">
        <div class="flex items-center gap-3">
          <img :src="bimasaktiLogo" alt="Bimasakti Logo" class="h-6 w-auto grayscale opacity-50">
          <span class="text-slate-400 text-xs font-bold tracking-wider uppercase">BIMASAKTI PROPERTY SYSTEMS</span>
        </div>
        <p class="text-[10px] font-bold uppercase tracking-widest">&copy; 2026 PT Realta Chakradarma. All rights reserved.</p>
      </div>
    </footer>

  </div>
</template>

<style scoped>
.font-serif {
  font-family: 'Lora', Georgia, Cambria, "Times New Roman", Times, serif;
}

/* Scroll Triggered Animations CSS */
.scroll-animate {
  opacity: 0;
  transition: all 0.8s cubic-bezier(0.16, 1, 0.3, 1);
  will-change: transform, opacity;
}

.scroll-animate.slide-up {
  transform: translateY(30px);
}

.scroll-animate.fade-in {
  transform: scale(0.99);
}

.scroll-animate.in-view {
  opacity: 1;
  transform: translateY(0) scale(1);
}
</style>
