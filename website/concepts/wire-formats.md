# Wire formats

The **wire format** is how data is turned into bytes to travel over the network, and how it is
rebuilt on the other side. If two programs do not agree on it exactly, they cannot understand each
other. That is why Murmur has a
[public specification](https://github.com/fsoftt/Murmur/blob/main/docs/protocol/spec.md), independent of the code: with it, someone
could write another compatible client in Kotlin or Swift.

## Two formats, two uses

| | JSON | CBOR |
|---|---|---|
| Where | Phone ↔ signaling server | Phone ↔ phone (inside Noise) |
| Why | Readable and easy to debug; the server only sees envelopes | Binary, compact, strict and typed (bytes, integers) |

### JSON (signaling)

```json
{"t":"sub","topics":["q3Jk0…"]}
{"t":"presence","topic":"q3Jk0…","peers":1}
```

### CBOR (messages)

CBOR is "binary JSON". Instead of field names we use **small numbers**:

```text
{ 0: 1,                 ← type: chat message
  1: h'01a1…',          ← id (16 bytes)
  2: 42,                ← Lamport clock
  3: 1791500000000,     ← time in milliseconds
  4: "Hello" }          ← body
```

## The rules that make it robust

1. **Limits before parsing.** A frame cannot exceed 60 KiB, nor a message 16 KiB. This is checked
   before any memory is allocated.
2. **Strict.** Duplicate keys, trailing bytes, indefinite lengths and invalid UTF-8 are rejected.
3. **Canonical when writing.** The same data always produces the same bytes.
4. **Ignore the unknown.** A field `5` added in a future version does not break older clients; an
   unknown frame type is ignored. This lets the protocol evolve.
5. **Everything that comes from the network is hostile.** The tests feed the parsers 20,000 random
   inputs or inputs with a flipped bit: the only possible outcome is a controlled protocol error.

## Versions

Identity cards and invites carry their own version, the handshake negotiates the protocol version,
and the QR prefix (`MURMUR1:`) identifies the format. An incompatible change requires a new version
and a new prologue.

**In the code:** [`PeerFrameCodec.cs`](https://github.com/fsoftt/Murmur/blob/main/src/Murmur.Protocol/Frames/PeerFrameCodec.cs) ·
[`CborMap.cs`](https://github.com/fsoftt/Murmur/blob/main/src/Murmur.Protocol/Serialization/CborMap.cs) ·
[`SignalingMessages.cs`](https://github.com/fsoftt/Murmur/blob/main/src/Murmur.Protocol/Signaling/SignalingMessages.cs) ·
[`ProtocolConstants.cs`](https://github.com/fsoftt/Murmur/blob/main/src/Murmur.Protocol/ProtocolConstants.cs)
