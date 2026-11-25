import { mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import Checkbox from '../Checkbox.vue'
import DropdownMenu from '../DropdownMenu.vue'
import FilterChips from '../FilterChips.vue'
import OrderTimeline from '../OrderTimeline.vue'
import QuantityControl from '../QuantityControl.vue'
import RadioGroup from '../RadioGroup.vue'
import TextArea from '../TextArea.vue'
import Toast from '../Toast.vue'
import ToastContainer, { type ToastData } from '../ToastContainer.vue'
import ToggleSwitch from '../ToggleSwitch.vue'

describe('form controls', () => {
  it('Checkbox emits the checked state', async () => {
    const wrapper = mount(Checkbox, { props: { label: 'Agree' } })

    await wrapper.get('input').setValue(true)
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([true])
  })

  it('ToggleSwitch is a switch and emits', async () => {
    const wrapper = mount(ToggleSwitch, { props: { modelValue: false } })
    const input = wrapper.get('input')

    expect(input.attributes('role')).toBe('switch')
    await input.setValue(true)
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([true])
  })

  it('RadioGroup groups options and emits the chosen value', async () => {
    const options = [{ label: 'A', value: 'a' }, { label: 'B', value: 'b' }]
    const wrapper = mount(RadioGroup, { props: { modelValue: 'a', options, label: 'Pick' } })

    expect(wrapper.get('[role=radiogroup]').attributes('aria-label')).toBe('Pick')
    await wrapper.findAll('input')[1]?.setValue(true)
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['b'])
  })

  it('TextArea links its error to the field', () => {
    const wrapper = mount(TextArea, { props: { label: 'Note', error: 'Too long' } })
    const area = wrapper.get('textarea')

    expect(area.attributes('aria-invalid')).toBe('true')
    expect(wrapper.get(`#${area.attributes('aria-describedby')}`).text()).toBe('Too long')
  })

  it('TextArea shows its hint under the field and swaps it for an error', async () => {
    const wrapper = mount(TextArea, { props: { label: 'Note', hint: 'Up to 1,000 characters.' } })
    const area = wrapper.get('textarea')

    expect(wrapper.get(`#${area.attributes('aria-describedby')}`).text()).toBe('Up to 1,000 characters.')
    await wrapper.setProps({ error: 'Too long' })
    expect(wrapper.text()).not.toContain('Up to 1,000 characters.')
    expect(wrapper.get(`#${area.attributes('aria-describedby')}`).text()).toBe('Too long')
  })

  it('QuantityControl stops at min and max', async () => {
    const labels = { decrease: 'Less', increase: 'More', quantity: 'Quantity' }
    const wrapper = mount(QuantityControl, { props: { modelValue: 1, min: 1, max: 2, labels } })

    expect(wrapper.get('[aria-label=Less]').attributes('disabled')).toBeDefined()
    await wrapper.get('[aria-label=More]').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([2])

    const atMax = mount(QuantityControl, { props: { modelValue: 2, min: 1, max: 2, labels } })
    expect(atMax.get('[aria-label=More]').attributes('disabled')).toBeDefined()
  })

  it('FilterChips marks the active chip', async () => {
    const options = [{ label: 'All', value: 'all' }, { label: 'New', value: 'new' }]
    const wrapper = mount(FilterChips, { props: { options, modelValue: 'all' } })

    expect(wrapper.findAll('button')[0]?.attributes('aria-pressed')).toBe('true')
    await wrapper.findAll('button')[1]?.trigger('click')
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['new'])
  })
})

describe('OrderTimeline', () => {
  it('marks the current step', () => {
    const steps = [
      { key: 'placed', label: 'Placed', isCompleted: true, isCurrent: false },
      { key: 'shipped', label: 'Shipped', isCompleted: false, isCurrent: true },
    ]
    const wrapper = mount(OrderTimeline, { props: { steps, label: 'Order progress' } })

    expect(wrapper.get('[aria-current=step]').text()).toContain('Shipped')
  })
})

describe('DropdownMenu', () => {
  it('opens, runs an action and closes', async () => {
    const onClick = vi.fn()
    const wrapper = mount(DropdownMenu, { props: { label: 'Actions', items: [{ label: 'Edit', onClick }] }, attachTo: document.body })

    await wrapper.get('[aria-label=Actions]').trigger('click')
    await wrapper.get('[role=menuitem]').trigger('click')

    expect(onClick).toHaveBeenCalledOnce()
    expect(wrapper.find('[role=menu]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('closes on Escape', async () => {
    const wrapper = mount(DropdownMenu, { props: { label: 'Actions', items: [{ label: 'Edit' }] }, attachTo: document.body })

    await wrapper.get('[aria-label=Actions]').trigger('click')
    await wrapper.get('[role=menu]').trigger('keydown', { key: 'Escape' })

    expect(wrapper.find('[role=menu]').exists()).toBe(false)
    wrapper.unmount()
  })
})

describe('Toast', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('closes by itself after its duration', async () => {
    const wrapper = mount(Toast, { props: { id: 't1', variant: 'success', title: 'Saved', closeLabel: 'Close' } })

    vi.advanceTimersByTime(5000)
    expect(wrapper.emitted('close')?.[0]).toEqual(['t1'])
  })

  it('pauses while hovered', async () => {
    const wrapper = mount(Toast, { props: { id: 't1', variant: 'info', title: 'Hi', closeLabel: 'Close' } })

    await wrapper.trigger('mouseenter')
    vi.advanceTimersByTime(10000)
    expect(wrapper.emitted('close')).toBeUndefined()
  })

  it('keeps an error until it is closed, and announces it as an alert', async () => {
    const wrapper = mount(Toast, { props: { id: 'e1', variant: 'error', title: 'Failed', duration: 0, closeLabel: 'Close' } })

    vi.advanceTimersByTime(60000)
    expect(wrapper.emitted('close')).toBeUndefined()
    expect(wrapper.attributes('role')).toBe('alert')
    await wrapper.get('[aria-label=Close]').trigger('click')
    expect(wrapper.emitted('close')?.[0]).toEqual(['e1'])
  })

  it('ToastContainer shows at most 3 toasts', () => {
    const toasts: ToastData[] = Array.from({ length: 5 }, (_, i) => ({ id: `t${i}`, variant: 'info', title: `T${i}` }))
    const wrapper = mount(ToastContainer, { props: { toasts, closeLabel: 'Close' }, global: { stubs: { teleport: true } } })

    expect(wrapper.findAllComponents(Toast)).toHaveLength(3)
  })
})
