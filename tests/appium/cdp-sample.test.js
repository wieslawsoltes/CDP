const test = require('node:test');
const assert = require('node:assert');
const { remote } = require('webdriverio');
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const http = require('node:http');

async function checkPortReady(port, timeout = 30000) {
  const startTime = Date.now();
  while (Date.now() - startTime < timeout) {
    try {
      await new Promise((resolve, reject) => {
        const req = http.get(`http://127.0.0.1:${port}/json/version`, (res) => {
          if (res.statusCode === 200) resolve();
          else reject();
        });
        req.on('error', reject);
        req.end();
      });
      return true;
    } catch (e) {
      // Retry
    }
    // Simple check for Appium port as well (it doesn't have /json/version but listens on root)
    if (port === 4723) {
      try {
        await new Promise((resolve, reject) => {
          const req = http.get(`http://127.0.0.1:4723/status`, (res) => {
            resolve(); // any response means port is open
          });
          req.on('error', reject);
          req.end();
        });
        return true;
      } catch (e) {}
    }
    await new Promise(resolve => setTimeout(resolve, 200));
  }
  throw new Error(`Timeout waiting for port ${port}`);
}

test.describe('Avalonia CDP E2E Automation - Appium JS', () => {
  let browser;
  let sampleProcess;
  let driverProcess;

  test.before(async () => {
    // 1. Ensure CdpSampleApp is running on port 9222
    let sampleRunning = false;
    try {
      await fetch('http://127.0.0.1:9222/json/version');
      sampleRunning = true;
    } catch (e) {}

    if (!sampleRunning) {
      const headless = process.env.HEADLESS !== 'false';
      const args = ['run', '--project', 'samples/CdpSampleApp/CdpSampleApp.csproj'];
      if (headless) {
        args.push('--', '--headless');
      }
      
      const logFile = fs.openSync('appium-sample-webserver.log', 'w');
      sampleProcess = spawn('dotnet', args, {
        stdio: ['ignore', logFile, logFile]
      });

      await checkPortReady(9222, 30000);
    }

    // 2. Ensure custom Appium Driver is running on port 4723
    let driverRunning = false;
    try {
      const res = await fetch('http://127.0.0.1:4723/status');
      driverRunning = true;
    } catch (e) {}

    if (!driverRunning) {
      const logFile = fs.openSync('appium-driver-webserver.log', 'w');
      driverProcess = spawn('node', ['scripts/appium-cdp-driver.js'], {
        stdio: ['ignore', logFile, logFile]
      });

      await checkPortReady(4723, 10000);
    }

    // 3. Connect webdriverio client to custom Appium driver
    browser = await remote({
      protocol: 'http',
      hostname: '127.0.0.1',
      port: 4723,
      path: '/',
      capabilities: {
        platformName: 'windows',
        'appium:automationName': 'CDP'
      }
    });
  });

  test.beforeEach(async () => {
    try {
      await browser.executeScript(`
        document.querySelector('#tabContainer').selectedIndex = 0;
        const status = document.querySelector('#txtStatus');
        if (status !== null) status.textContent = 'Not Clicked';
        const input = document.querySelector('#txtInput');
        if (input !== null) input.value = '';
        if (typeof Window !== 'undefined' && Window !== null) {
          Window.clickCount = 0;
        }
        const chk = document.querySelector('#chkToggle');
        if (chk !== null) chk.isChecked = false;
        const rb1 = document.querySelector('#rbOption1');
        if (rb1 !== null) rb1.isChecked = true;
        const rb2 = document.querySelector('#rbOption2');
        if (rb2 !== null) rb2.isChecked = false;
      `, []);
      await new Promise(resolve => setTimeout(resolve, 500));
    } catch (e) {}
  });

  test.after(async () => {
    if (browser) {
      await browser.deleteSession();
    }
    if (driverProcess) {
      driverProcess.kill('SIGTERM');
    }
    if (sampleProcess) {
      sampleProcess.kill('SIGTERM');
    }
  });

  test('Verify home page elements and interaction', async () => {
    const title = await browser.getTitle();
    assert.strictEqual(title, 'Avalonia CDP Inspector Sample');

    const statusTextEl = await browser.$('#txtStatus');
    let statusText = await statusTextEl.getText();
    assert.strictEqual(statusText, 'Not Clicked');

    const clickBtn = await browser.$('#btnClickMe');
    await clickBtn.click();
    await new Promise(resolve => setTimeout(resolve, 200));

    statusText = await statusTextEl.getText();
    assert.strictEqual(statusText, 'Clicked 1 times!');

    await clickBtn.click();
    await new Promise(resolve => setTimeout(resolve, 200));

    statusText = await statusTextEl.getText();
    assert.strictEqual(statusText, 'Clicked 2 times!');
  });

  test('Verify text box input and binding updates', async () => {
    const txtInput = await browser.$('#txtInput');
    await txtInput.setValue('CDP Appium JS E2E!');
    const value = await txtInput.getAttribute('value');
    assert.strictEqual(value, 'CDP Appium JS E2E!');
  });

  test('Verify slider and check box controls', async () => {
    const chkToggle = await browser.$('#chkToggle');
    let isChecked = await chkToggle.isSelected();
    assert.strictEqual(isChecked, false);

    await chkToggle.click();
    await new Promise(resolve => setTimeout(resolve, 200));
    
    isChecked = await chkToggle.isSelected();
    assert.strictEqual(isChecked, true);

    await browser.executeScript(`document.querySelector('#sliderValue').value = 75;`, []);
    await new Promise(resolve => setTimeout(resolve, 200));

    const sliderTextEl = await browser.$('#txtSliderVal');
    const sliderText = await sliderTextEl.getText();
    assert.strictEqual(sliderText, 'Slider Value: 75');
  });

  test('Verify navigation between tabs', async () => {
    await browser.executeScript(`document.querySelector('#tabContainer').selectedIndex = 1;`, []);
    await new Promise(resolve => setTimeout(resolve, 200));

    const scrollContainer = await browser.$('#scrollContainer');
    const isDisplayed = await scrollContainer.isDisplayed();
    assert.ok(isDisplayed);
  });

  test('Verify URL-based page navigation and back interaction', async () => {
    await browser.navigateTo('http://127.0.0.1:9222/about');
    await new Promise(resolve => setTimeout(resolve, 200));

    const tabAbout = await browser.$('#tabAbout');
    const isAboutDisplayed = await tabAbout.isDisplayed();
    assert.ok(isAboutDisplayed);

    const btnGoBack = await browser.$('#btnGoBack');
    await btnGoBack.click();
    await new Promise(resolve => setTimeout(resolve, 200));

    const tabHome = await browser.$('#tabHome');
    const isHomeDisplayed = await tabHome.isDisplayed();
    assert.ok(isHomeDisplayed);
  });
});
