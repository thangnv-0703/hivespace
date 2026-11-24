import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import { nextTick, ref } from 'vue'

import CodeInput from '../CodeInput.vue'

// FEAT-006 ui-design E-1: the provisional one-time code input.
function mountCode(initial = '') {
  const value = ref(initial)
  const wrapper = mount(CodeInput, {
    props: {
      modelValue: value.value,
      legend: '6-digit code',
      digitLabel: 'Digit {index} of 6',
      'onUpdate:modelValue': async (next: string) => {
        value.value = next
        await wrapper.setProps({ modelValue: next })
      },
    },
    attachTo: document.body,
  })
  return { wrapper, value, boxes: () => wrapper.findAll('input') }
}

describe('CodeInput', () => {
  it('is a fieldset with a legend and six labelled numeric boxes, the first one for one-time-code autofill', () => {
    const { wrapper, boxes } = mountCode()

    expect(wrapper.get('legend').text()).toBe('6-digit code')
    expect(boxes()).toHaveLength(6)
    expect(boxes()[0]!.attributes('autocomplete')).toBe('one-time-code')
    expect(boxes()[2]!.attributes('aria-label')).toBe('Digit 3 of 6')
    expect(boxes().every((b) => b.attributes('inputmode') === 'numeric')).toBe(true)
  })

  it('takes digits only and advances on typing', async () => {
    const { boxes, value } = mountCode()

    await boxes()[0]!.setValue('4')
    await boxes()[1]!.setValue('x')
    await nextTick()

    expect(value.value).toBe('4')
    expect(document.activeElement).toBe(boxes()[1]!.element)
  })

  it('fills every box from a paste and emits complete with the code', async () => {
    const { wrapper, value } = mountCode()

    await wrapper.findAll('input')[3]!.trigger('paste', { clipboardData: { getData: () => ' 042 917 ' } })

    expect(value.value).toBe('042917')
    expect(wrapper.emitted('complete')).toEqual([['042917']])
  })

  it('moves back and clears on Backspace in an empty box', async () => {
    const { boxes, value } = mountCode('12')

    await boxes()[2]!.trigger('keydown', { key: 'Backspace' })
    await nextTick()

    expect(value.value).toBe('1')
    expect(document.activeElement).toBe(boxes()[1]!.element)
  })

  it('links the error to the fieldset and marks every box', () => {
    const wrapper = mount(CodeInput, { props: { legend: 'Code', digitLabel: 'Digit {index}', error: 'That code is not right.' } })

    const error = wrapper.get('[role=alert]')
    expect(error.text()).toBe('That code is not right.')
    expect(wrapper.get('fieldset').attributes('aria-describedby')).toBe(error.attributes('id'))
    expect(wrapper.findAll('input').every((b) => b.classes().includes('border-feedback-error-icon'))).toBe(true)
  })
})
