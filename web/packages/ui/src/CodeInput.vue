<script setup lang="ts">
import { computed, nextTick, ref, useId } from 'vue'

// A one-time code split into boxes (FEAT-006 ui-design, escalation E-1: provisional until design.md §4.2 adopts it).
// One value through v-model; digits only; typing advances; Backspace on an empty box goes back; paste fills from the first box.
const props = withDefaults(
  defineProps<{
    modelValue?: string
    length?: number
    legend: string
    /** The accessible name of box n; `{index}` is replaced by its 1-based position. */
    digitLabel: string
    error?: string
    hint?: string
    disabled?: boolean
  }>(),
  { modelValue: '', length: 6, error: undefined, hint: undefined, disabled: false },
)
const emit = defineEmits<{ 'update:modelValue': [value: string]; complete: [value: string] }>()

const id = useId()
const errorId = `${id}-error`
const hintId = `${id}-hint`
const boxes = ref<HTMLInputElement[]>([])
const digits = computed(() => Array.from({ length: props.length }, (_, i) => props.modelValue[i] ?? ''))

function focusBox(index: number) {
  const target = boxes.value[Math.max(0, Math.min(index, props.length - 1))]
  target?.focus()
  target?.select()
}

function update(next: string[]) {
  const value = next.join('').slice(0, props.length)
  emit('update:modelValue', value)
  if (value.length === props.length && /^[0-9]+$/.test(value)) emit('complete', value)
}

/** Writes digits starting at `index`, as typing or pasting does. Returns the next box to focus. */
function write(index: number, text: string): number {
  const incoming = text.replace(/[^0-9]/g, '').split('')
  if (incoming.length === 0) return index
  const next = [...digits.value]
  let position = index
  for (const digit of incoming) {
    if (position >= props.length) break
    next[position] = digit
    position++
  }
  // The value is the joined string, so digits typed after an empty box move up to fill it.
  update(next)
  return Math.min(position, next.filter(Boolean).length)
}

async function onInput(index: number, event: Event) {
  const input = event.target as HTMLInputElement
  const value = input.value
  input.value = digits.value[index] ?? ''
  const nextIndex = write(index, value)
  await nextTick()
  if (nextIndex !== index) focusBox(nextIndex)
}

async function onPaste(event: ClipboardEvent) {
  event.preventDefault()
  const nextIndex = write(0, event.clipboardData?.getData('text') ?? '')
  await nextTick()
  focusBox(nextIndex)
}

function onKeydown(index: number, event: KeyboardEvent) {
  if (event.key === 'Backspace') {
    event.preventDefault()
    const next = [...digits.value]
    if (next[index]) {
      next[index] = ''
      update(next)
    } else if (index > 0) {
      next[index - 1] = ''
      update(next)
      focusBox(index - 1)
    }
  } else if (event.key === 'ArrowLeft') {
    event.preventDefault()
    focusBox(index - 1)
  } else if (event.key === 'ArrowRight') {
    event.preventDefault()
    focusBox(index + 1)
  }
}

defineExpose({ focus: () => focusBox(0) })
</script>

<template>
  <fieldset :aria-describedby="error ? errorId : hint ? hintId : undefined" :aria-invalid="error ? true : undefined">
    <legend class="mb-1.5 block text-sm font-medium text-text-secondary">{{ legend }}</legend>
    <div class="flex justify-center gap-2 sm:gap-3">
      <input
        v-for="(digit, index) in digits"
        :key="index"
        ref="boxes"
        :value="digit"
        type="text"
        inputmode="numeric"
        pattern="[0-9]*"
        maxlength="1"
        :autocomplete="index === 0 ? 'one-time-code' : 'off'"
        :aria-label="digitLabel.replace('{index}', String(index + 1))"
        :disabled="disabled"
        :data-testid="`code-digit-${index}`"
        :class="[
          'h-13 w-11 rounded-lg border bg-bg-surface text-center text-2xl font-semibold tabular-nums text-text-primary shadow-xs sm:h-15 sm:w-13',
          'focus:border-action-primary-bg focus:shadow-focus focus:outline-none focus:ring-2 focus:ring-focus-ring/30',
          'disabled:cursor-not-allowed disabled:bg-bg-subtle disabled:text-text-disabled',
          error ? 'border-feedback-error-icon' : 'border-border-control',
        ]"
        @input="onInput(index, $event)"
        @keydown="onKeydown(index, $event)"
        @paste="onPaste"
        @focus="($event.target as HTMLInputElement).select()"
      />
    </div>
    <p v-if="error" :id="errorId" role="alert" class="mt-2 text-xs text-feedback-error-fg">{{ error }}</p>
    <p v-else-if="hint" :id="hintId" class="mt-2 text-xs text-text-muted">{{ hint }}</p>
  </fieldset>
</template>
