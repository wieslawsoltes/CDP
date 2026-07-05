const { defineConfig } = require('@playwright/test');

const headless = process.env.HEADLESS !== 'false';

module.exports = defineConfig({
  testDir: './tests/playwright',
  timeout: 30000,
  expect: {
    timeout: 5000,
  },
  reporter: 'list',
  use: {
    // Tests connect programmatically in beforeAll using connectOverCDP, 
    // but we specify launch options here just in case.
  },
  webServer: {
    command: `dotnet run --project samples/CdpSampleApp/CdpSampleApp.csproj ${headless ? '-- --headless' : ''} > playwright-webserver.log 2>&1`,
    url: 'http://127.0.0.1:9222/json',
    reuseExistingServer: !process.env.CI,
    stdout: 'ignore',
    stderr: 'pipe',
  },
});
