# Safety number

## What it is for

If someone intercepted the QR code and paired with you while pretending to be your contact,
everything would work, but with **their** key. The safety number detects this: it is 60 digits
computed from the two identity keys, and **both phones must show the same digits**.

```text
12345 67890 13579 24680 11223 34455
66778 89900 10293 84756 56473 82910
```

## How it is computed

It follows the design of Signal's *safety number*:

1. For each identity key: `SHA-512(version ‖ key ‖ "Murmur")`, then `SHA-512(hash ‖ key)` repeated
   **5,200** times. The iterations make it more expensive to search for a fake key with a similar
   number.
2. 30 bytes are taken, in 6 blocks of 5, and each block becomes 5 digits → 30 digits per person.
3. The two halves are concatenated **in a fixed order** so that both phones show the same thing.

## How to use it

Compare it once, in person or over a trusted channel, and mark the contact as **verified**. Until
you do, the app points this out in the chat.

**In the code:** [`SafetyNumber.cs`](https://github.com/fsoftt/Murmur/blob/main/src/Murmur.Security/Identity/SafetyNumber.cs) ·
[`ContactDetailsViewModel.cs`](https://github.com/fsoftt/Murmur/blob/main/src/Murmur.Presentation/ViewModels/ContactDetailsViewModel.cs)
