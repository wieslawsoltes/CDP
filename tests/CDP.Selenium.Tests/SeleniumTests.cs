using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using CDP.Automation.Selenium;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CDP.Selenium.Tests;

public class SeleniumTests : IClassFixture<CdpSeleniumFixture>
{
    private readonly CdpSeleniumFixture _fixture;
    private readonly ChromeDriver _driver;

    public SeleniumTests(CdpSeleniumFixture fixture)
    {
        _fixture = fixture;
        _driver = fixture.Driver ?? throw new InvalidOperationException("Driver was not initialized in fixture.");
    }

    private void SelectTab(int index)
    {
        _driver.ExecuteScript($"document.querySelector('#tabContainer').selectedIndex = {index};");
        string checkScript = index switch
        {
            1 => "const el = document.querySelector('#scrollContainer'); return !!(el && el.offsetWidth > 0);",
            2 => "const el = document.querySelector('#btnGoBack'); return !!(el && el.offsetWidth > 0);",
            _ => "const el = document.querySelector('#txtInput'); return !!(el && el.offsetWidth > 0);"
        };

        for (int i = 0; i < 30; i++)
        {
            var isSettled = _driver.ExecuteScript(checkScript) as bool? ?? false;
            if (isSettled) return;
            System.Threading.Thread.Sleep(100);
        }
    }

    [Fact]
    public void VerifyHomePageElementsAndInteraction()
    {
        Assert.Equal("Avalonia CDP Inspector Sample", _driver.Title);

        // Reset Home Tab select index
        SelectTab(0);
        _driver.ExecuteScript("const status = document.querySelector('#txtStatus'); if (status) status.textContent = 'Not Clicked';");
        _driver.ExecuteScript("if (typeof Window !== 'undefined' && Window) Window.clickCount = 0;");

        var statusText = _driver.FindElement(By.Id("txtStatus"));
        for (int i = 0; i < 50; i++)
        {
            if (statusText.Text == "Not Clicked") break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.Equal("Not Clicked", statusText.Text);

        var clickBtn = _driver.FindElement(By.Id("btnClickMe"));
        clickBtn.Click();
        for (int i = 0; i < 50; i++)
        {
            if (statusText.Text == "Clicked 1 times!") break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.Equal("Clicked 1 times!", statusText.Text);

        clickBtn.Click();
        for (int i = 0; i < 50; i++)
        {
            if (statusText.Text == "Clicked 2 times!") break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.Equal("Clicked 2 times!", statusText.Text);
    }

    [Fact]
    public void VerifyTextBoxInputAndBinding()
    {
        SelectTab(0);
        var txtInput = _driver.FindElement(By.Id("txtInput"));
        txtInput.Clear();
        txtInput.SendKeys("CDP Selenium E2E!");
        for (int i = 0; i < 50; i++)
        {
            if (txtInput.GetAttribute("value") == "CDP Selenium E2E!") break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.Equal("CDP Selenium E2E!", txtInput.GetAttribute("value"));
    }

    [Fact]
    public void VerifySliderAndCheckBoxControls()
    {
        SelectTab(0);
        var chkToggle = _driver.FindElement(By.Id("chkToggle"));
        
        // Check standard toggling
        if (chkToggle.Selected)
        {
            chkToggle.Click();
        }
        for (int i = 0; i < 50; i++)
        {
            if (!chkToggle.Selected) break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.False(chkToggle.Selected);
        
        chkToggle.Click();
        for (int i = 0; i < 50; i++)
        {
            if (chkToggle.Selected) break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.True(chkToggle.Selected);

        // Update Slider value via Javascript evaluation
        _driver.ExecuteScript("document.querySelector('#sliderValue').value = 75;");
        
        var sliderText = _driver.FindElement(By.Id("txtSliderVal"));
        for (int i = 0; i < 50; i++)
        {
            if (sliderText.Text == "Slider Value: 75") break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.Equal("Slider Value: 75", sliderText.Text);
    }

    [Fact]
    public void VerifyNavigationBetweenTabs()
    {
        // Navigate using Javascript selectContainer trigger index
        SelectTab(1);
        var scrollContainer = _driver.FindElement(By.Id("scrollContainer"));
        Assert.True(scrollContainer.Displayed);

        SelectTab(2);
        var aboutTitle = _driver.FindElement(By.Id("tabAbout"));
        Assert.True(aboutTitle.Displayed);
    }

    [Fact]
    public void VerifyUrlBasedPageNavigationAndBackInteraction()
    {
        // Navigate to about page via URL
        _driver.Navigate().GoToUrl("http://127.0.0.1:9222/about");
        
        // Wait for about tab to be visible
        var tabAbout = _driver.FindElement(By.Id("tabAbout"));
        Assert.True(tabAbout.Displayed);

        // Click Go Back button
        var btnGoBack = _driver.FindElement(By.Id("btnGoBack"));
        btnGoBack.Click();

        // Wait for home page to be visible again
        var tabHome = _driver.FindElement(By.Id("tabHome"));
        Assert.True(tabHome.Displayed);
    }
}
