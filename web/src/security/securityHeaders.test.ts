// @vitest-environment node
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

const root = resolve(__dirname, '../..')
const headersFile = readFileSync(resolve(root, 'public/_headers'), 'utf8')
const vercel = JSON.parse(readFileSync(resolve(root, 'vercel.json'), 'utf8')) as {
  headers?: { source: string; headers: { key: string; value: string }[] }[]
}
const vercelHeaders = Object.fromEntries(
  (vercel.headers?.find((h) => h.source === '/(.*)')?.headers ?? []).map((h) => [h.key, h.value]),
)

const required = [
  'Content-Security-Policy',
  'Strict-Transport-Security',
  'X-Content-Type-Options',
  'Referrer-Policy',
  'Cross-Origin-Opener-Policy',
]

describe.each([
  ['Cloudflare public/_headers', (k: string) => headersFile.includes(`${k}:`)],
  ['Vercel vercel.json', (k: string) => k in vercelHeaders],
])('%s', (_name, has) => {
  it.each(required)('sets %s', (key) => {
    expect(has(key)).toBe(true)
  })
})

describe('Content-Security-Policy', () => {
  const csp = vercelHeaders['Content-Security-Policy'] ?? ''
  it('is identical in both deploy targets', () => {
    expect(headersFile).toContain(`Content-Security-Policy: ${csp}`)
  })
  it.each(["script-src 'self'", "object-src 'none'", "frame-ancestors 'none'", "base-uri 'none'"])(
    'contains %s',
    (directive) => expect(csp).toContain(directive),
  )
  it('never allows inline or eval scripts', () => {
    const scriptSrc = csp.split(';').find((d) => d.trim().startsWith('script-src')) ?? ''
    expect(scriptSrc).not.toMatch(/unsafe-inline|unsafe-eval|\*/)
  })
})
