import { onBeforeUnmount, ref } from 'vue'

/** A client-side countdown that blocks repeat requests (ui-design: 60 s resend cooldown). */
export function useCooldown(seconds = 60) {
  const remaining = ref(0)
  let timer: ReturnType<typeof setInterval> | undefined

  function stop() {
    if (timer) clearInterval(timer)
    timer = undefined
  }

  function start() {
    stop()
    remaining.value = seconds
    timer = setInterval(() => {
      remaining.value -= 1
      if (remaining.value <= 0) stop()
    }, 1000)
  }

  onBeforeUnmount(stop)
  return { remaining, start }
}
