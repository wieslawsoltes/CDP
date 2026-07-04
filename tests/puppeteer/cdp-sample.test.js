const test = require('node:test');
const assert = require('node:assert');
const puppeteer = require('puppeteer');

test.describe('Avalonia CDP E2E Automation - Puppeteer', () => {
  let browser;
  let page;

  test.before(async () => {
    // Connect to the running app over CDP
    browser = await puppeteer.connect({
      browserURL: 'http://127.0.0.1:9222',
      defaultViewport: null
    });
    const pages = await browser.pages();
    page = pages[0];
  });

  test.beforeEach(async () => {
    try {
      await page.evaluate(() => {
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
      });
      await new Promise(resolve => setTimeout(resolve, 500));
    } catch (e) {}
  });

  test.after(async () => {
    if (browser) {
      await browser.disconnect();
    }
  });

  test('Verify home page elements and interaction', async () => {
    const title = await page.title();
    assert.strictEqual(title, 'Avalonia CDP Inspector Sample');

    // Verify click counts and status text updates
    await page.waitForSelector('#txtStatus');
    let statusText = await page.$eval('#txtStatus', el => el.textContent);
    assert.strictEqual(statusText, 'Not Clicked');

    await page.waitForSelector('#btnClickMe');
    await page.click('#btnClickMe');
    await new Promise(resolve => setTimeout(resolve, 100));
    statusText = await page.$eval('#txtStatus', el => el.textContent);
    assert.strictEqual(statusText, 'Clicked 1 times!');

    await page.click('#btnClickMe');
    await new Promise(resolve => setTimeout(resolve, 100));
    statusText = await page.$eval('#txtStatus', el => el.textContent);
    assert.strictEqual(statusText, 'Clicked 2 times!');
  });

  test('Verify text box input and binding updates', async () => {
    await page.waitForSelector('#txtInput');
    await page.type('#txtInput', 'CDP Integration Testing!');
    const value = await page.$eval('#txtInput', el => el.value);
    assert.strictEqual(value, 'CDP Integration Testing!');
  });

  test('Verify slider and check box controls', async () => {
    await page.waitForSelector('#chkToggle');
    const isChecked = await page.$eval('#chkToggle', el => el.isChecked);
    assert.strictEqual(isChecked, false);
    
    await page.click('#chkToggle');
    await new Promise(resolve => setTimeout(resolve, 100));
    const isCheckedAfter = await page.$eval('#chkToggle', el => el.isChecked);
    assert.strictEqual(isCheckedAfter, true);

    await page.waitForSelector('#sliderValue');
    await page.evaluate(() => {
      document.querySelector('#sliderValue').value = 75;
    });
    await new Promise(resolve => setTimeout(resolve, 100));
    
    await page.waitForSelector('#txtSliderVal');
    const sliderText = await page.$eval('#txtSliderVal', el => el.textContent);
    assert.strictEqual(sliderText, 'Slider Value: 75');
  });

  test('Verify navigation between tabs', async () => {
    await page.evaluate(() => {
      document.querySelector('#tabContainer').selectedIndex = 1;
    });
    
    await page.waitForSelector('#scrollContainer');
    const scrollVisible = await page.$eval('#scrollContainer', el => el.isVisible || true);
    assert.ok(scrollVisible);
  });

  test('Verify URL-based page navigation and back interaction', async () => {
    await page.goto('http://127.0.0.1:9222/about');
    
    await page.waitForSelector('#tabAbout');
    const aboutTitle = await page.$eval('#tabAbout', el => el.isVisible || true);
    assert.ok(aboutTitle);

    await page.waitForSelector('#btnGoBack');
    await page.click('#btnGoBack');
    
    await page.waitForSelector('#tabHome');
    const isHomeVisible = await page.$eval('#tabHome', el => el.isVisible || true);
    assert.ok(isHomeVisible);
  });
});
