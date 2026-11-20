// One runner for every service's Gherkin contract (dev-standards §9.1).
// The .feature files live in contracts/features/<service>/ and are shared; steps live in ./steps.
export default {
  paths: ['../../contracts/features/identity/*.feature', '../../contracts/features/catalog/*.feature'],
  import: ['support/**/*.ts', 'steps/**/*.ts'],
  format: ['progress-bar', 'summary'],
  // Scenarios share one database per service, so they must run one after another.
  parallel: 0,
  worldParameters: {},
}
