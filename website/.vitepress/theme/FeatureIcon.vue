<script setup lang="ts">
defineProps<{ name: string }>()
</script>

<!-- Thin line icons for the home cards. Decorative: the card title carries the meaning. -->
<template>
  <svg class="feature-icon" viewBox="0 0 32 32" aria-hidden="true">
    <template v-if="name === 'device'">
      <rect x="9" y="3.5" width="14" height="25" rx="3" />
      <path d="M14 7h4" />
      <rect x="12.5" y="14" width="7" height="6" rx="1" />
      <path d="M14 14v-1.5a2 2 0 0 1 4 0V14" />
    </template>
    <template v-else-if="name === 'handshake'">
      <rect x="4" y="13" width="7" height="6" rx="1.2" />
      <path d="M5.5 13v-2a2 2 0 0 1 4 0v2" />
      <circle cx="24.5" cy="16" r="3.5" />
      <path d="M11 16h10" stroke-dasharray="1 2.5" />
      <path d="M16 7a9 9 0 0 1 0 18" opacity=".5" />
      <path d="M16 10.5a5.5 5.5 0 0 1 0 11" opacity=".75" />
    </template>
    <template v-else-if="name === 'qr'">
      <rect x="4" y="4" width="9" height="9" rx="1.5" />
      <rect x="19" y="4" width="9" height="9" rx="1.5" />
      <rect x="4" y="19" width="9" height="9" rx="1.5" />
      <path d="M19 19h3v3M28 19v.01M25 25h3v3M19 28h3M22 25v.01" />
      <path d="M7.5 7.5h2v2h-2zM22.5 7.5h2v2h-2zM7.5 22.5h2v2h-2z" />
    </template>
    <template v-else-if="name === 'topics'">
      <circle cx="16" cy="16" r="11.5" stroke-dasharray="2 3" />
      <path d="M16 9v7l4.5 3" />
      <path d="M25.5 5.5v4h-4" />
    </template>
    <template v-else-if="name === 'delivery'">
      <path d="M4 9.5h15l4 4v9H4z" />
      <path d="M8 16.5l3 3 5.5-6" />
      <path d="M16.5 19.5l1 1 5.5-6" opacity=".6" />
    </template>
    <template v-else-if="name === 'tests'">
      <path d="M12.5 4h7M13.5 4v8L6.5 25a2.2 2.2 0 0 0 2 3h15a2.2 2.2 0 0 0 2-3l-7-13V4" />
      <path d="M10 19h12" />
      <circle cx="14" cy="23" r=".6" />
      <circle cx="18.5" cy="22" r=".6" />
    </template>
  </svg>
</template>

<style scoped>
.feature-icon {
  width: 32px;
  height: 32px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.25;
  stroke-linecap: round;
  stroke-linejoin: round;
}
</style>
