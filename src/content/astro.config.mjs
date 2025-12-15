// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// https://astro.build/config
export default defineConfig({
	integrations: [
		starlight({
			title: 'Hex1b',
			logo: {
				light: './src/assets/hex1b-logo-light.svg',
				dark: './src/assets/hex1b-logo-dark.svg',
			},
			social: [
				{ icon: 'github', label: 'GitHub', href: 'https://github.com/hex1b/hex1b' },
				{ icon: 'discord', label: 'Discord', href: 'https://discord.gg/hex1b' },
			],
			customCss: [
				'./src/styles/custom.css',
			],
			head: [
				{
					tag: 'meta',
					attrs: {
						property: 'og:image',
						content: '/og-image.png',
					},
				},
				{
					tag: 'meta',
					attrs: {
						name: 'twitter:card',
						content: 'summary_large_image',
					},
				},
			],
			sidebar: [
				{
					label: 'Getting Started',
					items: [
						{ label: 'Quick Start', slug: 'guides/example' },
					],
				},
				{
					label: 'How-to Guides',
					items: [
						{
							label: 'Foundational',
							autogenerate: { directory: 'how-to/foundational' },
						},
						{
							label: 'Building Apps',
							autogenerate: { directory: 'how-to/building-apps' },
						},
						{
							label: 'Advanced',
							autogenerate: { directory: 'how-to/advanced' },
						},
						{
							label: 'Extending Hex1b',
							autogenerate: { directory: 'how-to/extending' },
						},
					],
				},
				{
					label: 'Concepts',
					autogenerate: { directory: 'concepts' },
				},
				{
					label: 'API Reference',
					collapsed: true,
					items: [
						{ label: 'Overview', slug: 'reference/overview' },
						{
							label: 'Widgets',
							autogenerate: { directory: 'reference/widgets' },
						},
						{
							label: 'Nodes',
							autogenerate: { directory: 'reference/nodes' },
						},
						{
							label: 'Layout',
							autogenerate: { directory: 'reference/layout' },
						},
						{
							label: 'Input',
							autogenerate: { directory: 'reference/input' },
						},
						{
							label: 'Theming',
							autogenerate: { directory: 'reference/theming' },
						},
						{
							label: 'Extensions',
							autogenerate: { directory: 'reference/extensions' },
						},
						{
							label: 'Core',
							autogenerate: { directory: 'reference/core' },
						},
					],
				},
			],
		}),
	],
});
