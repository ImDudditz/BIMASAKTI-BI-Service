<script setup>
import ReportLayout from '@/components/ReportLayout.vue'
</script>

<template>
  <ReportLayout title="Service Reports" subtitle="Maintenance Logs & Ticket Histories">
    <div class="flex-1 flex flex-col items-center justify-center p-8 max-w-screen-2xl mx-auto w-full h-full min-h-0">
      <div class="max-w-md w-full bg-white/70 backdrop-blur-md border border-white/80 rounded-[32px] p-8 text-center shadow-xl space-y-6">
        <div class="w-20 h-20 bg-rose-50 border border-rose-100 rounded-full flex items-center justify-center mx-auto text-4xl text-rose-600 shadow-inner">
          🔧
        </div>
        <div class="space-y-2">
          <h3 class="text-xl font-black text-slate-900 tracking-tight">Services Reporting</h3>
          <p class="text-sm text-slate-500 font-bold leading-relaxed">
            This module is currently under construction. Historical service orders, maintenance response times, and vendor metrics reports will be displayed here soon.
          </p>
        </div>
        <div class="pt-2">
          <RouterLink to="/dashboard/maintenance" class="inline-flex items-center justify-center px-6 py-2.5 bg-rose-600 hover:bg-rose-700 text-white text-xs font-black rounded-2xl shadow-md transition-all active:scale-95">
            Go to Maintenance Dashboard
          </RouterLink>
        </div>
      </div>
    </div>
  </ReportLayout>
</template>
