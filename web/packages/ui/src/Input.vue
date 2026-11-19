<script setup lang="ts">
import { computed, useId, useTemplateRef } from 'vue'

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
    type?: string
    placeholder?: string
  }>(),
  { modelValue: '', label: undefined, error: undefined, hint: undefined, required: false, disabled: false, type: 'text', placeholder: undefined },
)
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const id = useId()
const errorId = computed(() => `${id}-error`)
const hintId = computed(() => `${id}-hint`)
const input = useTemplateRef<HTMLInputElement>('input')

defineExpose({ focus: () => input.value?.focus() })
</script>

<template>
  <div>
    <label v-if="label" :for="id" class="mb-1.5 block text-sm font-medium text-text-secondary">
      {{ label }}<span v-if="required" class="text-feedback-error-fg" aria-hidden="true"> *</span>
    </label>
    <div class="relative">
      <input
        :id="id"
        ref="input"
        v-bind="$attrs"
        :type="type"
        :value="modelValue"
        :placeholder="placeholder"
        :disabled="disabled"
        :required="required"
        :aria-invalid="error ? true : undefined"
        :aria-describedby="error ? errorId : hint ? hintId : undefined"
        :class="[
          'min-h-11 w-full rounded-lg border bg-bg-surface px-4 text-base text-text-primary sm:text-sm placeholder:text-text-muted focus-visible:shadow-focus focus-visible:outline-2 focus-visible:outline-focus-ring disabled:cursor-not-allowed disabled:opacity-50',
          error ? 'border-feedback-error-icon' : 'border-border-control',
          $slots.append ? 'pr-12' : '',
        ]"
        @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      />
      <div v-if="$slots.append" class="absolute inset-y-0 right-1 flex items-center"><slot name="append" /></div>
    </div>
    <p v-if="error" :id="errorId" role="alert" class="mt-1 text-xs text-feedback-error-fg">{{ error }}</p>
    <p v-else-if="hint" :id="hintId" class="mt-1 text-xs text-text-muted">{{ hint }}</p>
  </div>
</template>
