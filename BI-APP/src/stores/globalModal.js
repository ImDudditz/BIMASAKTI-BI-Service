// frontend/src/stores/globalModal.js
import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useGlobalModalStore = defineStore('globalModal', () => {
  const isOpen = ref(false)
  const type = ref('alert') // 'alert', 'confirm', 'prompt'
  const title = ref('')
  const message = ref('')
  const inputValue = ref('')
  const confirmText = ref('OK')
  const cancelText = ref('Cancel')
  const confirmColor = ref('bg-sky-600 hover:bg-sky-700')
  const icon = ref('ℹ️')
  const onConfirmCallback = ref(null)

  const closeModal = () => {
    isOpen.value = false
  }

  const confirmModal = () => {
    if (onConfirmCallback.value) {
      if (type.value === 'prompt') {
        onConfirmCallback.value(inputValue.value)
      } else {
        onConfirmCallback.value()
      }
    }
    closeModal()
  }

  const showAlert = (newTitle, newMessage, isError = false) => {
    isOpen.value = true
    type.value = 'alert'
    title.value = newTitle
    message.value = newMessage
    inputValue.value = ''
    confirmText.value = 'Got it'
    cancelText.value = ''
    confirmColor.value = isError
      ? 'bg-rose-600 hover:bg-rose-700'
      : 'bg-emerald-600 hover:bg-emerald-700'
    icon.value = isError ? '❌' : '✅'
    onConfirmCallback.value = null
  }

  const showConfirm = (newTitle, newMessage, newIcon, isDestructive, onConfirm) => {
    isOpen.value = true
    type.value = 'confirm'
    title.value = newTitle
    message.value = newMessage
    inputValue.value = ''
    confirmText.value = isDestructive ? 'Yes, Delete' : 'Confirm'
    cancelText.value = 'Cancel'
    confirmColor.value = isDestructive
      ? 'bg-rose-600 hover:bg-rose-700'
      : 'bg-sky-600 hover:bg-sky-700'
    icon.value = newIcon
    onConfirmCallback.value = onConfirm
  }

  const showPrompt = (newTitle, newMessage, defaultValue, onConfirm) => {
    isOpen.value = true
    type.value = 'prompt'
    title.value = newTitle
    message.value = newMessage
    inputValue.value = defaultValue
    confirmText.value = 'Save'
    cancelText.value = 'Cancel'
    confirmColor.value = 'bg-sky-600 hover:bg-sky-700'
    icon.value = '✏️'
    onConfirmCallback.value = onConfirm
  }

  return {
    isOpen,
    type,
    title,
    message,
    inputValue,
    confirmText,
    cancelText,
    confirmColor,
    icon,
    closeModal,
    confirmModal,
    showAlert,
    showConfirm,
    showPrompt,
  }
})
