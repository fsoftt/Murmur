<script setup lang="ts">
import { useData, withBase } from 'vitepress'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import FeatureIcon from './FeatureIcon.vue'

type Action = { theme: 'brand' | 'alt'; text: string; link: string }
type Feature = { icon: string; eyebrow: string; title: string; details: string; link: string }
type Home = {
  eyebrow: string
  text: string
  tagline: string
  status: { label: string; text: string; link: string }
  actions: Action[]
  art: string
  cipher: { eyebrow: string; from: string; to: string; plain: string; caption: string }
  features: { eyebrow: string; title: string; items: Feature[] }
}

const { frontmatter } = useData()
const home = computed(() => frontmatter.value.murmur as Home)

const isExternal = (link: string) => /^https?:\/\//.test(link)
const href = (link: string) => (isExternal(link) ? link : withBase(link))

// "*word*" in the headline becomes an italic accent.
const headline = computed(() =>
  home.value.text.split(/(\*[^*]+\*)/).filter(Boolean).map((part) =>
    part.startsWith('*') ? { em: true, text: part.slice(1, -1) } : { em: false, text: part }))

// Decorative ciphertext: a fixed, base64-looking string the same length as the message,
// re-rolled a few characters at a time so it "murmurs". It is aria-hidden; the caption carries the meaning.
const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
function seeded(length: number) {
  let s = 0x2f6b5
  let out = ''
  for (let i = 0; i < length; i++) {
    s = (s * 48271) % 2147483647
    out += alphabet[(s >> 7) % alphabet.length]
  }
  return out
}
const cipher = ref(seeded(home.value.cipher.plain.length + 16))
const sealed = ref(0)
let timer: number | undefined

onMounted(() => {
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    // A still frame: half of the message sealed, nothing moving.
    sealed.value = Math.floor(home.value.cipher.plain.length / 2)
    return
  }
  const plain = home.value.cipher.plain
  let tick = 0
  timer = window.setInterval(() => {
    tick++
    // Seal the plaintext one character at a time, hold, then start over.
    const cycle = plain.length + 40
    const step = tick % cycle
    sealed.value = Math.min(step, plain.length)
    const chars = cipher.value.split('')
    for (let n = 0; n < 3; n++)
      chars[Math.floor(Math.random() * chars.length)] = alphabet[Math.floor(Math.random() * alphabet.length)]
    cipher.value = chars.join('')
  }, 90)
})

onBeforeUnmount(() => window.clearInterval(timer))

const plainView = computed(() => {
  const plain = home.value.cipher.plain
  return { open: plain.slice(sealed.value), sealed: cipher.value.slice(0, sealed.value) }
})
</script>

<template>
  <div class="MurmurHome">
    <section class="hero">
      <div class="hero-text">
        <p class="eyebrow">{{ home.eyebrow }}</p>
        <h1 class="headline">
          <template v-for="(part, i) in headline" :key="i">
            <em v-if="part.em">{{ part.text }}</em><template v-else>{{ part.text }}</template>
          </template>
        </h1>
        <p class="tagline">{{ home.tagline }}</p>
        <div class="actions">
          <a
            v-for="action in home.actions"
            :key="action.link"
            class="action"
            :class="action.theme"
            :href="href(action.link)"
            :target="isExternal(action.link) ? '_blank' : undefined"
            :rel="isExternal(action.link) ? 'noreferrer' : undefined"
          >
            {{ action.text }}
            <svg v-if="action.theme === 'brand'" viewBox="0 0 16 16" aria-hidden="true"><path d="M3 8h9M8.5 4.5 12 8l-3.5 3.5" /></svg>
            <svg v-else-if="isExternal(action.link)" viewBox="0 0 16 16" aria-hidden="true"><path d="M6 3h7v7M13 3 4 12" /></svg>
          </a>
        </div>
        <a class="status" :href="href(home.status.link)">
          <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M8 1.75 14.5 13.5h-13Z" /><path d="M8 6.25v3.25M8 11.4v.1" /></svg>
          <span class="status-label">{{ home.status.label }}</span>
          <span class="status-text">{{ home.status.text }}</span>
        </a>
      </div>

      <div class="hero-art">
        <svg class="art" viewBox="0 0 520 440" role="img" :aria-label="home.art">
          <defs>
            <radialGradient id="mm-glow" cx="50%" cy="50%" r="50%">
              <stop offset="0" stop-color="var(--mm-accent)" stop-opacity=".18" />
              <stop offset="1" stop-color="var(--mm-accent)" stop-opacity="0" />
            </radialGradient>
          </defs>
          <circle cx="260" cy="200" r="190" fill="url(#mm-glow)" />
          <g class="rings" fill="none" stroke="var(--mm-accent)">
            <circle class="ring r1" cx="260" cy="200" r="60" />
            <circle class="ring r2" cx="260" cy="200" r="60" />
            <circle class="ring r3" cx="260" cy="200" r="60" />
          </g>

          <!-- the dotted channel between the phones -->
          <path class="channel" d="M150 200 C 205 168, 315 168, 370 200" />

          <!-- phone A -->
          <g class="phone" transform="translate(54 96)">
            <rect width="100" height="196" rx="18" class="phone-body" />
            <rect x="8" y="10" width="84" height="176" rx="11" class="phone-screen" />
            <rect x="38" y="16" width="24" height="5" rx="2.5" class="phone-notch" />
            <rect x="16" y="40" width="48" height="14" rx="7" class="bubble in" />
            <rect x="34" y="62" width="50" height="14" rx="7" class="bubble out" />
            <rect x="16" y="84" width="38" height="14" rx="7" class="bubble in" />
            <rect x="28" y="106" width="56" height="14" rx="7" class="bubble out" />
          </g>

          <!-- phone B -->
          <g class="phone" transform="translate(366 96)">
            <rect width="100" height="196" rx="18" class="phone-body" />
            <rect x="8" y="10" width="84" height="176" rx="11" class="phone-screen" />
            <rect x="38" y="16" width="24" height="5" rx="2.5" class="phone-notch" />
            <rect x="34" y="40" width="50" height="14" rx="7" class="bubble out" />
            <rect x="16" y="62" width="48" height="14" rx="7" class="bubble in" />
            <rect x="34" y="84" width="40" height="14" rx="7" class="bubble out" />
            <rect x="16" y="106" width="56" height="14" rx="7" class="bubble in" />
          </g>

          <!-- lock on the line -->
          <g class="lock" transform="translate(260 176)">
            <circle r="24" class="lock-halo" />
            <path d="M-7 -3v-5a7 7 0 0 1 14 0v5" class="lock-shackle" />
            <rect x="-10" y="-3" width="20" height="15" rx="3" class="lock-body" />
            <circle cy="4" r="1.8" class="lock-hole" />
          </g>

          <!-- QR-like pairing tile -->
          <g class="qr" transform="translate(212 300)">
            <rect width="96" height="96" rx="14" class="qr-tile" />
            <g class="qr-marks">
              <rect x="14" y="14" width="22" height="22" rx="4" class="qr-finder" />
              <rect x="60" y="14" width="22" height="22" rx="4" class="qr-finder" />
              <rect x="14" y="60" width="22" height="22" rx="4" class="qr-finder" />
              <rect x="20" y="20" width="10" height="10" rx="1.5" class="qr-dot" />
              <rect x="66" y="20" width="10" height="10" rx="1.5" class="qr-dot" />
              <rect x="20" y="66" width="10" height="10" rx="1.5" class="qr-dot" />
              <rect x="44" y="16" width="6" height="6" class="qr-dot" />
              <rect x="44" y="30" width="6" height="6" class="qr-dot" />
              <rect x="44" y="44" width="6" height="6" class="qr-dot" />
              <rect x="16" y="44" width="6" height="6" class="qr-dot" />
              <rect x="30" y="44" width="6" height="6" class="qr-dot" />
              <rect x="58" y="44" width="6" height="6" class="qr-dot" />
              <rect x="72" y="44" width="6" height="6" class="qr-dot" />
              <rect x="58" y="58" width="6" height="6" class="qr-dot" />
              <rect x="76" y="62" width="6" height="6" class="qr-dot" />
              <rect x="44" y="62" width="6" height="6" class="qr-dot" />
              <rect x="62" y="72" width="6" height="6" class="qr-dot" />
              <rect x="44" y="76" width="6" height="6" class="qr-dot" />
              <rect x="74" y="76" width="6" height="6" class="qr-dot" />
            </g>
            <path class="qr-scan" d="M6 48h84" />
          </g>
          <path class="tether" d="M104 292 C 110 340, 160 350, 212 348" />
          <path class="tether" d="M416 292 C 410 340, 360 350, 308 348" />
        </svg>
      </div>
    </section>

    <section class="cipher" :aria-labelledby="'mm-cipher-caption'">
      <div class="cipher-inner">
        <p class="eyebrow">{{ home.cipher.eyebrow }}</p>
        <div class="cipher-line" aria-hidden="true">
          <span class="endpoint">{{ home.cipher.from }}</span>
          <span class="stream">
            <span class="sealed">{{ plainView.sealed }}</span><span class="open">{{ plainView.open }}</span>
          </span>
          <span class="endpoint">{{ home.cipher.to }}</span>
        </div>
        <div class="cipher-wire" aria-hidden="true">
          <svg viewBox="0 0 16 16"><rect x="3.5" y="7" width="9" height="7" rx="1.5" /><path d="M5.5 7V5a2.5 2.5 0 0 1 5 0v2" /></svg>
          <span class="wire-text">{{ cipher }}</span>
        </div>
        <p id="mm-cipher-caption" class="cipher-caption">{{ home.cipher.caption }}</p>
      </div>
    </section>

    <section class="features" aria-labelledby="mm-features-title">
      <header class="features-head">
        <p class="eyebrow">{{ home.features.eyebrow }}</p>
        <h2 id="mm-features-title" class="features-title">{{ home.features.title }}</h2>
      </header>
      <ul class="cards">
        <li v-for="(f, i) in home.features.items" :key="f.link">
          <a class="card" :href="href(f.link)">
            <span class="card-top">
              <FeatureIcon :name="f.icon" />
              <span class="card-index" aria-hidden="true">0{{ i + 1 }}</span>
            </span>
            <span class="card-eyebrow">{{ f.eyebrow }}</span>
            <span class="card-title">{{ f.title }}</span>
            <span class="card-details">{{ f.details }}</span>
            <span class="card-more" aria-hidden="true">
              <svg viewBox="0 0 16 16"><path d="M3 8h9M8.5 4.5 12 8l-3.5 3.5" /></svg>
            </span>
          </a>
        </li>
      </ul>
    </section>
  </div>
</template>

<style scoped>
.MurmurHome {
  --mm-pad: 24px;
  position: relative;
  overflow: hidden;
}

.MurmurHome::before {
  content: '';
  position: absolute;
  inset: 0 0 auto;
  height: 900px;
  pointer-events: none;
  background:
    radial-gradient(60% 50% at 75% 20%, var(--mm-glow), transparent 70%),
    radial-gradient(1px 1px at 1px 1px, var(--mm-grid) 1px, transparent 0) 0 0 / 22px 22px;
  mask-image: linear-gradient(to bottom, #000 30%, transparent);
  -webkit-mask-image: linear-gradient(to bottom, #000 30%, transparent);
}

section {
  position: relative;
}

.eyebrow {
  margin: 0;
  font-family: var(--vp-font-family-mono);
  font-size: 12px;
  font-weight: 500;
  letter-spacing: 0.14em;
  text-transform: uppercase;
  color: var(--mm-accent-text);
}

/* ---------- hero ---------- */

.hero {
  display: grid;
  gap: 24px;
  max-width: 1280px;
  margin: 0 auto;
  padding: 40px var(--mm-pad) 32px;
}

.headline {
  margin: 20px 0 0;
  font-family: var(--mm-font-display);
  font-weight: 400;
  font-size: clamp(2.6rem, 9vw, 4.6rem);
  line-height: 1.02;
  letter-spacing: -0.015em;
  color: var(--vp-c-text-1);
  text-wrap: balance;
}

.headline em {
  font-style: italic;
  color: var(--mm-accent-text);
}

.tagline {
  margin: 24px 0 0;
  max-width: 34rem;
  font-size: 1.075rem;
  line-height: 1.7;
  color: var(--vp-c-text-2);
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-top: 32px;
}

.action {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: 48px;
  padding: 0 20px;
  border: 1px solid var(--vp-c-divider);
  border-radius: 999px;
  font-size: 15px;
  font-weight: 600;
  color: var(--vp-c-text-1);
  text-decoration: none;
  transition: border-color 0.2s, background-color 0.2s, color 0.2s;
}

.action svg {
  width: 16px;
  height: 16px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.6;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.action:hover {
  border-color: var(--mm-accent-text);
}

.action.brand {
  border-color: transparent;
  background: var(--vp-button-brand-bg);
  color: var(--vp-button-brand-text);
}

.action.brand:hover {
  background: var(--vp-button-brand-hover-bg);
}

.action:focus-visible,
.status:focus-visible,
.card:focus-visible {
  outline: 2px solid var(--mm-accent-text);
  outline-offset: 3px;
}

.status {
  display: inline-flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px 10px;
  margin-top: 28px;
  padding: 10px 14px;
  border: 1px solid var(--vp-c-divider);
  border-left: 3px solid var(--vp-c-text-1);
  border-radius: 8px;
  background: var(--vp-c-bg-soft);
  font-size: 14px;
  line-height: 1.5;
  color: var(--vp-c-text-2);
  text-decoration: none;
}

.status svg {
  flex: none;
  width: 16px;
  height: 16px;
  fill: none;
  stroke: var(--vp-c-text-1);
  stroke-width: 1.5;
  stroke-linejoin: round;
  stroke-linecap: round;
}

.status-label {
  font-family: var(--vp-font-family-mono);
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--vp-c-text-1);
}

.status:hover .status-text {
  color: var(--vp-c-text-1);
  text-decoration: underline;
  text-underline-offset: 3px;
}

.hero-art {
  display: flex;
  justify-content: center;
}

.art {
  width: 100%;
  max-width: 520px;
  height: auto;
  overflow: visible;
}

.ring {
  stroke-width: 1;
  transform-box: fill-box;
  transform-origin: center;
  opacity: 0;
  animation: murmur 6s cubic-bezier(0.2, 0.6, 0.3, 1) infinite;
}

.r2 { animation-delay: 2s; }
.r3 { animation-delay: 4s; }

@keyframes murmur {
  0% { transform: scale(0.6); opacity: 0; }
  15% { opacity: 0.55; }
  100% { transform: scale(3); opacity: 0; }
}

.channel {
  fill: none;
  stroke: var(--mm-accent);
  stroke-width: 2.5;
  stroke-linecap: round;
  stroke-dasharray: 0.5 11;
  animation: flow 2.4s linear infinite;
}

@keyframes flow {
  to { stroke-dashoffset: -23; }
}

.tether {
  fill: none;
  stroke: var(--mm-line-strong);
  stroke-width: 1.25;
  stroke-dasharray: 2 6;
  stroke-linecap: round;
}

.phone-body { fill: var(--mm-surface-2); stroke: var(--mm-line-strong); stroke-width: 1.25; }
.phone-screen { fill: var(--vp-c-bg); stroke: var(--vp-c-divider); }
.phone-notch { fill: var(--mm-line-strong); }
.bubble.in { fill: var(--mm-surface-2); stroke: var(--mm-line-strong); }
.bubble.out { fill: var(--mm-accent); opacity: 0.85; }

.lock-halo { fill: var(--vp-c-bg); stroke: var(--mm-accent); stroke-width: 1.25; }
.lock-shackle { fill: none; stroke: var(--mm-accent); stroke-width: 2; stroke-linecap: round; }
.lock-body { fill: var(--mm-accent); }
.lock-hole { fill: var(--vp-c-bg); }

.qr-tile { fill: var(--mm-surface-1); stroke: var(--mm-line-strong); stroke-width: 1.25; }
.qr-finder { fill: none; stroke: var(--vp-c-text-1); stroke-width: 2.5; }
.qr-dot { fill: var(--vp-c-text-1); }
.qr-scan {
  stroke: var(--mm-accent);
  stroke-width: 2;
  stroke-linecap: round;
  animation: scan 4s ease-in-out infinite;
}

@keyframes scan {
  0%, 100% { transform: translateY(-30px); opacity: 0; }
  20%, 80% { opacity: 0.9; }
  50% { transform: translateY(30px); }
}

/* ---------- ciphertext strip ---------- */

.cipher {
  margin-top: 24px;
  border-block: 1px solid var(--vp-c-divider);
  background: var(--mm-surface-1);
}

.cipher-inner {
  max-width: 1280px;
  margin: 0 auto;
  padding: 32px var(--mm-pad);
}

.cipher-line {
  display: grid;
  grid-template-columns: auto 1fr auto;
  align-items: baseline;
  gap: 12px;
  margin-top: 18px;
  font-family: var(--vp-font-family-mono);
}

.endpoint {
  font-size: 12px;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--vp-c-text-2);
}

.stream {
  min-width: 0;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: clip;
  font-family: var(--mm-font-display);
  font-size: clamp(1.35rem, 4.5vw, 2.1rem);
  line-height: 1.3;
  text-align: center;
}

.stream .open {
  font-style: italic;
  color: var(--vp-c-text-1);
}

.stream .sealed {
  font-family: var(--vp-font-family-mono);
  font-size: 0.72em;
  letter-spacing: 0.02em;
  color: var(--mm-accent-text);
}

.stream .sealed:not(:empty) {
  margin-right: 0.3em;
}

.cipher-wire {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-top: 14px;
  padding-top: 14px;
  border-top: 1px dashed var(--vp-c-divider);
  font-family: var(--vp-font-family-mono);
  font-size: 13px;
  color: var(--vp-c-text-2);
}

.cipher-wire svg {
  flex: none;
  width: 16px;
  height: 16px;
  fill: none;
  stroke: var(--mm-accent-text);
  stroke-width: 1.4;
  stroke-linecap: round;
}

.wire-text {
  min-width: 0;
  overflow: hidden;
  white-space: nowrap;
  letter-spacing: 0.08em;
}

.cipher-caption {
  margin: 14px 0 0;
  font-size: 15px;
  color: var(--vp-c-text-2);
}

/* ---------- features ---------- */

.features {
  max-width: 1280px;
  margin: 0 auto;
  padding: 72px var(--mm-pad) 8px;
}

.features-title {
  margin: 12px 0 0;
  max-width: 40rem;
  font-family: var(--mm-font-display);
  font-weight: 400;
  font-size: clamp(2rem, 6vw, 2.9rem);
  line-height: 1.08;
  letter-spacing: -0.01em;
  color: var(--vp-c-text-1);
  text-wrap: balance;
}

.cards {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1px;
  margin: 36px 0 0;
  padding: 0;
  list-style: none;
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  overflow: hidden;
  background: var(--vp-c-divider);
}

.card {
  position: relative;
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 26px 24px 28px;
  background: var(--vp-c-bg);
  color: inherit;
  text-decoration: none;
  transition: background-color 0.25s;
}

.card:hover {
  background: var(--mm-surface-1);
}

.card-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  color: var(--mm-accent-text);
}

.card-index {
  font-family: var(--vp-font-family-mono);
  font-size: 12px;
  color: var(--vp-c-text-3);
}

.card-eyebrow {
  margin-top: 26px;
  font-family: var(--vp-font-family-mono);
  font-size: 11px;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: var(--vp-c-text-2);
}

.card-title {
  margin-top: 8px;
  font-family: var(--mm-font-display);
  font-size: 1.65rem;
  line-height: 1.15;
  color: var(--vp-c-text-1);
}

.card-details {
  margin-top: 10px;
  font-size: 15px;
  line-height: 1.65;
  color: var(--vp-c-text-2);
}

.card-more {
  margin-top: auto;
  padding-top: 18px;
  color: var(--vp-c-text-3);
  transition: color 0.2s, transform 0.2s;
}

.card-more svg {
  width: 18px;
  height: 18px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.5;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.card:hover .card-more {
  color: var(--mm-accent-text);
  transform: translateX(4px);
}

@media (min-width: 640px) {
  .MurmurHome { --mm-pad: 48px; }
  .cards { grid-template-columns: repeat(2, 1fr); }
}

@media (min-width: 960px) {
  .MurmurHome { --mm-pad: 64px; }
  .hero {
    grid-template-columns: 1.1fr 0.9fr;
    align-items: center;
    gap: 48px;
    padding-top: 72px;
    padding-bottom: 56px;
  }
  .cards { grid-template-columns: repeat(3, 1fr); }
}

@media (max-width: 639px) {
  .cipher-line {
    grid-template-columns: 1fr 1fr;
  }
  .stream {
    grid-column: 1 / -1;
    grid-row: 2;
    text-align: left;
  }
  .endpoint:last-child { text-align: right; }
}

@media (prefers-reduced-motion: reduce) {
  .ring,
  .channel,
  .qr-scan {
    animation: none;
  }
  .ring { opacity: 0.35; }
  .r1 { transform: scale(1.2); }
  .r2 { transform: scale(1.8); opacity: 0.22; }
  .r3 { transform: scale(2.4); opacity: 0.12; }
  .qr-scan { opacity: 0; }
  .card-more,
  .card,
  .action { transition: none; }
}
</style>
