<script setup lang="ts">
import { Eye, EyeOff } from '@lucide/vue'
import { ref, useTemplateRef } from 'vue'

import Button from './Button.vue'
import Input from './Input.vue'

// Password input with the show/hide toggle (ui-design "Form conventions"). Other attributes (autocomplete, onBlur) reach the input.
defineProps<{ label: string; error?: string; hint?: string; showLabel: string; hideLabel: string }>()
const model = defineModel<string>({ default: '' })

const shown = ref(false)
const input = useTemplateRef<{ focus: () => void }>('input')
defineExpose({ focus: () => input.value?.focus() })
</script>

<template>
  <Input ref="input" v-model="model" :type="shown ? 'text' : 'password'" :label="label" :error="error" :hint="hint">
    <template #append>
      <Button
        variant="ghost"
        size="sm"
        icon-only
        :aria-label="shown ? hideLabel : showLabel"
        :aria-pressed="shown"
        :start-icon="shown ? EyeOff : Eye"
        @click="shown = !shown"
      />
    </template>
  </Input>
</template>
