<script setup>
import { ref, watch, defineProps, defineEmits } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'

const props = defineProps({
  show: { type: Boolean, default: false },
})

const emit = defineEmits(['close', 'saved'])

const authStore = useAuthStore()

const masterWidgets = ref([])
const localWidgets = ref([])
const isSaving = ref(false)

const loadWidgets = async () => {
  try {
    const company_id = authStore.user?.company_id
    const username = authStore.user?.username
    if (!company_id || !username) return

    // Fetch master widgets dynamically from endpoint
    const masterRes = await api.get('/dynamic-widgets/available', { params: { username } })
    masterWidgets.value = (masterRes.data || []).map((mw, index) => ({
      key: mw.id,
      title: mw.name,
      order: index
    }))

    const res = await api.get('/dashboard/my-widgets', { params: { company_id, username } })
    const userWidgets = res.data || []

    // Merge fetched with master
    localWidgets.value = masterWidgets.value
      .map((mw) => {
        const existing = userWidgets.find((uw) => uw.widget_key === mw.key)
        if (existing) {
          return { ...existing, title: mw.title }
        }
        return {
          widget_key: mw.key,
          title: mw.title,
          is_active: false,
          layout_order: mw.order,
          config: null,
        }
      })
      .sort((a, b) => a.layout_order - b.layout_order)
  } catch (error) {
    console.error('Failed to load widget config', error)
  }
}

watch(
  () => props.show,
  (newVal) => {
    if (newVal) {
      loadWidgets()
    }
  },
)

const saveLayout = async () => {
  isSaving.value = true
  try {
    const company_id = authStore.user?.company_id
    const username = authStore.user?.username

    // Prepare payload
    const payload = localWidgets.value.map((w, index) => ({
      widget_key: w.widget_key,
      is_active: w.is_active,
      layout_order: index, // Preserve array order
      config: w.config,
    }))

    await api.post('/dashboard/my-widgets', payload, { params: { company_id, username } })

    emit('saved')
    emit('close')
  } catch (error) {
    console.error('Failed to save widgets', error)
    alert('Failed to save layout.')
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <div
    v-if="show"
    class="fixed inset-0 z-[200] flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm transition-opacity"
  >
    <div
      class="bg-white rounded-xl shadow-2xl w-full max-w-md overflow-hidden flex flex-col border border-slate-200"
    >
      <div
        class="px-6 py-4 border-b border-slate-100 bg-slate-50 flex items-center justify-between"
      >
        <h3 class="font-bold text-slate-800 text-lg">Customize Dashboard</h3>
        <button
          @click="emit('close')"
          class="text-slate-400 hover:text-slate-600 transition-colors"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            class="h-6 w-6"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M6 18L18 6M6 6l12 12"
            />
          </svg>
        </button>
      </div>

      <div class="p-6 flex flex-col gap-4 overflow-y-auto max-h-[60vh] custom-scrollbar">
        <p class="text-sm text-slate-500 mb-2">
          Select which widgets you want to display on your executive dashboard.
        </p>

        <div
          v-for="widget in localWidgets"
          :key="widget.widget_key"
          class="flex items-center justify-between p-4 rounded-lg border border-slate-200 hover:border-sky-300 hover:bg-sky-50/50 transition-colors bg-white"
        >
          <span class="font-medium text-slate-700 text-[14px]">{{ widget.title }}</span>

          <label class="relative inline-flex items-center cursor-pointer">
            <input type="checkbox" v-model="widget.is_active" class="sr-only peer" />
            <div
              class="w-11 h-6 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-emerald-500"
            ></div>
          </label>
        </div>
      </div>

      <div class="px-6 py-4 border-t border-slate-100 bg-slate-50 flex justify-end gap-3">
        <button
          @click="emit('close')"
          class="px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-200 bg-slate-100 rounded transition-colors"
        >
          Cancel
        </button>
        <button
          @click="saveLayout"
          :disabled="isSaving"
          class="px-5 py-2 text-sm font-medium text-white bg-sky-600 hover:bg-sky-700 rounded shadow-sm disabled:opacity-70 flex items-center transition-colors"
        >
          <span v-if="isSaving" class="mr-2">
            <svg
              class="animate-spin h-4 w-4 text-white"
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
          </span>
          Save Layout
        </button>
      </div>
    </div>
  </div>
</template>
