/** UTC everywhere; Asia/Ho_Chi_Minh only at the UI edge (NFR-L10N-002). */
export const DISPLAY_TIME_ZONE = 'Asia/Ho_Chi_Minh'

export function formatDateTime(isoUtc: string, locale: string): string {
  return new Intl.DateTimeFormat(locale === 'vi' ? 'vi-VN' : 'en-GB', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: DISPLAY_TIME_ZONE,
  }).format(new Date(isoUtc))
}
