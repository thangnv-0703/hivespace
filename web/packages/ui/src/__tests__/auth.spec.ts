import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import Alert from '../Alert.vue'
import AuthLayout from '../AuthLayout.vue'
import Button from '../Button.vue'
import Input from '../Input.vue'
import PasswordRules from '../PasswordRules.vue'

describe('AuthLayout', () => {
  it('renders one h1 with the title, the form slot and the corner slot', () => {
    const wrapper = mount(AuthLayout, {
      props: { title: 'Sign in', subtitle: 'Welcome back', imageHeading: 'Shop', imageBody: 'Many stores' },
      slots: { default: '<form data-testid="form" />', corner: '<span data-testid="corner" />' },
    })

    expect(wrapper.findAll('h1')).toHaveLength(1)
    expect(wrapper.get('h1').text()).toBe('Sign in')
    expect(wrapper.find('[data-testid=form]').exists()).toBe(true)
    expect(wrapper.find('[data-testid=corner]').exists()).toBe(true)
    expect(wrapper.get('main').attributes('id')).toBe('auth-main')
    expect(wrapper.get('[data-testid=auth-panel]').text()).toContain('Many stores')
  })

  it('links to the form from a skip link and to the home page from the logo', () => {
    const wrapper = mount(AuthLayout, { props: { title: 'Sign in', skipLabel: 'Skip', homeLabel: 'Home' } })

    expect(wrapper.get('a[href="#auth-main"]').text()).toBe('Skip')
    expect(wrapper.get('a[aria-label=Home]').attributes('href')).toBe('/')
  })
})

describe('PasswordRules', () => {
  const rules = [
    { key: 'length', label: 'At least 8 characters', met: true },
    { key: 'digit', label: 'Contains a number', met: false },
  ]

  it('marks met rules and keeps unmet ones neutral until errors are shown', () => {
    const wrapper = mount(PasswordRules, { props: { rules } })

    expect(wrapper.findAll('li').map((li) => li.attributes('data-state'))).toEqual(['met', 'idle'])
  })

  it('turns unmet rules to the error state when asked, with text kept (colour is not the only signal)', () => {
    const wrapper = mount(PasswordRules, { props: { rules, showErrors: true } })

    expect(wrapper.findAll('li').map((li) => li.attributes('data-state'))).toEqual(['met', 'error'])
    expect(wrapper.text()).toContain('Contains a number')
  })
})

describe('Input additions', () => {
  it('shows the hint, replaces it with the error, and links the description', async () => {
    const wrapper = mount(Input, { props: { label: 'Email', hint: 'We never share it' } })
    expect(wrapper.text()).toContain('We never share it')
    expect(wrapper.get('input').attributes('aria-describedby')).toContain('hint')

    await wrapper.setProps({ error: 'Required' })

    expect(wrapper.text()).not.toContain('We never share it')
    expect(wrapper.get('[role=alert]').text()).toBe('Required')
    expect(wrapper.get('input').attributes('aria-invalid')).toBe('true')
  })

  it('renders the append slot beside the control and exposes focus()', () => {
    const wrapper = mount(Input, { props: { label: 'Password' }, slots: { append: '<button data-testid="reveal" />' }, attachTo: document.body })

    expect(wrapper.find('[data-testid=reveal]').exists()).toBe(true)
    ;(wrapper.vm as unknown as { focus: () => void }).focus()
    expect(document.activeElement).toBe(wrapper.get('input').element)
    wrapper.unmount()
  })
})

describe('Alert and Button additions', () => {
  it('renders an action slot and a strip style', () => {
    const wrapper = mount(Alert, { props: { variant: 'warning', title: 'Verify', strip: true }, slots: { action: '<a href="#">Resend</a>' } })

    expect(wrapper.text()).toContain('Resend')
    expect(wrapper.classes()).toContain('rounded-none')
  })

  it('offers a 48 px large size for primary CTAs', () => {
    const wrapper = mount(Button, { props: { size: 'lg' }, slots: { default: 'Go' } })

    expect(wrapper.classes()).toContain('min-h-12')
  })
})
