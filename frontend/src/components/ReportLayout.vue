<script setup>
defineProps({
  title: { type: String, required: true },
  subtitle: { type: String, default: '' }
})
</script>

<template>
  <div 
    class="absolute inset-0 flex flex-col w-full bg-linear-to-br from-sky-100 via-sky-50 to-white font-sans antialiased text-slate-800 overflow-hidden animate-fade-in"
  >
    
    <header class="shrink-0 bg-white/90 backdrop-blur-md border-b border-slate-200/60 shadow-sm z-[100] w-full relative">
      <!-- THE FIX: Switched from rigid grid to flex layout to allow the horizontal controls to breathe -->
      <div class="max-w-screen-2xl mx-auto px-4 sm:px-6 lg:px-8 py-2 flex items-center justify-between gap-4">
        
        <div class="flex items-center justify-start flex-1 min-w-0">
          <slot name="controls"></slot>
        </div>

        <!-- Tightened Header Heights to grant more space to reports -->
        <div class="flex flex-col justify-center text-center shrink-0">
          <h2 class="text-lg font-black text-sky-950 tracking-tight whitespace-nowrap">{{ title }}</h2>
          <p v-if="subtitle" class="text-[11px] font-bold text-slate-500 uppercase tracking-widest mt-0.5">{{ subtitle }}</p>
        </div>
        
        <div class="flex items-center justify-end flex-1 min-w-0">
          <slot name="actions"></slot>
        </div>
      </div>
    </header>

    <main class="grow flex flex-col min-h-0 w-full relative z-10">
      <slot></slot>
    </main>
  </div>
</template>

<style>

.custom-scrollbar::-webkit-scrollbar { width: 10px; height: 10px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background-color: #cbd5e1; border-radius: 12px; }
.custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: #94a3b8; }

.animate-fade-in {
  animation: fadeIn 0.4s ease-out;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(5px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>