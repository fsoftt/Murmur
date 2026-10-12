import DefaultTheme from 'vitepress/theme'
import type { Theme } from 'vitepress'
import { useData } from 'vitepress'
import { h } from 'vue'
import Mermaid from './Mermaid.vue'
import MurmurHome from './MurmurHome.vue'
import './custom.css'

export default {
  extends: DefaultTheme,
  // Home pages describe their hero, cipher strip and cards under `murmur:` in the frontmatter.
  Layout() {
    const { frontmatter } = useData()
    return h(DefaultTheme.Layout, null, {
      'home-hero-before': () => (frontmatter.value.murmur ? h(MurmurHome) : null),
    })
  },
  enhanceApp({ app }) {
    app.component('Mermaid', Mermaid)
  },
} satisfies Theme
