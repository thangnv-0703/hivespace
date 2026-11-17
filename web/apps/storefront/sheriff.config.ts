import type { SheriffConfig } from '@softarc/sheriff-core'

// Module boundaries (repo-structure §8, dev-standards §8.4):
// a feature is used only through its index.ts; features do not import other features;
// app/ wires everything; shared/ may use features' public APIs for the shell.
// Sheriff builds its graph from .ts files only; .vue imports are skipped here and covered by the
// no-restricted-imports rules in eslint.config.ts (no deep imports, no feature-to-feature imports).
export const config: SheriffConfig = {
  entryFile: 'src/app/main.ts',
  ignoreFileExtensions: (defaults) => [...defaults, 'vue'],
  modules: {
    'src/app': 'app',
    'src/features/<feature>': 'feature:<feature>',
    'src/shared': 'shared',
    'src/locales': 'locales',
  },
  depRules: {
    root: ['app', 'shared', 'locales', 'feature:*'],
    app: ['feature:*', 'shared', 'locales'],
    shared: ['feature:*', 'shared'],
    'feature:*': [],
    locales: [],
  },
}
