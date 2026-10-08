# Roadmap

| Phase | Scope | Status |
|---|---|---|
| 1. Foundations | Identity, Keystore, SQLCipher, domain, migrations | ✅ |
| 2. Pairing | Signed QR, Noise_IK, safety number | ✅ |
| 3. Signaling | WebSocket, presence, rotating topics, reconnection, limits | ✅ |
| 4. Real P2P | WebRTC DataChannel (binding of `stream-webrtc-android`), encrypted SDP | ⏳ next |
| 5. Messages | Noise_KK, outbox, ACK, idempotency, retries, Lamport, backpressure, states | ✅ on the simulated transport and the development relay |
| 6. Robustness | Background delivery and always-available mode ✅ · network changes, privacy-respecting P2P success metrics ⏳ | 🟡 |
| 7. Attachments | Chunking, resumption, integrity, limits | ⏳ |
| — | Independent security audit | ⏳ before any real-world use |

## Out of the MVP, on purpose

Groups · calls · stories · bots · cloud backups · full multi-device · self-hosted TURN ·
Bluetooth · Wi-Fi Direct · federation · global user directory.

## Open questions

- **How many pairs can't get a direct route?** That decides whether TURN gets in.
- **Push notifications on iOS:** iOS doesn't allow Android-style background work; a content-free push would wake the app, but APNs would see metadata. Needs its own ADR.
- **Recovery:** an encrypted, exportable local backup? Never a cloud backup with the key held by the provider.
