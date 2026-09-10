# 0003: Offline sync engine scope boundary

**Status:** Accepted

**Decision:** The full multi-device conflict-resolution sync engine (background isolate, batched `/sync`, later-timestamp-auto-cancel rule) is built in Phase 6, primarily to serve Cloud-mode branches that can lose connectivity to the cloud for extended periods.

**Rationale:** Local/dedicated-mode devices talk to a server on the same LAN, which stays reachable even during an internet outage (only Xendit-dependent QR Ph payments are affected). The hard multi-day, multi-device conflict scenario is therefore mainly a Cloud-mode concern. The client-side `pending_sync_queue` write-first pattern is still built universally as defense-in-depth (a device's own Wi-Fi can drop independently of the LAN server's health).

**Boundary:** Local mode does not require the full conflict-resolution engine to be exercised in normal operation; it's still available since the queue mechanism is shared code, not mode-specific.
