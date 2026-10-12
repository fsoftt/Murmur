<script setup lang="ts">
import { useData } from 'vitepress'
import { onMounted, ref, watch } from 'vue'

const props = defineProps<{ code: string }>()
const { isDark } = useData()
const svg = ref('')
let counter = 0

// Diagram colors follow the site palette: graphite and sage at night, paper and ink by day.
const palettes = {
  dark: {
    background: '#0F1211', primaryColor: '#1C2120', primaryTextColor: '#E8ECE9', primaryBorderColor: '#5E8C74',
    secondaryColor: '#151918', tertiaryColor: '#151918', lineColor: '#9CC3AC', textColor: '#E8ECE9',
    mainBkg: '#1C2120', nodeBorder: '#5E8C74', clusterBkg: '#151918', clusterBorder: '#2A302E',
    edgeLabelBackground: '#151918', actorBkg: '#1C2120', actorBorder: '#5E8C74', actorTextColor: '#E8ECE9',
    actorLineColor: '#2A302E', signalColor: '#9CC3AC', signalTextColor: '#E8ECE9', labelBoxBkgColor: '#1C2120',
    labelBoxBorderColor: '#5E8C74', labelTextColor: '#E8ECE9', loopTextColor: '#E8ECE9', noteBkgColor: '#151918',
    noteBorderColor: '#5E8C74', noteTextColor: '#E8ECE9', activationBkgColor: '#2A302E', activationBorderColor: '#9CC3AC',
    sequenceNumberColor: '#0F1211', stateBkg: '#1C2120', transitionColor: '#9CC3AC', compositeBackground: '#151918',
  },
  light: {
    background: '#F2F1EC', primaryColor: '#FFFFFF', primaryTextColor: '#141816', primaryBorderColor: '#3D6B55',
    secondaryColor: '#F8F7F3', tertiaryColor: '#F8F7F3', lineColor: '#3D6B55', textColor: '#141816',
    mainBkg: '#FFFFFF', nodeBorder: '#3D6B55', clusterBkg: '#F8F7F3', clusterBorder: '#DCDDD6',
    edgeLabelBackground: '#F8F7F3', actorBkg: '#FFFFFF', actorBorder: '#3D6B55', actorTextColor: '#141816',
    actorLineColor: '#DCDDD6', signalColor: '#3D6B55', signalTextColor: '#141816', labelBoxBkgColor: '#FFFFFF',
    labelBoxBorderColor: '#3D6B55', labelTextColor: '#141816', loopTextColor: '#141816', noteBkgColor: '#F8F7F3',
    noteBorderColor: '#3D6B55', noteTextColor: '#141816', activationBkgColor: '#DCDDD6', activationBorderColor: '#3D6B55',
    sequenceNumberColor: '#FFFFFF', stateBkg: '#FFFFFF', transitionColor: '#3D6B55', compositeBackground: '#F8F7F3',
  },
}

async function render() {
  // Loaded lazily in the browser only: keeps the static build light and SSR-safe.
  const { default: mermaid } = await import('mermaid')
  // Measure labels with the final web font, otherwise long labels get clipped.
  await document.fonts.ready
  mermaid.initialize({
    startOnLoad: false,
    theme: 'base',
    securityLevel: 'strict',
    fontFamily: "'Manrope', ui-sans-serif, system-ui, sans-serif",
    themeVariables: {
      ...(isDark.value ? palettes.dark : palettes.light),
      darkMode: isDark.value,
      fontFamily: "'Manrope', ui-sans-serif, system-ui, sans-serif",
    },
  })
  const id = `mermaid-${Math.random().toString(36).slice(2)}-${counter++}`
  svg.value = (await mermaid.render(id, decodeURIComponent(props.code))).svg
}

onMounted(render)
watch(isDark, render)
</script>

<template>
  <div class="mermaid-diagram" v-html="svg" />
</template>
