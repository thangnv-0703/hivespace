import { onBeforeUnmount, ref } from 'vue'

/**
 * A client-side countdown that blocks repeat requests: the 60 s resend cooldown (FEAT-001) and the `Retry-After` wait after a
 * rate-limited request (FEAT-005). `start` may override the length for one run.
 */
export function useCooldown(seconds = 60) {
  const remaining = ref(0)
  let timer: ReturnType<typeof setInterval> | undefined

  function stop() {
    if (timer) clearInterval(timer)
    timer = undefined
  }

  function start(override?: number) {
    stop()
    remaining.value = override ?? seconds
    timer = setInterval(() => {
      remaining.value -= 1
      if (remaining.value <= 0) stop()
    }, 1000)
  }

  onBeforeUnmount(stop)
  return { remaining, start }
}
