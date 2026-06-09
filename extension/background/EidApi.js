import { TIMEOUT } from '../shared/constants.js'

/**
 * Typed façade over {@link NativeClient}.
 *
 * Responsibilities (SRP):
 *  - Maps business operations to bridge message types.
 *  - Validates required parameters before sending.
 *  - Encapsulates timeout decisions.
 *
 * No Chrome APIs here — only bridge calls and input guards.
 * Depends on NativeClient via its interface (DIP).
 */
export class EidApi {
  /** @param {import('./NativeClient.js').NativeClient} client */
  constructor(client) {
    this.#client = client
  }

  #client

  // ── Hardware status ────────────────────────────────────────────────────────

  /**
   * Checks whether the bridge is alive, a reader is connected and a card is inserted.
   * Never rejects — swallows bridge errors and returns all-false on failure.
   *
   * @returns {Promise<{ bridgeActive: boolean, readerPresent: boolean, cardPresent: boolean }>}
   */
  async checkStatus() {
    try {
      const r = await this.#client.send('get_status', {}, TIMEOUT.STATUS)
      return { bridgeActive: true, readerPresent: r.readerPresent, cardPresent: r.cardPresent }
    } catch {
      return { bridgeActive: false, readerPresent: false, cardPresent: false }
    }
  }

  /**
   * Returns all connected PC/SC readers.
   *
   * @returns {Promise<{ readers: Array<{ name: string, slotId: number, hasCardInserted: boolean }> }>}
   */
  getReaders() {
    return this.#client.send('get_readers', {}, TIMEOUT.STATUS)
  }

  // ── Card data ──────────────────────────────────────────────────────────────

  /**
   * Reads identity, address, certificates and optionally the JPEG photo.
   *
   * @param {boolean} [includePhoto=false]
   */
  readCard(includePhoto = false) {
    return this.#client.send('read_card', { includePhoto }, TIMEOUT.OPERATION)
  }

  /** Reads only the identity fields (faster than readCard). */
  readIdentity() {
    return this.#client.send('read_identity', {}, TIMEOUT.OPERATION)
  }

  // ── PIN ────────────────────────────────────────────────────────────────────

  /**
   * Returns the current PIN status without consuming an attempt.
   *
   * @returns {Promise<{ remainingAttempts: number, isBlocked: boolean }>}
   */
  getPinStatus() {
    return this.#client.send('get_pin_status', {}, TIMEOUT.OPERATION)
  }

  /**
   * Verifies the cardholder PIN against the card.
   *
   * @param {string} pin
   * @throws {Error} `err.triesRemaining` set on wrong PIN; `err.blocked` set when card is blocked.
   */
  verifyPin(pin) {
    if (!pin) throw new Error('Missing PIN code')
    return this.#client.send('verify_pin', { pin }, TIMEOUT.OPERATION)
  }

  // ── Cryptography ───────────────────────────────────────────────────────────

  /**
   * Signs a server-issued challenge with the card's **authentication** key.
   * The bridge hashes the challenge internally before signing.
   *
   * @param {string} pin
   * @param {string} challenge  Base64-encoded challenge bytes.
   * @param {string} [algorithm='sha256']
   */
  signChallenge(pin, challenge, algorithm = 'sha256') {
    if (!pin || !challenge) throw new Error('Missing PIN or challenge')
    return this.#client.send('sign_challenge', { pin, challenge, algorithm }, TIMEOUT.OPERATION)
  }

  /**
   * Signs a pre-computed hash with the card's **non-repudiation** key.
   * Use for large documents: compute the hash client-side and pass it here.
   *
   * @param {string} pin
   * @param {string} hash  Base64-encoded hash.
   * @param {string} [algorithm='sha256']
   */
  signHash(pin, hash, algorithm = 'sha256') {
    if (!pin || !hash) throw new Error('Missing PIN or hash')
    return this.#client.send('sign_hash', { pin, hash, algorithm }, TIMEOUT.OPERATION)
  }

  /**
   * Signs raw data with the card's **non-repudiation** key.
   * The bridge computes the hash internally — suitable only for small payloads.
   *
   * @param {string} pin
   * @param {string} data  Base64-encoded data.
   * @param {string} [algorithm='sha256']
   */
  signData(pin, data, algorithm = 'sha256') {
    if (!pin || !data) throw new Error('Missing PIN or data')
    return this.#client.send('sign_data', { pin, data, algorithm }, TIMEOUT.OPERATION)
  }
}
