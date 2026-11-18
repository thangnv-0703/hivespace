<script setup lang="ts">
import { ChevronDown } from '@lucide/vue'
import { computed, useId } from 'vue'

export interface SelectOption {
  label: string
  value: string
}

defineOptions({ inheritAttrs: false })

withDefaults(
  defineProps<{
    modelValue?: string
    options: SelectOption[]
    label?: string
    error?: string
    disabled?: boolean
    placeholder?: string
  }>(),
  { modelValue: '', label: undefined, error: undefined, disabled: false, placeholder: undefined },
)
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const id = useId()
const errorId = computed(() => `${id}-error`)
</script>

<template>
  <div>
    <label v-if="label" :for="id" class="mb-1.5 block text-sm font-medium text-text-secondary">{{ label }}</label>
    <div class="relative">
      <select
        :id="id"
        v-bind="$attrs"
        :value="modelValue"
        :disabled="disabled"
        :aria-invalid="error ? true : undefined"
        :aria-describedby="error ? errorId : undefined"
        :class="[
          'min-h-11 w-full appearance-none rounded-lg border bg-bg-surface pl-4 pr-10 text-sm text-text-primary focus-visible:shadow-focus focus-visible:outline-2 focus-visible:outline-focus-ring disabled:cursor-not-allowed disabled:opacity-50',
          error ? 'border-feedback-error-icon' : 'border-border-control',
        ]"
        @change="emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
      >
        <option v-if="placeholder" value="" disabled>{{ placeholder }}</option>
        <option v-for="option in options" :key="option.value" :value="option.value">{{ option.label }}</option>
      </select>
      <ChevronDown class="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-text-muted" aria-hidden="true" />
    </div>
    <p v-if="error" :id="errorId" class="mt-1 text-xs text-feedback-error-fg">{{ error }}</p>
  </div>
</template>
