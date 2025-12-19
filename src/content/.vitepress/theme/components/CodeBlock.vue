<script setup lang="ts">
import { ref, onMounted, watch, useSlots, nextTick, type VNode } from 'vue'
import { codeToHtml } from 'shiki'
import FloatingTerminal from './FloatingTerminal.vue'

const props = defineProps<{
  code?: string
  lang?: string
  title?: string
  command?: string
  example?: string
  exampleTitle?: string
}>()

const slots = useSlots()
const highlightedCode = ref<string>('')
const copied = ref(false)
const commandCopied = ref(false)
const actualCode = ref('')
const terminalRef = ref<InstanceType<typeof FloatingTerminal> | null>(null)
const backendAvailable = ref(false)

async function checkBackend() {
  try {
    const response = await fetch('/apps')
    backendAvailable.value = response.ok
  } catch {
    backendAvailable.value = false
  }
}

// Extract text from VNode tree recursively
function extractTextFromVNodes(vnodes: VNode[]): string {
  const parts: string[] = []
  
  for (const vnode of vnodes) {
    if (typeof vnode.children === 'string') {
      parts.push(vnode.children)
    } else if (Array.isArray(vnode.children)) {
      parts.push(extractTextFromVNodes(vnode.children as VNode[]))
    } else if (vnode.children && typeof vnode.children === 'object') {
      // Check for default slot function
      const children = vnode.children as Record<string, unknown>
      if (typeof children.default === 'function') {
        const result = children.default()
        if (Array.isArray(result)) {
          parts.push(extractTextFromVNodes(result))
        }
      }
    }
  }
  
  return parts.join('')
}

function getCodeFromSlot(): string {
  // Get the default slot function
  const defaultSlot = slots.default
  if (defaultSlot) {
    const vnodes = defaultSlot()
    if (vnodes && vnodes.length > 0) {
      // VitePress markdown parser wraps content in <p> tags
      // Each paragraph becomes a separate VNode
      const texts: string[] = []
      for (const vnode of vnodes) {
        // Check if it's a paragraph element
        if (vnode.type === 'p' && typeof vnode.children === 'string') {
          texts.push(vnode.children)
        } else if (typeof vnode.children === 'string') {
          texts.push(vnode.children)
        } else if (Array.isArray(vnode.children)) {
          texts.push(extractTextFromVNodes(vnode.children as VNode[]))
        } else {
          // Try to extract any text content
          const extracted = extractTextFromVNodes([vnode])
          if (extracted) {
            texts.push(extracted)
          }
        }
      }
      // Join with double newlines (blank line separator)
      return texts.filter(t => t.trim()).join('\n\n')
    }
  }
  return ''
}

async function highlightCode() {
  // Get code from prop or slot VNodes
  const code = props.code || getCodeFromSlot()
  if (code && code !== actualCode.value) {
    actualCode.value = code
    highlightedCode.value = await codeToHtml(code, {
      lang: props.lang || 'csharp',
      theme: 'github-dark'
    })
  }
}

function copyToClipboard() {
  navigator.clipboard.writeText(actualCode.value)
  copied.value = true
  setTimeout(() => {
    copied.value = false
  }, 2000)
}

function copyCommand() {
  if (props.command) {
    navigator.clipboard.writeText(props.command)
    commandCopied.value = true
    setTimeout(() => {
      commandCopied.value = false
    }, 2000)
  }
}

function openDemo() {
  if (terminalRef.value) {
    terminalRef.value.openTerminal()
  }
}

onMounted(async () => {
  // Wait for DOM to be fully rendered
  await nextTick()
  await highlightCode()
  if (props.example) {
    checkBackend()
  }
})

watch(() => props.code, () => {
  highlightCode()
})
</script>

<template>
  <div class="code-block-wrapper">
    <div class="code-header">
      <span class="code-lang">{{ title || lang || 'C#' }}</span>
      <button class="copy-button" @click="copyToClipboard" :title="copied ? 'Copied!' : 'Copy code'">
        <svg v-if="!copied" class="copy-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
          <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
        </svg>
        <svg v-else class="copy-icon check" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <polyline points="20 6 9 17 4 12"></polyline>
        </svg>
      </button>
    </div>
    <div 
      v-if="highlightedCode" 
      class="code-block highlighted"
      v-html="highlightedCode"
    ></div>
    <pre v-else class="code-block"><code>{{ actualCode }}</code></pre>
    
    <!-- Integrated terminal command footer -->
    <div v-if="command" class="command-footer">
      <div class="terminal-icon-box">
        <svg class="terminal-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <polyline points="4 17 10 11 4 5"></polyline>
          <line x1="12" y1="19" x2="20" y2="19"></line>
        </svg>
      </div>
      <div class="command-content">
        <code class="command-text">{{ command }}</code>
      </div>
      <button class="command-copy-button" @click="copyCommand" :title="commandCopied ? 'Copied!' : 'Copy command'">
        <svg v-if="!commandCopied" class="command-copy-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
          <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
        </svg>
        <svg v-else class="command-copy-icon check" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <polyline points="20 6 9 17 4 12"></polyline>
        </svg>
      </button>
      
      <!-- Run in browser button -->
      <ClientOnly>
        <button 
          v-if="example && backendAvailable"
          class="run-button" 
          @click="openDemo"
          title="Run in browser"
        >
          <svg class="play-icon" viewBox="0 0 24 24" fill="currentColor">
            <polygon points="5 3 19 12 5 21 5 3"></polygon>
          </svg>
          <span class="run-label">Run in browser</span>
        </button>
      </ClientOnly>
    </div>
    
    <!-- Hidden FloatingTerminal -->
    <ClientOnly>
      <div v-if="example && backendAvailable" class="hidden-terminal">
        <FloatingTerminal 
          ref="terminalRef"
          :example="example"
          :title="exampleTitle || 'Demo'"
        />
      </div>
    </ClientOnly>
  </div>
</template>

<style scoped>
.code-block-wrapper {
  background: #24273a;
  border-radius: 8px;
  overflow: hidden;
  margin: 16px 0;
}

.code-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 16px;
  background: rgba(0, 0, 0, 0.2);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.code-lang {
  font-size: 12px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.5);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.copy-button {
  display: flex;
  align-items: center;
  justify-content: center;
  background: transparent;
  border: none;
  padding: 4px;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s ease;
}

.copy-button:hover {
  background: rgba(255, 255, 255, 0.1);
}

.copy-icon {
  width: 16px;
  height: 16px;
  color: rgba(255, 255, 255, 0.5);
  transition: color 0.2s ease;
}

.copy-button:hover .copy-icon {
  color: rgba(255, 255, 255, 0.8);
}

.copy-icon.check {
  color: #4ecdc4;
}

.code-block {
  margin: 0;
  padding: 20px;
  overflow-x: auto;
  font-family: 'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace;
  font-size: 13px;
  line-height: 1.3;
  color: #e0e0e0;
}

.code-block code {
  white-space: pre;
}

.code-block.highlighted :deep(pre) {
  margin: 0;
  padding: 0;
  background: transparent !important;
  counter-reset: line;
}

.code-block.highlighted :deep(code) {
  font-family: 'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace;
  font-size: 13px;
  line-height: 1.3;
  display: block;
  position: relative;
  padding-left: calc(2em + 20px);
}

.code-block.highlighted :deep(code::before) {
  content: '';
  position: absolute;
  left: calc(2em + 8px);
  top: 0;
  bottom: 0;
  width: 1px;
  background: rgba(255, 255, 255, 0.08);
}

.code-block.highlighted :deep(code .line) {
  display: block;
  line-height: 1.0;
}

.code-block.highlighted :deep(code .line::before) {
  counter-increment: line;
  content: counter(line);
  display: inline-block;
  width: 2em;
  margin-left: calc(-2em - 20px);
  margin-right: 20px;
  text-align: right;
  color: rgba(255, 255, 255, 0.25);
}

/* Command footer styles */
.command-footer {
  display: flex;
  align-items: stretch;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}

.terminal-icon-box {
  display: flex;
  align-items: center;
  justify-content: center;
  background: #1e2030;
  padding: 12px 14px;
  flex-shrink: 0;
}

.terminal-icon {
  width: 18px;
  height: 18px;
  color: #4ecdc4;
}

.command-content {
  flex: 1;
  display: flex;
  align-items: center;
  background: linear-gradient(135deg, #4ecdc4 0%, #44a8a0 100%);
  padding: 12px 16px;
  overflow-x: auto;
}

.command-text {
  font-family: 'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace;
  font-size: 13px;
  color: #0f0f1a;
  white-space: nowrap;
}

.command-copy-button {
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #44a8a0 0%, #3d9690 100%);
  border: none;
  padding: 12px 14px;
  cursor: pointer;
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.command-copy-button:hover {
  background: linear-gradient(135deg, #4ecdc4 0%, #44a8a0 100%);
}

.command-copy-icon {
  width: 16px;
  height: 16px;
  color: #0f0f1a;
}

.command-copy-icon.check {
  color: #0f0f1a;
}

/* Run button */
.run-button {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  background: #1e2030;
  border: none;
  padding: 12px 18px;
  cursor: pointer;
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.run-button:hover {
  background: #2a2d42;
}

.run-button:hover .play-icon {
  transform: scale(1.1);
}

.play-icon {
  width: 14px;
  height: 14px;
  color: #4ecdc4;
  transition: transform 0.2s ease;
}

.run-label {
  font-size: 13px;
  font-weight: 600;
  color: #4ecdc4;
}

/* Hide the terminal trigger card */
.hidden-terminal {
  position: absolute;
  width: 0;
  height: 0;
  overflow: hidden;
  pointer-events: none;
}

.hidden-terminal :deep(.terminal-trigger) {
  display: none !important;
}

/* Custom scrollbar styling */
.code-block::-webkit-scrollbar {
  height: 8px;
}

.code-block::-webkit-scrollbar-track {
  background: rgba(0, 0, 0, 0.2);
  border-radius: 4px;
}

.code-block::-webkit-scrollbar-thumb {
  background: rgba(78, 205, 196, 0.4);
  border-radius: 4px;
}

.code-block::-webkit-scrollbar-thumb:hover {
  background: rgba(78, 205, 196, 0.6);
}

/* Firefox scrollbar */
.code-block {
  scrollbar-width: thin;
  scrollbar-color: rgba(78, 205, 196, 0.4) rgba(0, 0, 0, 0.2);
}

@media (max-width: 600px) {
  .command-footer {
    flex-wrap: wrap;
  }
  
  .command-content {
    flex: 1 1 auto;
    min-width: 0;
  }
  
  .run-button {
    width: 100%;
    justify-content: center;
    border-top: 1px solid rgba(255, 255, 255, 0.08);
  }
}
</style>
