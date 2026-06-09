/** Native Messaging host name (must match be.belgianeid.bridge.json). */
export const HOST_NAME = 'be.belgianeid.bridge'

/** Request timeouts in milliseconds. */
export const TIMEOUT = {
  STATUS:    5_000,
  OPERATION: 30_000,
}

/**
 * All message types exchanged between extension layers.
 *
 * Flow:
 *   Page (MAIN world)
 *     └─[window.postMessage]─► content.js (ISOLATED world)
 *          └─[chrome.runtime.sendMessage]─► background (service worker)
 *               └─[Native Messaging]─► BelgianEidBridge
 *
 * Push events travel the reverse path:
 *   BelgianEidBridge ─► background ─[chrome.runtime.sendMessage]─► content.js / popup
 */
export const MSG = {
  // ── Requests (page → extension → bridge) ──────────────────────────────────

  /** Checks bridge alive + reader present + card present. */
  CHECK_STATUS:   'CHECK_STATUS',

  /** Throws if no reader is connected. */
  DETECT_READER:  'DETECT_READER',

  /** Throws if no card is inserted. */
  DETECT_CARD:    'DETECT_CARD',

  /** Returns all connected PC/SC readers. */
  GET_READERS:    'GET_READERS',

  /** Reads identity + address + certificates (+ optional photo). */
  READ_CARD:      'READ_CARD',

  /** Reads only identity fields. */
  READ_IDENTITY:  'READ_IDENTITY',

  /** Returns remaining PIN attempts and blocked flag. */
  GET_PIN_STATUS: 'GET_PIN_STATUS',

  /** Verifies the cardholder PIN. */
  VERIFY_PIN:     'VERIFY_PIN',

  /** Signs a server challenge with the authentication key. */
  SIGN_CHALLENGE: 'SIGN_CHALLENGE',

  /** Signs a pre-computed hash with the non-repudiation key. */
  SIGN_HASH:      'SIGN_HASH',

  /** Signs raw data with the non-repudiation key (lib hashes internally). */
  SIGN_DATA:      'SIGN_DATA',

  // ── Push events (bridge → background → content / popup) ───────────────────

  /**
   * Emitted by the bridge when a reader is plugged/unplugged or a card is
   * inserted/removed.  No request is needed — the background forwards it
   * automatically to all extension pages and the content script.
   *
   * Payload: { eventKind, reader: { name, slotId, hasCardInserted } }
   * eventKind: 'readerConnected' | 'readerDisconnected' | 'cardInserted' | 'cardRemoved'
   */
  READER_STATE_CHANGED: 'READER_STATE_CHANGED',
}
