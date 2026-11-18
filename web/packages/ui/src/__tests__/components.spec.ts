import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'

import AppShell from '../AppShell.vue'
import ErrorPage from '../ErrorPage.vue'
import Button from '../Button.vue'
import Input from '../Input.vue'
import LanguageSwitcher from '../LanguageSwitcher.vue'
import Pagination from '../Pagination.vue'
import Tabs from '../Tabs.vue'
import type { SidebarMenuGroup } from '../sidebar.types'

const Icon = defineComponent({ render: () => h('svg') })

describe('Button', () => {
  it('blocks clicks and is busy while loading', async () => {
    const wrapper = mount(Button, { props: { loading: true }, slots: { default: 'Save' } })

    expect(wrapper.attributes('disabled')).toBeDefined()
    expect(wrapper.attributes('aria-busy')).toBe('true')
    await wrapper.trigger('click')
    expect(wrapper.emitted('click')).toBeUndefined()
  })

  it('names an icon-only button with aria-label', () => {
    const wrapper = mount(Button, { props: { iconOnly: true, ariaLabel: 'Refresh', startIcon: Icon } })

    expect(wrapper.attributes('aria-label')).toBe('Refresh')
  })
})

describe('Button as a link', () => {
  it('renders an anchor with the button look when given an href', () => {
    const wrapper = mount(Button, { props: { href: '/seller/' }, slots: { default: 'Back' } })

    expect(wrapper.element.tagName).toBe('A')
    expect(wrapper.attributes('href')).toBe('/seller/')
    expect(wrapper.attributes('type')).toBeUndefined()
    expect(wrapper.classes()).toContain('bg-action-primary-bg')
  })
})

describe('ErrorPage', () => {
  it('shows the message and a button-styled link home', () => {
    const wrapper = mount(ErrorPage, {
      props: { code: '404', title: 'Not found', message: 'Gone', actionLabel: 'Home', actionHref: '/' },
    })

    expect(wrapper.get('[role=alert]').text()).toContain('Not found')
    const link = wrapper.get('a')
    expect(link.text()).toBe('Home')
    expect(link.classes()).toContain('bg-action-primary-bg')
  })
})

describe('Input', () => {
  it('links the error message to the field', () => {
    const wrapper = mount(Input, { props: { label: 'Name', error: 'Required' } })
    const input = wrapper.get('input')

    expect(input.attributes('aria-invalid')).toBe('true')
    expect(wrapper.get(`#${input.attributes('aria-describedby')}`).text()).toBe('Required')
  })

  it('emits the typed value', async () => {
    const wrapper = mount(Input)

    await wrapper.get('input').setValue('abc')
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['abc'])
  })
})

describe('Pagination', () => {
  const labels = { nav: 'Pages', previous: 'Previous', next: 'Next' }

  it('marks the current page and emits the chosen one', async () => {
    const wrapper = mount(Pagination, { props: { currentPage: 2, totalPages: 5, labels } })

    expect(wrapper.get('[aria-current=page]').text()).toBe('2')
    await wrapper.findAll('button').find((b) => b.text() === '4')?.trigger('click')
    expect(wrapper.emitted('update:currentPage')?.[0]).toEqual([4])
  })

  it('disables previous on the first page', () => {
    const wrapper = mount(Pagination, { props: { currentPage: 1, totalPages: 3, labels } })

    expect(wrapper.get('[aria-label=Previous]').attributes('disabled')).toBeDefined()
  })
})

describe('Tabs', () => {
  it('selects a tab', async () => {
    const options = [
      { label: 'All', value: 'all' },
      { label: 'Draft', value: 'draft' },
    ]
    const wrapper = mount(Tabs, { props: { options, modelValue: 'all' } })

    expect(wrapper.findAll('[role=tab]')[0]?.attributes('aria-selected')).toBe('true')
    await wrapper.findAll('[role=tab]')[1]?.trigger('click')
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['draft'])
  })
})

describe('AppShell', () => {
  const menuGroups: SidebarMenuGroup[] = [
    {
      title: 'Menu',
      items: [
        { name: 'Dashboard', icon: Icon, path: '/' },
        { name: 'Products', icon: Icon, subItems: [{ name: 'List', path: '/products' }] },
      ],
    },
  ]
  const labels = { nav: 'Main', search: 'Search', searchPlaceholder: 'Search...', openMenu: 'Open menu', closeMenu: 'Close menu', skipToContent: 'Skip' }

  function shell() {
    return mount(AppShell, { props: { menuGroups, activePath: '/', labels }, slots: { default: '<p>content</p>' } })
  }

  it('marks the active item and renders the content', () => {
    const wrapper = shell()

    expect(wrapper.get('[aria-current=page]').text()).toBe('Dashboard')
    expect(wrapper.get('main').text()).toBe('content')
  })

  it('expands a group and collapses the sidebar from the header toggle', async () => {
    const wrapper = shell()

    expect(wrapper.text()).toContain('Menu')
    await wrapper.get('[aria-expanded=false]').trigger('click')
    expect(wrapper.find('[aria-expanded=true]').exists()).toBe(true)

    await wrapper.get('[aria-label="Open menu"]').trigger('click')
    // Collapsed: group titles and item labels are hidden (icons only).
    expect(wrapper.get('[data-testid=sidebar]').text()).not.toContain('Menu')
  })
})

describe('LanguageSwitcher', () => {
  function mountSwitcher(modelValue = 'vi') {
    return mount(LanguageSwitcher, { props: { modelValue, label: 'Language' }, attachTo: document.body })
  }

  it('opens a menu with a row per language and marks the current one', async () => {
    const wrapper = mountSwitcher()

    expect(wrapper.find('[role=menu]').exists()).toBe(false)
    await wrapper.get('[data-testid=language-switcher]').trigger('click')

    const rows = wrapper.findAll('[role=menuitemradio]')
    expect(rows.map((row) => row.text())).toEqual(['Tiếng Việt', 'English'])
    expect(rows[0]?.attributes('aria-checked')).toBe('true')
    expect(rows[1]?.attributes('aria-checked')).toBe('false')
    wrapper.unmount()
  })

  it('emits the chosen language and closes', async () => {
    const wrapper = mountSwitcher()

    await wrapper.get('[data-testid=language-switcher]').trigger('click')
    await wrapper.findAll('[role=menuitemradio]')[1]?.trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['en'])
    expect(wrapper.find('[role=menu]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('closes on Escape and on a click outside', async () => {
    const wrapper = mountSwitcher()
    const trigger = wrapper.get('[data-testid=language-switcher]')

    await trigger.trigger('click')
    await wrapper.get('[role=menu]').trigger('keydown', { key: 'Escape' })
    expect(wrapper.find('[role=menu]').exists()).toBe(false)

    await trigger.trigger('click')
    document.body.click()
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[role=menu]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('moves between rows with the arrow keys', async () => {
    const wrapper = mountSwitcher()

    await wrapper.get('[data-testid=language-switcher]').trigger('click')
    await wrapper.vm.$nextTick()
    const rows = wrapper.findAll('[role=menuitemradio]')
    expect(document.activeElement).toBe(rows[0]?.element)

    await wrapper.get('[role=menu]').trigger('keydown', { key: 'ArrowDown' })
    expect(document.activeElement).toBe(rows[1]?.element)
    wrapper.unmount()
  })
})
