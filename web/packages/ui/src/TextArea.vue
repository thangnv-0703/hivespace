<script setup lang="ts">
import { computed, useId } from 'vue'

defineOptions({ inheritAttrs: false })

withDefaults(
  defineProps<{
    modelValue?: string
    label?: string
    error?: string
    /** Help text under the field; replaced by `error` while there is one (design.md §4.2). */
    hint?: string
    required?: boolean
    disabled?: boolean
    placeholder?: string
    rows?: number
  }>(),
  { modelValue: '', label: undefined, error: undefined, hint: undefined, required: false, disabled: false, placeholder: undefined, rows: 4 },
)
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const id = useId()
const errorId = computed(() => `${id}-error`)
const hintId = computed(() => `${id}-hint`)
</script>

<template>
  <div>
    <label v-if="label" :for="id" class="mb-1.5 block text-sm font-medium text-text-secondary">
      {{ label }}<span v-if="required" class="text-feedback-error-fg" aria-hidden="true"> *</span>
    </label>
    <textarea
      :id="id"
      v-bind="$attrs"
      :value="modelValue"
      :rows="rows"
      :placeholder="placeholder"
      :disabled="disabled"
      :required="required"
      :aria-invalid="error ? true : undefined"
      :aria-describedby="error ? errorId : hint ? hintId : undefined"
      :class="[
        'min-h-24 w-full rounded-lg border bg-bg-surface px-3.5 py-2.5 text-sm text-text-primary shadow-xs placeholder:text-text-muted focus-visible:shadow-focus focus-visible:outline-2 focus-visible:outline-focus-ring disabled:cursor-not-allowed disabled:bg-bg-subtle disabled:text-text-disabled',
        error ? 'border-feedback-error-icon' : 'border-border-control',
      ]"
      @input="emit('update:modelValue', ($event.target as HTMLTextAreaElement).value)"
    />
    <p v-if="error" :id="errorId" class="mt-1 text-xs text-feedback-error-fg">{{ error }}</p>
    <p v-else-if="hint" :id="hintId" class="mt-1 text-xs text-text-muted">{{ hint }}</p>
  </div>
</template>
