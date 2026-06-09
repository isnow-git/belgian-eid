import { MSG } from '../shared/constants.js'

/**
 * Routes `chrome.runtime` messages to the appropriate {@link EidApi} method.
 *
 * Responsibilities (SRP):
 *  - One switch case per message type — nothing else.
 *  - Exposes a single `dispatch(message)` method.
 *
 * Adding a new command requires only one line here and one case in the switch.
 * No knowledge of Native Messaging or Chrome port internals (DIP).
 */
export class MessageDispatcher {
  /** @param {import('./EidApi.js').EidApi} api */
  constructor(api) {
    this.#api = api
  }

  #api

  /**
   * Dispatches an incoming message to the correct API method.
   *
   * @param {{ type: string, [key: string]: any }} message
   * @returns {Promise<object>}
   */
  async dispatch(message) {
    const { type, pin, challenge, hash, data, algorithm, includePhoto } = message

    switch (type) {
      case MSG.CHECK_STATUS:   return this.#api.checkStatus()
      case MSG.DETECT_READER:  return this.#requireReader()
      case MSG.DETECT_CARD:    return this.#requireCard()
      case MSG.GET_READERS:    return this.#api.getReaders()
      case MSG.READ_CARD:      return this.#api.readCard(includePhoto)
      case MSG.READ_IDENTITY:  return this.#api.readIdentity()
      case MSG.GET_PIN_STATUS: return this.#api.getPinStatus()
      case MSG.VERIFY_PIN:     return this.#api.verifyPin(pin)
      case MSG.SIGN_CHALLENGE: return this.#api.signChallenge(pin, challenge, algorithm)
      case MSG.SIGN_HASH:      return this.#api.signHash(pin, hash, algorithm)
      case MSG.SIGN_DATA:      return this.#api.signData(pin, data, algorithm)
      default:
        throw new Error(`Unhandled message type: '${type}'`)
    }
  }

  // ── Compound checks ────────────────────────────────────────────────────────

  async #requireReader() {
    const { readerPresent } = await this.#api.checkStatus()
    if (!readerPresent) throw new Error('No card reader connected')
    return {}
  }

  async #requireCard() {
    const { cardPresent } = await this.#api.checkStatus()
    if (!cardPresent) throw new Error('No eID card inserted in the reader')
    return {}
  }
}
