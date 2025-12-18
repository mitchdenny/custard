<script setup lang="ts">
import { ref, onMounted } from 'vue'

interface Example {
  id: string
  title: string
  description: string
  websocketUrl: string
}

const examples = ref<Example[]>([])
const isLoading = ref(true)
const error = ref<string | null>(null)

async function loadExamples() {
  try {
    const response = await fetch('/examples')
    if (!response.ok) {
      throw new Error('Failed to load examples')
    }
    examples.value = await response.json()
    isLoading.value = false
  } catch (err) {
    console.error('Failed to load examples:', err)
    error.value = 'Failed to load gallery examples. Make sure the backend is running.'
    isLoading.value = false
  }
}

onMounted(() => {
  loadExamples()
})
</script>

<template>
  <ClientOnly>
    <div class="gallery-container">
      <!-- Loading state -->
      <div v-if="isLoading" class="gallery-loading">
        Loading exhibits...
      </div>
      
      <!-- Error state -->
      <div v-else-if="error" class="gallery-error">
        {{ error }}
      </div>
      
      <!-- Gallery grid with FloatingTerminal triggers -->
      <div v-else class="gallery-grid">
        <div 
          v-for="example in examples" 
          :key="example.id"
          class="gallery-card-wrapper"
        >
          <FloatingTerminal 
            :example="example.id" 
            :title="example.title" 
          />
          <div class="gallery-card-info">
            <div class="gallery-card-description">{{ example.description }}</div>
          </div>
        </div>
      </div>
    </div>
    
    <template #fallback>
      <div class="gallery-loading">Loading gallery...</div>
    </template>
  </ClientOnly>
</template>

<style scoped>
.gallery-container {
  margin-top: 24px;
}

.gallery-loading,
.gallery-error {
  text-align: center;
  padding: 48px;
  color: var(--vp-c-text-2);
}

.gallery-error {
  color: #ff6b6b;
}

.gallery-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 24px;
}

.gallery-card-wrapper {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.gallery-card-info {
  padding: 0 8px;
}

.gallery-card-description {
  color: var(--vp-c-text-2);
  font-size: 14px;
  line-height: 1.5;
}
</style>
