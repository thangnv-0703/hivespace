import { globalIgnores } from 'eslint/config'
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'
import pluginVue from 'eslint-plugin-vue'
import pluginVitest from '@vitest/eslint-plugin'
import pluginOxlint from 'eslint-plugin-oxlint'
import sheriff from '@softarc/eslint-plugin-sheriff'
import skipFormatting from 'eslint-config-prettier/flat'

const apiClient = {
  group: ['@hivespace/api-client', '@hivespace/api-client/*'],
  message: 'Import the API client only from a feature api.ts (dev-standards §8.4).',
}
const deepFeature = {
  group: ['@/features/*/*'],
  message: "Import a feature only through its index.ts (Sheriff encapsulation).",
}
const otherFeature = {
  group: ['@/features/*'],
  message: 'Features do not import other features; compose them in app/ or shared/.',
}

export default defineConfigWithVueTs(
  {
    name: 'app/files-to-lint',
    files: ['**/*.{vue,ts,mts,tsx}'],
  },

  globalIgnores(['**/dist/**', '**/dist-ssr/**', '**/coverage/**']),

  ...pluginVue.configs['flat/essential'],
  vueTsConfigs.recommended,

  {
    ...pluginVitest.configs.recommended,
    files: ['src/**/__tests__/*'],
  },

  // Module boundaries from sheriff.config.ts (dev-standards §8.4).
  {
    name: 'app/sheriff',
    files: ['src/**/*.{ts,vue}'],
    ignores: ['src/**/__tests__/**'],
    plugins: { '@softarc/sheriff': sheriff },
    rules: {
      '@softarc/sheriff/dependency-rule': 'error',
      '@softarc/sheriff/encapsulation': 'error',
    },
  },

  // Import rules that also cover .vue files (Sheriff skips them):
  // only a feature's api.ts imports the generated client; nobody deep-imports a feature;
  // features never import other features.
  {
    name: 'app/import-boundaries',
    files: ['src/**/*.{ts,vue}'],
    ignores: ['src/**/__tests__/**'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [apiClient, deepFeature] }],
    },
  },
  {
    name: 'app/import-boundaries-features',
    files: ['src/features/**/*.{ts,vue}'],
    ignores: ['src/**/__tests__/**'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [apiClient, deepFeature, otherFeature] }],
    },
  },
  {
    name: 'app/api-ts-may-use-client',
    files: ['src/**/api.ts'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [deepFeature, otherFeature] }],
    },
  },

  ...pluginOxlint.buildFromOxlintConfigFile('.oxlintrc.json'),

  skipFormatting,
)
