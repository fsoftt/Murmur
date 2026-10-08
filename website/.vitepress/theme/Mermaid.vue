<script setup lang="ts">
import { useData } from 'vitepress'
import { onMounted, ref, watch } from 'vue'

const props = defineProps<{ code: string }>()
const { isDark } = useData()
const svg = ref('')
let counter = 0

async function render() {
  // Loaded lazily in the browser only: keeps the static build light and SSR-safe.
  const { default: mermaid } = await import('mermaid')
  // Measure labels with the final web font, otherwise long labels get clipped.
  await document.fonts.ready
  mermaid.initialize({ startOnLoad: false, theme: isDark.value ? 'dark' : 'default', securityLevel: 'strict' })
  const id = `mermaid-${Math.random().toString(36).slice(2)}-${counter++}`
  svg.value = (await mermaid.render(id, decodeURIComponent(props.code))).svg
}

onMounted(render)
watch(isDark, render)
</script>

<template>
  <div class="mermaid-diagram" v-html="svg" />
</template>
