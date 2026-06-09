/**
 * inject.js — MAIN world.
 *
 * Sets a version marker in the page's JavaScript context so the web app
 * can detect that the extension is installed (e.g. via useExtensionCheck()).
 *
 * Classic script — runs before any page script thanks to run_at: document_start.
 */
window.__EID_EXTENSION_VERSION__ = "1.0.0";
