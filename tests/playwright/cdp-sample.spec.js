const { test, expect, chromium } = require('@playwright/test');

test.describe('Avalonia CDP E2E Automation', () => {
  let browser;
  let context;
  let page;

  test.beforeAll(async () => {
    // Connect over CDP to the running app
    browser = await chromium.connectOverCDP('http://127.0.0.1:9222');
    context = browser.contexts()[0];
    page = context.pages()[0];
  });
  
  test.beforeEach(async () => {
    try {
      await page.evaluate(() => {
        document.querySelector('#tabContainer').selectedIndex = 0;
        const status = document.querySelector('#txtStatus');
        if (status !== null) status.textContent = 'Not Clicked';
        const input = document.querySelector('#txtInput');
        if (input !== null) input.value = '';
        
        // Reset ClickCount in MainWindow C# backing state
        if (typeof Window !== 'undefined' && Window !== null) {
          Window.clickCount = 0;
        }
        
        // Reset checkbox and radio buttons state
        const chk = document.querySelector('#chkToggle');
        if (chk !== null) chk.isChecked = false;
        const rb1 = document.querySelector('#rbOption1');
        if (rb1 !== null) rb1.isChecked = true;
        const rb2 = document.querySelector('#rbOption2');
        if (rb2 !== null) rb2.isChecked = false;
      });
      await page.waitForTimeout(500);
    } catch (e) {}
  });

  test.afterAll(async () => {
    if (browser) {
      await browser.close();
    }
  });

  test('Verify home page elements and interaction', async () => {
    // Assert page title
    expect(await page.title()).toBe('Avalonia CDP Inspector Sample');

    // Assert that tabHome is selected
    const tabHome = page.locator('#tabHome');
    await expect(tabHome).toBeVisible();

    // Verify click counts and status text updates
    const statusText = page.locator('#txtStatus');
    await expect(statusText).toHaveText('Not Clicked');

    const clickBtn = page.locator('#btnClickMe');
    await clickBtn.click();
    await expect(statusText).toHaveText('Clicked 1 times!');

    await clickBtn.click();
    await expect(statusText).toHaveText('Clicked 2 times!');
  });

  test('Verify text box input and binding updates', async () => {
    // Fill text input
    const txtInput = page.locator('#txtInput');
    await txtInput.fill('CDP Integration Testing!');
    
    // Assert the value was successfully filled
    await expect(txtInput).toHaveValue('CDP Integration Testing!');
  });

  test('Verify slider and check box controls', async () => {
    // Toggle the checkbox
    const chkToggle = page.locator('#chkToggle');
    await expect(chkToggle).not.toBeChecked();
    await chkToggle.click();
    await expect(chkToggle).toBeChecked();

    // Change slider value
    await page.evaluate(() => {
      document.querySelector('#sliderValue').value = 75;
    });
    
    // Verify progress/slider sync TextBlock updates
    const sliderText = page.locator('#txtSliderVal');
    await expect(sliderText).toHaveText('Slider Value: 75');
  });

  test('Verify navigation between tabs', async () => {
    // Navigate using the tabs directly
    await page.evaluate(() => {
      document.querySelector('#tabContainer').selectedIndex = 1;
    });
    
    const scrollContainer = page.locator('#scrollContainer');
    await expect(scrollContainer).toBeVisible();

    // Navigate to about tab
    await page.evaluate(() => {
      document.querySelector('#tabContainer').selectedIndex = 2;
    });
    
    const aboutTitle = page.locator('#tabAbout');
    await expect(aboutTitle).toBeVisible();
  });

  test('Verify target auto-attachment with second window', async () => {
    // Set up page listener to capture the new window page target
    const newPagePromise = context.waitForEvent('page');

    // Click the open second window button
    const openSecondBtn = page.locator('#btnOpenSecond');
    await openSecondBtn.click();

    // Wait for the auto-attached page target to resolve
    const secondPage = await newPagePromise;
    expect(await secondPage.title()).toBe('Sample Second Window');

    // Assert content inside the new page target
    const textOnSecond = secondPage.locator('TextBlock:has-text("This is the second window!")');
    await expect(textOnSecond).toBeVisible();

    // Close the second page
    await secondPage.close();
  });

  test('Verify radio button selection and toggling', async () => {
    const rb1 = page.locator('#rbOption1');
    const rb2 = page.locator('#rbOption2');

    // Option 1 should be checked initially, Option 2 unchecked
    await expect(rb1).toBeChecked();
    await expect(rb2).not.toBeChecked();

    // Click Option 2
    await rb2.click();

    // Option 2 should be checked, Option 1 unchecked
    await expect(rb1).not.toBeChecked();
    await expect(rb2).toBeChecked();
  });

  test('Verify URL-based page navigation and back interaction', async () => {
    // Navigate to about page via URL
    await page.goto('http://127.0.0.1:9222/about');
    await page.waitForTimeout(500);
    
    // Expect about tab content to be visible
    const aboutTitle = page.locator('TextBlock:has-text("About This App")');
    await expect(aboutTitle).toBeVisible();

    // Click Go Back button to return home
    const btnGoBack = page.locator('#btnGoBack');
    await btnGoBack.click();

    // Home page elements should be visible again
    const tabHome = page.locator('#tabHome');
    await expect(tabHome).toBeVisible();
  });

  test('Verify HTTP request execution status update', async () => {
    const statusText = page.locator('#txtStatus');
    await expect(statusText).toHaveText('Not Clicked');

    const btnSendHttp = page.locator('#btnSendHttp');
    await btnSendHttp.click();

    // Expect status to update and wait for HTTP request to complete (success or failure)
    await expect(statusText).toHaveText(/(HTTP Request successful!|HTTP Failed:)/, { timeout: 10000 });
  });

  test('Verify scroll container content visibility and input in secondary tab', async () => {
    // Navigate to Scroll Test tab via URL
    await page.goto('http://127.0.0.1:9222/scroll');
    await page.waitForTimeout(500);

    const scrollContainer = page.locator('#scrollContainer');
    await expect(scrollContainer).toBeVisible();

    // Check presence of different scroll items
    const btnScroll1 = page.locator('#scrollBtn1');
    await expect(btnScroll1).toBeVisible();

    const txtScroll1 = page.locator('#scrollTxt1');
    await txtScroll1.fill('Scrolling Input!');
    await expect(txtScroll1).toHaveValue('Scrolling Input!');
  });

  test('Verify full multi-window interaction workflow', async () => {
    const newPagePromise = context.waitForEvent('page');

    const openSecondBtn = page.locator('#btnOpenSecond');
    await openSecondBtn.click();

    const secondPage = await newPagePromise;
    expect(await secondPage.title()).toBe('Sample Second Window');

    // Interact with element inside second window
    const btnSecondClick = secondPage.locator('#btnSecondClick');
    await expect(btnSecondClick).toBeVisible();
    await btnSecondClick.click();

    // Close window
    await secondPage.close();
  });
});

