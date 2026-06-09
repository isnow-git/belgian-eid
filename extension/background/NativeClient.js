import { HOST_NAME, TIMEOUT } from '../shared/constants.js'

/**
 * Manages the Chrome Native Messaging port lifecycle.
 *
 * Responsibilities (SRP):
 *  - Opens / reconnects the native port on demand.
 *  - Correlates outbound requests to inbound responses via an `id` field.
 *  - Routes unsolicited push messages (no `id`) to registered listeners.
 *
 * All other concerns (business logic, Chrome APIs beyond the port) belong
 * to higher layers.
 */
export class NativeClient {
  #port          = null
  #pending       = new Map()   // id → { resolve, reject, timerId }
  #counter       = 0
  #pushListeners = new Set()

  // ── Public API ─────────────────────────────────────────────────────────────

  /**
   * Sends a typed request to the native host.
   *
   * @param {string} type      Bridge message type (e.g. `'get_status'`).
   * @param {object} [params]  Additional fields merged into the request frame.
   * @param {number} [timeoutMs]
   * @returns {Promise<object>} Resolves with the response payload.
   */
  send(type, params = {}, timeoutMs = TIMEOUT.STATUS) {
    return new Promise((resolve, reject) => {
      const id = String(++this.#counter)

      const timerId = setTimeout(() => {
        this.#pending.delete(id)
        reject(new Error(`Timeout — '${type}' received no response after ${timeoutMs} ms`))
      }, timeoutMs)

      this.#pending.set(id, { resolve, reject, timerId })

      try {
        this.#getPort().postMessage({ id, type, ...params })
      } catch (err) {
        this.#pending.delete(id)
        clearTimeout(timerId)
        reject(err)
      }
    })
  }

  /**
   * Registers a listener for push messages from the native host
   * (messages that arrive without a correlation `id`).
   *
   * @param {(message: object) => void} listener
   * @returns {() => void} Unsubscribe function.
   */
  onPush(listener) {
    this.#pushListeners.add(listener)
    return () => this.#pushListeners.delete(listener)
  }

  // ── Private ────────────────────────────────────────────────────────────────

  #getPort() {
    if (this.#port) return this.#port

    this.#port = chrome.runtime.connectNative(HOST_NAME)

    this.#port.onMessage.addListener((msg) => {
      if (msg.id !== undefined) {
        this.#handleResponse(msg)
      } else {
        this.#handlePush(msg)
      }
    })

    this.#port.onDisconnect.addListener(() => {
      const err = new Error(
        chrome.runtime.lastError?.message ?? 'Connection to the eID bridge lost'
      )
      for (const { reject, timerId } of this.#pending.values()) {
        clearTimeout(timerId)
        reject(err)
      }
      this.#pending.clear()
      this.#port = null
    })

    return this.#port
  }

  #handleResponse(msg) {
    const req = this.#pending.get(msg.id)
    if (!req) return

    this.#pending.delete(msg.id)
    clearTimeout(req.timerId)

    if (msg.error) {
      const err = Object.assign(new Error(msg.error), {
        triesRemaining: msg.triesRemaining,
        blocked:        msg.blocked,
      })
      req.reject(err)
    } else {
      req.resolve(msg)
    }
  }

  #handlePush(msg) {
    for (const listener of this.#pushListeners) {
      try { listener(msg) } catch { /* isolate listener errors */ }
    }
  }
}
