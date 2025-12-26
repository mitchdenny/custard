import DefaultTheme from 'vitepress/theme'
import type { Theme } from 'vitepress'
import TerminalDemo from './components/TerminalDemo.vue'
import TerminalGallery from './components/TerminalGallery.vue'
import FloatingTerminal from './components/FloatingTerminal.vue'
import FeatureSamples from './components/FeatureSamples.vue'
import InstallGuide from './components/InstallGuide.vue'
import TerminalCommand from './components/TerminalCommand.vue'
import CodeBlock from './components/CodeBlock.vue'
import StaticTerminal from './components/StaticTerminal.vue'
import StaticCodeBlock from './components/StaticCodeBlock.vue'
import StaticTerminalPreview from './components/StaticTerminalPreview.vue'
import './custom.css'

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    // Register global components
    app.component('TerminalDemo', TerminalDemo)
    app.component('TerminalGallery', TerminalGallery)
    app.component('FloatingTerminal', FloatingTerminal)
    app.component('FeatureSamples', FeatureSamples)
    app.component('InstallGuide', InstallGuide)
    app.component('TerminalCommand', TerminalCommand)
    app.component('CodeBlock', CodeBlock)
    app.component('StaticTerminal', StaticTerminal)
    app.component('StaticCodeBlock', StaticCodeBlock)
    app.component('StaticTerminalPreview', StaticTerminalPreview)
  }
} satisfies Theme
