import { defineConfig } from 'vitepress'

const repo = 'https://github.com/fsoftt/Directo'

export default defineConfig({
  lang: 'es',
  title: 'Directo',
  description: 'Mensajería privada local-first, cifrada de extremo a extremo y peer-to-peer, construida con .NET 10 y .NET MAUI: qué es, cómo funciona y cómo se construyó.',
  base: '/Directo/',
  cleanUrls: true,
  ignoreDeadLinks: 'localhostLinks',
  lastUpdated: true,
  // Mermaid is large but loaded lazily, only on pages that contain diagrams.
  vite: { build: { chunkSizeWarningLimit: 3000 } },
  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: '/Directo/favicon.svg' }],
    ['meta', { property: 'og:title', content: 'Directo: mensajería privada peer-to-peer' }],
    ['meta', { property: 'og:description', content: 'Sin cuentas ni servidores que guarden mensajes. Noise, SQLCipher, QR y .NET MAUI.' }],
  ],

  markdown: {
    // ```mermaid fences are rendered in the browser by the <Mermaid> component.
    config(md) {
      const fence = md.renderer.rules.fence!
      md.renderer.rules.fence = (tokens, idx, options, env, self) => {
        const token = tokens[idx]
        if (token.info.trim() === 'mermaid')
          return `<Mermaid code="${encodeURIComponent(token.content)}" />`
        return fence(tokens, idx, options, env, self)
      }
    },
  },

  themeConfig: {
    logo: '/favicon.svg',
    nav: [
      { text: 'Qué es', link: '/guide/overview' },
      { text: 'Arquitectura', link: '/guide/architecture' },
      { text: 'Conceptos', link: '/concepts/' },
      { text: 'Cómo se construyó', link: '/guide/how-it-was-built' },
      { text: 'Código', link: repo },
    ],
    sidebar: [
      {
        text: 'El proyecto',
        items: [
          { text: 'Qué es Directo', link: '/guide/overview' },
          { text: 'Arquitectura', link: '/guide/architecture' },
          { text: 'La vida de un mensaje', link: '/guide/life-of-a-message' },
          { text: 'Emparejamiento por QR', link: '/guide/pairing' },
          { text: 'Privacidad y amenazas', link: '/guide/privacy' },
          { text: 'Cómo se construyó', link: '/guide/how-it-was-built' },
          { text: 'Pruebas y CI', link: '/guide/testing' },
          { text: 'Decisiones de diseño', link: '/guide/decisions' },
          { text: 'Hoja de ruta', link: '/guide/roadmap' },
          { text: 'Ejecutarlo', link: '/guide/run-locally' },
        ],
      },
      {
        text: 'Conceptos',
        items: [
          { text: 'Mapa de conceptos', link: '/concepts/' },
          { text: 'Claves, firmas y Diffie-Hellman', link: '/concepts/keys-and-signatures' },
          { text: 'Noise y forward secrecy', link: '/concepts/noise' },
          { text: 'Cifrado autenticado y nonces', link: '/concepts/authenticated-encryption' },
          { text: 'Formatos de cable', link: '/concepts/wire-formats' },
          { text: 'Signaling y temas de encuentro', link: '/concepts/rendezvous' },
          { text: 'NAT, STUN, ICE, TURN y WebRTC', link: '/concepts/nat-and-p2p' },
          { text: 'Outbox, ACK e idempotencia', link: '/concepts/outbox-and-acks' },
          { text: 'Relojes de Lamport', link: '/concepts/lamport-clocks' },
          { text: 'Máquinas de estados', link: '/concepts/state-machines' },
          { text: 'Contrapresión y token bucket', link: '/concepts/backpressure' },
          { text: 'Almacenamiento cifrado', link: '/concepts/encrypted-storage' },
          { text: 'Código de seguridad', link: '/concepts/safety-number' },
          { text: 'Vectores de prueba', link: '/concepts/test-vectors' },
        ],
      },
    ],
    socialLinks: [{ icon: 'github', link: repo }],
    editLink: { pattern: `${repo}/edit/main/website/:path`, text: 'Editar esta página en GitHub' },
    search: {
      provider: 'local',
      options: {
        translations: {
          button: { buttonText: 'Buscar', buttonAriaLabel: 'Buscar' },
          modal: {
            noResultsText: 'Sin resultados para',
            resetButtonTitle: 'Borrar búsqueda',
            footer: { selectText: 'abrir', navigateText: 'navegar', closeText: 'cerrar' },
          },
        },
      },
    },
    outline: { level: [2, 3], label: 'En esta página' },
    docFooter: { prev: 'Anterior', next: 'Siguiente' },
    lastUpdated: { text: 'Actualizado' },
    returnToTopLabel: 'Volver arriba',
    sidebarMenuLabel: 'Menú',
    darkModeSwitchLabel: 'Apariencia',
    footer: {
      message: 'Código bajo AGPL-3.0 · Documentación bajo CC BY 4.0',
      copyright: 'Directo: mensajería privada, local-first y peer-to-peer',
    },
  },
})
