/**
 * Service worker — composition root.
 *
 * Wires up NativeClient → EidApi → MessageDispatcher and registers
 * the two event listeners:
 *  1. chrome.runtime.onMessage  — handles requests from content scripts / popup.
 *  2. NativeClient.onPush       — forwards bridge push events to extension pages.
 */

import { NativeClient }      from './NativeClient.js'
import { EidApi }            from './EidApi.js'
import { MessageDispatcher } from './MessageDispatcher.js'
import { MSG }               from '../shared/constants.js'

// ── Composition ───────────────────────────────────────────────────────────────

const client     = new NativeClient()
const api        = new EidApi(client)
const dispatcher = new MessageDispatcher(api)

// ── Bridge push events → extension pages ──────────────────────────────────────
// reader_state_changed arrives without a correlation id from BelgianEidBridge.
// Broadcast it so the popup and content scripts can react in real time.

client.onPush((msg) => {
  if (msg.type !== 'reader_state_changed') return

  chrome.runtime
    .sendMessage({
      type:      MSG.READER_STATE_CHANGED,
      eventKind: msg.eventKind,
      reader:    msg.reader,
    })
    .catch(() => { /* no popup or content script open — ignore */ })
})

// ── Content script ↔ background ───────────────────────────────────────────────

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  dispatcher
    .dispatch(message)
    .then(sendResponse)
    .catch((err) =>
      sendResponse({
        error:          err.message ?? 'Unknown error',
        triesRemaining: err.triesRemaining,
        blocked:        err.blocked,
      })
    )

  return true  // keep the channel open for the async response
})
