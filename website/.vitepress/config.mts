import { defineConfig } from 'vitepress'

const repo = 'https://github.com/fsoftt/Murmur'

type Sidebar = { text: string; items: { text: string; link: string }[] }[]

function sidebar(prefix: string, t: Record<string, string>): Sidebar {
  const g = (path: string) => ({ text: t[path], link: `${prefix}/guide/${path}` })
  const c = (path: string) => ({ text: t[path || 'map'], link: `${prefix}/concepts/${path}` })
  return [
    {
      text: t.project,
      items: ['overview', 'architecture', 'life-of-a-message', 'pairing', 'privacy', 'how-it-was-built',
        'testing', 'decisions', 'roadmap', 'run-locally'].map(g),
    },
    {
      text: t.concepts,
      items: ['', 'keys-and-signatures', 'noise', 'authenticated-encryption', 'wire-formats', 'rendezvous',
        'nat-and-p2p', 'outbox-and-acks', 'lamport-clocks', 'state-machines', 'backpressure',
        'encrypted-storage', 'safety-number', 'test-vectors'].map(c),
    },
  ]
}

const en = {
  project: 'The project', concepts: 'Concepts', overview: 'What Murmur is', architecture: 'Architecture',
  'life-of-a-message': 'Life of a message', pairing: 'QR pairing', privacy: 'Privacy and threats',
  'how-it-was-built': 'How it was built', testing: 'Tests and CI', decisions: 'Design decisions',
  roadmap: 'Roadmap', 'run-locally': 'Run it', map: 'Concept map',
  'keys-and-signatures': 'Keys, signatures and Diffie-Hellman', noise: 'Noise and forward secrecy',
  'authenticated-encryption': 'Authenticated encryption and nonces', 'wire-formats': 'Wire formats',
  rendezvous: 'Signaling and rendezvous topics', 'nat-and-p2p': 'NAT, STUN, ICE, TURN and WebRTC',
  'outbox-and-acks': 'Outbox, ACKs and idempotency', 'lamport-clocks': 'Lamport clocks',
  'state-machines': 'State machines', backpressure: 'Backpressure and token buckets',
  'encrypted-storage': 'Encrypted storage', 'safety-number': 'Safety number', 'test-vectors': 'Test vectors',
}

const es = {
  project: 'El proyecto', concepts: 'Conceptos', overview: 'Qué es Murmur', architecture: 'Arquitectura',
  'life-of-a-message': 'La vida de un mensaje', pairing: 'Emparejamiento por QR', privacy: 'Privacidad y amenazas',
  'how-it-was-built': 'Cómo se construyó', testing: 'Pruebas y CI', decisions: 'Decisiones de diseño',
  roadmap: 'Hoja de ruta', 'run-locally': 'Ejecutarlo', map: 'Mapa de conceptos',
  'keys-and-signatures': 'Claves, firmas y Diffie-Hellman', noise: 'Noise y forward secrecy',
  'authenticated-encryption': 'Cifrado autenticado y nonces', 'wire-formats': 'Formatos de cable',
  rendezvous: 'Signaling y temas de encuentro', 'nat-and-p2p': 'NAT, STUN, ICE, TURN y WebRTC',
  'outbox-and-acks': 'Outbox, ACK e idempotencia', 'lamport-clocks': 'Relojes de Lamport',
  'state-machines': 'Máquinas de estados', backpressure: 'Contrapresión y token bucket',
  'encrypted-storage': 'Almacenamiento cifrado', 'safety-number': 'Código de seguridad', 'test-vectors': 'Vectores de prueba',
}

export default defineConfig({
  title: 'Murmur',
  base: '/Murmur/',
  cleanUrls: true,
  ignoreDeadLinks: 'localhostLinks',
  lastUpdated: true,
  // Mermaid is large but loaded lazily, only on pages that contain diagrams.
  vite: { build: { chunkSizeWarningLimit: 3000 } },
  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: '/Murmur/favicon.svg' }],
    ['meta', { property: 'og:title', content: 'Murmur: private peer-to-peer messaging' }],
    ['meta', { property: 'og:description', content: 'No accounts, no servers that keep messages. Noise, SQLCipher, QR and .NET MAUI.' }],
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

  locales: {
    root: {
      label: 'English',
      lang: 'en',
      description: 'Private, local-first, end-to-end encrypted, peer-to-peer messaging built with .NET 10 and .NET MAUI: what it is, how it works and how it was built.',
      themeConfig: {
        nav: [
          { text: 'What it is', link: '/guide/overview' },
          { text: 'Architecture', link: '/guide/architecture' },
          { text: 'Concepts', link: '/concepts/' },
          { text: 'How it was built', link: '/guide/how-it-was-built' },
          { text: 'Code', link: repo },
        ],
        sidebar: sidebar('', en),
        editLink: { pattern: `${repo}/edit/main/website/:path`, text: 'Edit this page on GitHub' },
        footer: {
          message: 'Code under AGPL-3.0 · Documentation under CC BY 4.0',
          copyright: 'Murmur: private, local-first, peer-to-peer messaging',
        },
      },
    },
    es: {
      label: 'Español',
      lang: 'es',
      link: '/es/',
      description: 'Mensajería privada local-first, cifrada de extremo a extremo y peer-to-peer, construida con .NET 10 y .NET MAUI: qué es, cómo funciona y cómo se construyó.',
      themeConfig: {
        nav: [
          { text: 'Qué es', link: '/es/guide/overview' },
          { text: 'Arquitectura', link: '/es/guide/architecture' },
          { text: 'Conceptos', link: '/es/concepts/' },
          { text: 'Cómo se construyó', link: '/es/guide/how-it-was-built' },
          { text: 'Código', link: repo },
        ],
        sidebar: sidebar('/es', es),
        editLink: { pattern: `${repo}/edit/main/website/:path`, text: 'Editar esta página en GitHub' },
        outline: { level: [2, 3], label: 'En esta página' },
        docFooter: { prev: 'Anterior', next: 'Siguiente' },
        lastUpdated: { text: 'Actualizado' },
        returnToTopLabel: 'Volver arriba',
        sidebarMenuLabel: 'Menú',
        darkModeSwitchLabel: 'Apariencia',
        langMenuLabel: 'Cambiar idioma',
        footer: {
          message: 'Código bajo AGPL-3.0 · Documentación bajo CC BY 4.0',
          copyright: 'Murmur: mensajería privada, local-first y peer-to-peer',
        },
      },
    },
  },

  themeConfig: {
    logo: '/favicon.svg',
    socialLinks: [{ icon: 'github', link: repo }],
    outline: { level: [2, 3] },
    search: {
      provider: 'local',
      options: {
        locales: {
          es: {
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
      },
    },
  },
})
