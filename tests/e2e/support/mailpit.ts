// Mailpit's HTTP port is fixed by the AppHost (MailpitHttpPort).
const mailpit = process.env.MAILPIT_URL ?? 'http://localhost:8025'

/** Waits up to 60 s for the first email sent to `address` and returns its subject and text body. */
export async function emailFor(address: string): Promise<{ text: string; subject: string }> {
  const deadline = Date.now() + 60_000
  while (Date.now() < deadline) {
    const search = await fetch(`${mailpit}/api/v1/search?query=${encodeURIComponent(`to:${address}`)}`)
    const body = (await search.json()) as { messages: { ID: string }[] }
    if (body.messages.length > 0) {
      const message = await fetch(`${mailpit}/api/v1/message/${body.messages[0]!.ID}`)
      const detail = (await message.json()) as { Text: string; Subject: string }
      return { text: detail.Text, subject: detail.Subject }
    }
    await new Promise((resolve) => setTimeout(resolve, 500))
  }
  throw new Error(`No email for ${address} reached Mailpit within 60 s`)
}
