import { describe, expect, it } from 'vitest'
import { detectLocale, formatMoney } from './index'

describe('formatMoney (FR-CUR-002)', () => {
  it('formats VND in Vietnamese as whole dong', () => {
    expect(formatMoney({ amount: 300000, currency: 'VND' }, 'vi').replace(/\s/g, ' ')).toBe('300.000 ₫')
  })

  it('formats VND in English', () => {
    expect(formatMoney({ amount: 300000, currency: 'VND' }, 'en')).toBe('₫300,000')
  })

  it('rejects a currency that is not enabled', () => {
    expect(() => formatMoney({ amount: 1, currency: 'USD' }, 'en')).toThrow()
  })
})

describe('detectLocale (FR-LOC-003)', () => {
  it.each([
    [null, ['en-US'], 'en'],
    [null, ['vi-VN'], 'vi'],
    [null, ['fr-FR'], 'vi'],
    ['vi', ['en-US'], 'vi'],
  ] as const)('saved=%s browser=%s → %s', (saved, langs, want) => {
    expect(detectLocale(saved, langs)).toBe(want)
  })
})
