import { createHiveI18n } from '@hivespace/core'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import { PickupPayoutPage, ShopProfilePage, settingsSessionKey, type SettingsSession } from '../features/settings'
import { loadShop, saveAddress, savePayout, saveProfile, type MyShop } from '../features/settings/api'
import { messages } from '../locales'

vi.mock('../features/settings/api')

const address = { contactName: 'An', phone: '+84912345678', province: 'Hà Nội', district: 'Ba Đình', ward: 'Phúc Xá', street: '12 Lê Lợi' }
const payout = { bankName: 'Vietcombank', accountNumberLast4: '4567', accountHolder: 'NGUYEN VAN AN' }
const fresh: MyShop = {
  shopId: 's1',
  name: 'Tiệm Bánh An',
  sellerType: 'individual',
  description: null,
  pickupAddress: null,
  payout: null,
  setup: { pickupAddress: false, payout: false },
}
const failure = (status: number, code: string, extra: object = {}) => ({ ok: false as const, failure: { status, code, details: [], ...extra } })

async function mountPage(page: typeof ShopProfilePage | typeof PickupPayoutPage) {
  const session: SettingsSession = {
    refresh: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
    markSignedOut: vi.fn<() => void>(),
    notify: vi.fn<SettingsSession['notify']>(),
  }
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: { template: '<p/>' } }, { path: '/settings', component: page }] })
  await router.push('/settings')
  const wrapper = mount(page, { global: { plugins: [router, createHiveI18n(messages)], provide: { [settingsSessionKey as symbol]: session } } })
  await flushPromises()
  return { wrapper, session }
}

describe('ShopProfilePage (SCR-SEL-007)', () => {
  beforeEach(() => {
    vi.mocked(loadShop).mockReset()
    vi.mocked(saveProfile).mockReset()
    localStorage.setItem('hs-locale', 'en')
  })

  it('shows the loading skeleton, then the shop pre-filled with the seller type read-only', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: { ...fresh, description: 'Bánh mì nóng' } })
    const { wrapper } = await mountPage(ShopProfilePage)

    expect((wrapper.get('input').element as HTMLInputElement).value).toBe('Tiệm Bánh An')
    expect((wrapper.get('textarea').element as HTMLTextAreaElement).value).toBe('Bánh mì nóng')
    expect(wrapper.get('[data-testid=seller-type]').text()).toBe('Individual')
    expect(wrapper.get('[data-testid=description-count]').text()).toBe('12 / 1000')
  })

  it('shows a load error with Retry', async () => {
    vi.mocked(loadShop).mockResolvedValueOnce(failure(500, 'common.internal_error'))
    vi.mocked(loadShop).mockResolvedValueOnce({ ok: true, data: fresh })
    const { wrapper } = await mountPage(ShopProfilePage)

    expect(wrapper.text()).toContain("We couldn't load your shop. Try again.")
    await wrapper.get('[data-testid=retry]').trigger('click')
    await flushPromises()

    expect(loadShop).toHaveBeenCalledTimes(2)
    expect(wrapper.find('input').exists()).toBe(true)
  })

  it('refreshes the session on 403 so the route guard can reroute', async () => {
    vi.mocked(loadShop).mockResolvedValue(failure(403, 'common.forbidden'))
    const { session } = await mountPage(ShopProfilePage)

    expect(session.refresh).toHaveBeenCalled()
  })

  it('saves the profile, announces it and keeps the saved copy', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: fresh })
    vi.mocked(saveProfile).mockResolvedValue({ ok: true, data: { ...fresh, name: 'Tiệm Mới', description: 'Mô tả' } })
    const { wrapper, session } = await mountPage(ShopProfilePage)

    await wrapper.get('input').setValue('Tiệm Mới')
    await wrapper.get('textarea').setValue('Mô tả')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(saveProfile).toHaveBeenCalledWith({ name: 'Tiệm Mới', description: 'Mô tả' })
    expect(session.notify).toHaveBeenCalledWith({ variant: 'success', title: 'Shop profile saved' })
  })

  it('checks the name on the client before sending', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: fresh })
    const { wrapper } = await mountPage(ShopProfilePage)

    await wrapper.get('input').setValue('ab')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(saveProfile).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Use at least 3 characters.')
  })

  it('shows field errors from details next to their fields and keeps what was typed', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: fresh })
    vi.mocked(saveProfile).mockResolvedValue(
      failure(400, 'common.validation_failed', {
        details: [
          { field: 'name', code: 'identity.shop_name_invalid', message: 'Name has bad characters.' },
          { field: 'description', code: 'identity.shop_description_too_long', message: 'Too long.' },
        ],
      }),
    )
    const { wrapper } = await mountPage(ShopProfilePage)

    await wrapper.get('textarea').setValue('x')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Name has bad characters.')
    expect(wrapper.text()).toContain('Too long.')
    expect((wrapper.get('textarea').element as HTMLTextAreaElement).value).toBe('x')
  })

  it('shows shop_name_taken on the name field', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: fresh })
    vi.mocked(saveProfile).mockResolvedValue(failure(409, 'identity.shop_name_taken', { message: 'That name is taken.' }))
    const { wrapper, session } = await mountPage(ShopProfilePage)

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('#' + wrapper.get('input').attributes('aria-describedby')).text()).toBe('That name is taken.')
    expect(session.notify).not.toHaveBeenCalled()
  })

  it('shows an error alert on a server failure', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: fresh })
    vi.mocked(saveProfile).mockResolvedValue(failure(500, 'common.internal_error'))
    const { wrapper } = await mountPage(ShopProfilePage)

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain("We couldn't save your shop profile. Try again.")
  })
})

describe('PickupPayoutPage (SCR-SEL-008)', () => {
  beforeEach(() => {
    vi.mocked(loadShop).mockReset()
    vi.mocked(saveAddress).mockReset()
    vi.mocked(savePayout).mockReset()
    localStorage.setItem('hs-locale', 'en')
  })

  it('on a first visit shows both Missing badges and the payout form directly', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: fresh })
    const { wrapper } = await mountPage(PickupPayoutPage)

    expect(wrapper.get('[data-testid=address-badge]').text()).toBe('Missing')
    expect(wrapper.get('[data-testid=payout-badge]').text()).toBe('Missing')
    expect(wrapper.find('[data-testid=payout-card] form').exists()).toBe(true)
  })

  it('pre-fills the address and shows the payout as a masked summary when both are saved', async () => {
    vi.mocked(loadShop).mockResolvedValue({
      ok: true,
      data: { ...fresh, pickupAddress: address, payout, setup: { pickupAddress: true, payout: true } },
    })
    const { wrapper } = await mountPage(PickupPayoutPage)

    expect(wrapper.get('[data-testid=address-badge]').text()).toBe('Saved')
    expect((wrapper.get('input[type=tel]').element as HTMLInputElement).value).toBe('+84912345678')
    expect(wrapper.get('[data-testid=masked-number]').text()).toContain('•••• 4567')
    expect(wrapper.get('[data-testid=masked-number]').text()).toContain('ending in 4 5 6 7')
    expect(wrapper.find('[data-testid=payout-card] form').exists()).toBe(false)
  })

  it('checks required fields and the phone before sending the address', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: fresh })
    const { wrapper } = await mountPage(PickupPayoutPage)

    await wrapper.get('[data-testid=address-card] form').trigger('submit')
    await flushPromises()

    expect(saveAddress).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Enter contact name.')
    expect(wrapper.text()).toContain('Enter district.')
    expect(wrapper.find('[data-testid=address-summary]').exists()).toBe(true)
  })

  it('shows server field errors from details on the address fields', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: fresh })
    vi.mocked(saveAddress).mockResolvedValue(
      failure(400, 'common.validation_failed', { details: [{ field: 'phone', code: 'identity.phone_invalid', message: 'Bad phone from server.' }] }),
    )
    const { wrapper } = await mountPage(PickupPayoutPage)
    const form = wrapper.get('[data-testid=address-card] form')
    const fields = form.findAll('input')
    for (const [index, value] of ['An', '0912345678', 'Hà Nội', 'Ba Đình', 'Phúc Xá', '12 Lê Lợi'].entries()) await fields[index]!.setValue(value)

    await form.trigger('submit')
    await flushPromises()

    expect(saveAddress).toHaveBeenCalled()
    expect(wrapper.text()).toContain('Bad phone from server.')
  })

  it('saves the address and says the shop is ready when it completes the checklist', async () => {
    const ready = { ...fresh, pickupAddress: address, payout, setup: { pickupAddress: true, payout: true } }
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: { ...fresh, payout, setup: { pickupAddress: false, payout: true } } })
    vi.mocked(saveAddress).mockResolvedValue({ ok: true, data: ready })
    const { wrapper, session } = await mountPage(PickupPayoutPage)
    const form = wrapper.get('[data-testid=address-card] form')
    const fields = form.findAll('input')
    for (const [index, value] of ['An', '0912 345 678', 'Hà Nội', 'Ba Đình', 'Phúc Xá', '12 Lê Lợi'].entries()) await fields[index]!.setValue(value)

    await form.trigger('submit')
    await flushPromises()

    expect(session.notify).toHaveBeenCalledWith({ variant: 'success', title: 'Your shop is ready to publish products.' })
    expect(wrapper.get('[data-testid=address-badge]').text()).toBe('Saved')
  })

  it('checks the account number before sending the payout', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: fresh })
    const { wrapper } = await mountPage(PickupPayoutPage)
    const form = wrapper.get('[data-testid=payout-card] form')
    const fields = form.findAll('input')
    await fields[0]!.setValue('Vietcombank')
    await fields[1]!.setValue('12ab')
    await fields[2]!.setValue('NGUYEN VAN AN')

    await form.trigger('submit')
    await flushPromises()

    expect(savePayout).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Enter 6 to 19 digits.')
  })

  it('clears the account number and shows •••• 1234 after the payout is saved', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: fresh })
    vi.mocked(savePayout).mockResolvedValue({
      ok: true,
      data: { ...fresh, payout: { ...payout, accountNumberLast4: '1234' }, setup: { pickupAddress: false, payout: true } },
    })
    const { wrapper, session } = await mountPage(PickupPayoutPage)
    const form = wrapper.get('[data-testid=payout-card] form')
    const fields = form.findAll('input')
    await fields[0]!.setValue('Vietcombank')
    await fields[1]!.setValue('0071 0012 31234')
    await fields[2]!.setValue('NGUYEN VAN AN')

    await form.trigger('submit')
    await flushPromises()

    expect(savePayout).toHaveBeenCalledWith({ bankName: 'Vietcombank', accountNumber: '0071 0012 31234', accountHolder: 'NGUYEN VAN AN' })
    expect(wrapper.get('[data-testid=masked-number]').text()).toContain('•••• 1234')
    expect(wrapper.html()).not.toContain('0071')
    expect(session.notify).toHaveBeenCalledWith({ variant: 'success', title: 'Payout details saved' })
  })

  it('Change opens the form with bank and holder pre-filled and an empty account number, and Cancel closes it', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: { ...fresh, payout, setup: { pickupAddress: false, payout: true } } })
    const { wrapper } = await mountPage(PickupPayoutPage)

    await wrapper.get('[data-testid=change-payout]').trigger('click')
    const fields = wrapper.get('[data-testid=payout-card] form').findAll('input').map((input) => (input.element as HTMLInputElement).value)
    expect(fields).toEqual(['Vietcombank', '', 'NGUYEN VAN AN'])

    await wrapper.get('[data-testid=cancel-change]').trigger('click')
    expect(wrapper.find('[data-testid=masked-number]').exists()).toBe(true)
  })

  it('keeps the typed account number when a payout save fails on the server', async () => {
    vi.mocked(loadShop).mockResolvedValue({ ok: true, data: fresh })
    vi.mocked(savePayout).mockResolvedValue(failure(500, 'common.internal_error'))
    const { wrapper } = await mountPage(PickupPayoutPage)
    const form = wrapper.get('[data-testid=payout-card] form')
    const fields = form.findAll('input')
    await fields[0]!.setValue('Vietcombank')
    await fields[1]!.setValue('0071001234567')
    await fields[2]!.setValue('NGUYEN VAN AN')

    await form.trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain("We couldn't save your payout details. Try again.")
    expect((wrapper.findAll('[data-testid=payout-card] input')[1]!.element as HTMLInputElement).value).toBe('0071001234567')
  })

  it('shows a load error in place of both cards', async () => {
    vi.mocked(loadShop).mockResolvedValue(failure(500, 'common.internal_error'))
    const { wrapper } = await mountPage(PickupPayoutPage)

    expect(wrapper.text()).toContain("We couldn't load your shop. Try again.")
    expect(wrapper.find('[data-testid=address-card]').exists()).toBe(false)
  })
})
