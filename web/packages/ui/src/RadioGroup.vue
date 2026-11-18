<script setup lang="ts">
import { useId } from 'vue'
import Radio from './Radio.vue'

export interface RadioOption {
  label: string
  value: string | number | boolean
  disabled?: boolean
}

withDefaults(
  defineProps<{
    modelValue: string | number | boolean | null
    options: RadioOption[]
    label: string
    direction?: 'vertical' | 'horizontal'
  }>(),
  { direction: 'vertical' },
)
defineEmits<{ 'update:modelValue': [value: string | number | boolean] }>()

const name = `radio-group-${useId()}`
</script>

<template>
  <div role="radiogroup" :aria-label="label" :class="['flex', direction === 'vertical' ? 'flex-col' : 'flex-row flex-wrap gap-x-6']">
    <Radio
      v-for="option in options"
      :key="String(option.value)"
      :name="name"
      :value="option.value"
      :model-value="modelValue"
      :disabled="option.disabled"
      @update:model-value="$emit('update:modelValue', $event)"
    >
      <slot name="option" :option="option">{{ option.label }}</slot>
    </Radio>
  </div>
</template>
