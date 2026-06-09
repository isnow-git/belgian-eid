/**
 * content.js — ISOLATED world.
 *
 * Bridges two communication channels:
 *  1. Page → Extension: window.postMessage ► chrome.runtime.sendMessage
 *  2. Extension → Page: chrome.runtime.onMessage ► window.postMessage
 *     (used for bridge push events such as reader_state_changed)
 *
 * Classic script — ES module imports are not supported for content scripts.
 * Message type strings are intentionally inlined to keep this file self-contained.
 */

// ── Allowlist ─────────────────────────────────────────────────────────────────
// Only forward known eID message types to avoid becoming a generic proxy.

const HANDLED_TYPES = new Set([
  'CHECK_STATUS',
  'DETECT_READER',
  'DETECT_CARD',
  'GET_READERS',
  'READ_CARD',
  'READ_IDENTITY',
  'GET_PIN_STATUS',
  'VERIFY_PIN',
  'SIGN_CHALLENGE',
  'SIGN_HASH',
  'SIGN_DATA',
])

// ── Page → Extension ──────────────────────────────────────────────────────────

window.addEventListener('message', async (event) => {
  if (event.source !== window || event.data?.__eidFrom === 'extension') return

  const { type, id, ...rest } = event.data ?? {}
  if (!type || !HANDLED_TYPES.has(type)) return

  try {
    const response = await chrome.runtime.sendMessage({ type, ...rest })
    window.postMessage(
      { type: `${type}_RESPONSE`, id, __eidFrom: 'extension', ...response },
      '*',
    )
  } catch (err) {
    window.postMessage(
      {
        type:         `${type}_RESPONSE`,
        id,
        __eidFrom:   'extension',
        error:        err?.message ?? "Internal extension error",
      },
      '*',
    )
  }
})

// ── Extension → Page (push events) ───────────────────────────────────────────
// Relay reader_state_changed from the background so the web app can react
// in real time without polling.

chrome.runtime.onMessage.addListener((message) => {
  if (message.type === 'READER_STATE_CHANGED') {
    window.postMessage({ ...message, __eidFrom: 'extension' }, '*')
  }
})
