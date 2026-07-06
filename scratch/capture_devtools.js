const { chromium } = require('playwright');
const http = require('http');
const path = require('path');
const fs = require('fs');

async function getJson(url) {
  return new Promise((resolve, reject) => {
    http.get(url, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        try {
          resolve(JSON.parse(data));
        } catch (e) {
          reject(e);
        }
      });
    }).on('error', reject);
  });
}

async function main() {
  const artifactsDir = '/Users/wieslawsoltes/.gemini/antigravity/brain/32ddf4c1-722a-41d6-a34b-c408166cc350';
  if (!fs.existsSync(artifactsDir)) {
    fs.mkdirSync(artifactsDir, { recursive: true });
  }

  console.log("Launching Chromium with DevTools enabled in headful mode...");
  const browser = await chromium.launch({
    headless: false,
    args: ['--auto-open-devtools-for-tabs', '--remote-debugging-port=9225']
  });
  
  const context = await browser.newContext();
  const page = await context.newPage();
  console.log("Navigating to example.com...");
  await page.goto('https://example.com');
  
  console.log("Waiting for targets to populate...");
  await page.waitForTimeout(4000);
  
  try {
    const referencePath = path.join(artifactsDir, 'devtools_reference.png');
    console.log("Connecting to browser endpoint http://127.0.0.1:9225 ...");
    const devtoolsBrowser = await chromium.connectOverCDP('http://127.0.0.1:9225');
    const pages = devtoolsBrowser.contexts()[0].pages();
    console.log("Pages inside connected context:", pages.map(p => p.url()));
    
    const devtoolsPage = pages.find(p => p.url().startsWith('devtools://'));
    if (devtoolsPage) {
      // Set a larger viewport for DevTools page if possible
      try {
        await devtoolsPage.setViewportSize({ width: 1200, height: 800 });
      } catch (e) {
        console.log("Could not set viewport on devtools page directly (non-viewport page type), continuing...");
      }
      await devtoolsPage.screenshot({ path: referencePath });
      console.log("Successfully saved DevTools reference screenshot to:", referencePath);
    } else {
      console.error("Could not find DevTools page in the context. Trying fallback...");
      const targetPage = pages.find(p => p.url().includes('example.com'));
      if (targetPage && targetPage.devtoolsFrontendUrl) {
        // Fallback by navigating to frontend
        const fallbackPage = await context.newPage();
        await fallbackPage.goto(targetPage.devtoolsFrontendUrl);
        await fallbackPage.waitForTimeout(5000);
        await fallbackPage.screenshot({ path: referencePath });
        console.log("Successfully saved fallback screenshot to:", referencePath);
      }
    }
    await devtoolsBrowser.close();
  } catch (err) {
    console.error("Error querying/capturing DevTools:", err);
  }
  
  await browser.close();
}

main().catch(console.error);
