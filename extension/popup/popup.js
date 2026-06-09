/**
 * popup.js — ES module.
 *
 * Responsibilities:
 *  - Displays current bridge / reader / card status on open.
 *  - Updates the reader and card badges in real time via push events
 *    (no polling — the background forwards reader_state_changed automatically).
 */

import { MSG } from "../shared/constants.js";

// ── Init ──────────────────────────────────────────────────────────────────────

document.getElementById("version-label").textContent = "v" + chrome.runtime.getManifest().version;

document.getElementById("btn-refresh").addEventListener("click", refresh);

refresh();

// ── Push events — live update ─────────────────────────────────────────────────
// Receives reader_state_changed from the background service worker while the
// popup is open, so badges update instantly without re-querying.

chrome.runtime.onMessage.addListener((message) => {
  if (message.type !== MSG.READER_STATE_CHANGED) return;

  const { eventKind, reader } = message;

  switch (eventKind) {
    case "readerConnected":
      setStatus("reader", "ok", "Connected");
      setStatus(
        "card",
        reader.hasCardInserted ? "ok" : "error",
        reader.hasCardInserted ? "Detected" : "Absent"
      );
      refresh(); // In case the reader has a card already inserted when the popup opens, we might not have the latest status. Refresh to be sure.
      break;
    case "readerDisconnected":
      setStatus("reader", "error", "Absent");
      setStatus("card", "idle");
      break;
    case "cardInserted":
      setStatus("card", "ok", "Detected");
      break;
    case "cardRemoved":
      setStatus("card", "error", "Absent");
      break;
  }
});

// ── Status refresh ────────────────────────────────────────────────────────────

async function refresh() {
  setStatus("client", "loading");
  setStatus("reader", "idle");
  setStatus("card", "idle");

  let status;
  try {
    status = await chrome.runtime.sendMessage({ type: MSG.CHECK_STATUS });
  } catch {
    status = { bridgeActive: false, readerPresent: false, cardPresent: false };
  }

  if (!status?.bridgeActive) {
    setStatus("client", "error", "Bridge inactive");
    return;
  }

  setStatus("client", "ok", "Detected");
  setStatus(
    "reader",
    status.readerPresent ? "ok" : "error",
    status.readerPresent ? "Detected" : "Absent"
  );

  if (!status.readerPresent) return;

  setStatus(
    "card",
    status.cardPresent ? "ok" : "error",
    status.cardPresent ? "Detected" : "Absent"
  );
}

// ── UI helpers ────────────────────────────────────────────────────────────────

function setStatus(key, state, label) {
  const badge = document.getElementById(`badge-${key}`);
  const dot = document.getElementById(`dot-${key}`);
  const spin = document.getElementById(`spin-${key}`);
  const lbl = document.getElementById(`label-${key}`);

  badge.className = `badge ${state === "loading" ? "idle" : state}`;

  if (spin) spin.style.display = state === "loading" ? "" : "none";
  if (dot) {
    dot.style.display = state === "loading" ? "none" : "";
    dot.className = `dot ${state === "ok" ? "ok" : state === "error" ? "error" : "idle"}`;
  }

  if (label !== undefined) lbl.textContent = label;
  else if (state === "loading") lbl.textContent = "…";
}
