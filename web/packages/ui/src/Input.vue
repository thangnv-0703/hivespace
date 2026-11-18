<script setup lang="ts">
import { computed, useId } from 'vue'

defineOptions({ inheritAttrs: false })

withDefaults(
  defineProps<{
    modelValue?: string
    label?: string
    error?: string
    required?: boolean
    disabled?: boolean
    type?: string
    placeholder?: string
  }>(),
  { modelValue: '', label: undefined, error: undefined, required: false, disabled: false, type: 'text', placeholder: undefined },
)
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const id = useId()
const errorId = computed(() => `${id}-error`)
</script>

<template>
  <div>
    <label v-if="label" :for="id" class="mb-1.5 block text-sm font-medium text-text-secondary">
      {{ label }}<span v-if="required" class="text-feedback-error-fg" aria-hidden="true"> *</span>
    </label>
    <input
      :id="id"
      v-bind="$attrs"
      :type="type"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :required="required"
      :aria-invalid="error ? true : undefined"
      :aria-describedby="error ? errorId : undefined"
      :class="[
        'min-h-11 w-full rounded-lg border bg-bg-surface px-4 text-sm text-text-primary placeholder:text-text-muted focus-visible:shadow-focus focus-visible:outline-2 focus-visible:outline-focus-ring disabled:cursor-not-allowed disabled:opacity-50',
        error ? 'border-feedback-error-icon' : 'border-border-control',
      ]"
      @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
    />
    <p v-if="error" :id="errorId" class="mt-1 text-xs text-feedback-error-fg">{{ error }}</p>
  </div>
</template>
