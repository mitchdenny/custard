<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'

interface Props {
  code: string
  language?: string
  htmlPath: string // Path to the HTML file (relative to public, e.g., '/svg/text-overflow-truncate.html')
}

const props = withDefaults(defineProps<Props>(), {
  language: 'csharp'
})

const isOpen = ref(false)
const iframeRef = ref<HTMLIFrameElement | null>(null)
const containerRef = ref<HTMLDivElement | null>(null)

// Animation states
const isAnimating = ref(false)
const animationReady = ref(false)

function openPreview() {
  isOpen.value = true
  isAnimating.value = true
  
  // Start animation after DOM updates
  nextTick(() => {
    requestAnimationFrame(() => {
      animationReady.value = true
      // Animation complete after transition
      setTimeout(() => {
        isAnimating.value = false
      }, 300)
    })
  })
}

function closePreview() {
  animationReady.value = false
  
  // Wait for animation to complete before hiding
  setTimeout(() => {
    isOpen.value = false
    isAnimating.value = false
  }, 200)
}

function handleOverlayClick(e: MouseEvent) {
  // Only close if clicking the overlay itself, not the content
  if (e.target === e.currentTarget) {
    closePreview()
  }
}

function handleEscape(e: KeyboardEvent) {
  if (e.key === 'Escape' && isOpen.value) {
    closePreview()
  }
}

onMounted(() => {
  document.addEventListener('keydown', handleEscape)
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleEscape)
})
</script>

<template>
  <div class="static-terminal-preview">
    <!-- Code Block -->
    <div class="code-container">
      <div class="code-header">
        <span class="language-label">{{ language }}</span>
        <button 
          class="view-output-button" 
          @click="openPreview"
          title="View Output"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <polygon points="5 3 19 12 5 21 5 3"></polygon>
          </svg>
          View Output
        </button>
      </div>
      <div class="code-block">
        <slot>
          <pre><code>{{ code }}</code></pre>
        </slot>
      </div>
    </div>

    <!-- Floating Preview Overlay -->
    <Teleport to="body">
      <Transition name="overlay">
        <div 
          v-if="isOpen"
          class="preview-overlay"
          :class="{ 'animate-in': animationReady }"
          @click="handleOverlayClick"
        >
          <div 
            ref="containerRef"
            class="preview-container"
            :class="{ 'animate-in': animationReady }"
          >
            <!-- Header -->
            <div class="preview-header">
              <div class="preview-title">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <rect x="2" y="3" width="20" height="14" rx="2" ry="2"></rect>
                  <line x1="8" y1="21" x2="16" y2="21"></line>
                  <line x1="12" y1="17" x2="12" y2="21"></line>
                </svg>
                <span>Terminal Output</span>
              </div>
              <button class="close-button" @click="closePreview" title="Close (Esc)">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <line x1="18" y1="6" x2="6" y2="18"></line>
                  <line x1="6" y1="6" x2="18" y2="18"></line>
                </svg>
              </button>
            </div>

            <!-- Content -->
            <div class="preview-content">
              <iframe 
                ref="iframeRef"
                :src="`${htmlPath}?minimal=true`" 
                frameborder="0"
                sandbox="allow-scripts allow-same-origin"
                title="Terminal Output Preview"
              ></iframe>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.static-terminal-preview {
  margin: 1.5rem 0;
}

/* Code Container */
.code-container {
  border-radius: 8px;
  overflow: hidden;
  background: var(--vp-code-block-bg);
  border: 1px solid var(--vp-c-divider);
}

.code-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.5rem 1rem;
  background: var(--vp-c-bg-soft);
  border-bottom: 1px solid var(--vp-c-divider);
}

.language-label {
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--vp-c-text-2);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.view-output-button {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.375rem 0.75rem;
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--vp-c-brand-1);
  background: transparent;
  border: 1px solid var(--vp-c-brand-1);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.view-output-button:hover {
  background: var(--vp-c-brand-1);
  color: var(--vp-c-white);
}

.view-output-button svg {
  width: 14px;
  height: 14px;
}

.code-block {
  overflow-x: auto;
}

.code-block :deep(pre) {
  margin: 0;
  padding: 1rem;
  background: transparent;
}

.code-block :deep(code) {
  font-family: var(--vp-font-family-mono);
  font-size: 0.875rem;
  line-height: 1.6;
}

/* Preview Overlay */
.preview-overlay {
  position: fixed;
  inset: 0;
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0);
  backdrop-filter: blur(0px);
  transition: all 0.25s ease;
}

.preview-overlay.animate-in {
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
}

/* Preview Container */
.preview-container {
  display: flex;
  flex-direction: column;
  width: min(90vw, 900px);
  height: min(80vh, 700px);
  background: var(--vp-c-bg);
  border-radius: 12px;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
  overflow: hidden;
  transform: translateY(30px) scale(0.95);
  opacity: 0;
  transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}

.preview-container.animate-in {
  transform: translateY(0) scale(1);
  opacity: 1;
}

/* Preview Header */
.preview-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.75rem 1rem;
  background: var(--vp-c-bg-soft);
  border-bottom: 1px solid var(--vp-c-divider);
}

.preview-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--vp-c-text-1);
}

.preview-title svg {
  color: var(--vp-c-brand-1);
}

.close-button {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  padding: 0;
  background: transparent;
  border: none;
  border-radius: 6px;
  color: var(--vp-c-text-2);
  cursor: pointer;
  transition: all 0.2s ease;
}

.close-button:hover {
  background: var(--vp-c-bg-mute);
  color: var(--vp-c-text-1);
}

/* Preview Content */
.preview-content {
  flex: 1;
  overflow: hidden;
  background: #0f0f1a; /* Match terminal background */
}

.preview-content iframe {
  width: 100%;
  height: 100%;
  border: none;
}

/* Overlay Transition */
.overlay-enter-active {
  transition: opacity 0.2s ease;
}

.overlay-leave-active {
  transition: opacity 0.15s ease;
}

.overlay-enter-from,
.overlay-leave-to {
  opacity: 0;
}
</style>
