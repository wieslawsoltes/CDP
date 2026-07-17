using CdpGalleryApp;

namespace CdpGalleryApp.ViewModels;

public class HtmlPageViewModel : ViewModelBase
{
    private string _htmlText = @"<div class=""container"">
  <div class=""header"">
    <h1>HTML/CSS Flex Layout Showcase</h1>
    <p class=""subtitle"">SkiaSharp Rendering Engine Demo</p>
  </div>

  <!-- Flexbox Layout Row Container -->
  <div class=""flex-row"">
    <div class=""box card1"">
      <h3>Flex Card 1</h3>
      <p>This box demonstrates standard block margins, borders, padding, and text-wrapping layout flow in a flex container.</p>
    </div>
    
    <div class=""box card2"">
      <h3>Flex Card 2</h3>
      <p>This box showcases custom border radius, colorful borders, system background, and custom padding.</p>
    </div>
  </div>

  <div class=""footer"">
    <p>Notice how text wraps dynamically as you resize this window or adjust the text inputs in the editor pane.</p>
  </div>
</div>";

    private string _cssText = @".container {
  padding: 18px;
  background-color: #1a1a1a;
  border: 2px solid #333333;
  border-radius: 8px;
}

.header {
  margin-bottom: 16px;
  border-bottom: 2px solid #444444;
  padding-bottom: 8px;
}

h1 {
  color: #4caf50;
  margin: 0;
  font-size: 22px;
}

.subtitle {
  color: #9aa0a6;
  font-size: 13px;
  margin: 4px 0 0 0;
}

.flex-row {
  display: flex;
  flex-direction: row;
  justify-content: space-between;
  margin-bottom: 16px;
}

.box {
  width: 220px;
  padding: 14px;
  margin: 6px;
  border-radius: 6px;
}

.card1 {
  background-color: #212c21;
  border: 1.5px solid #4caf50;
  color: #e8eaed;
}

.card2 {
  background-color: #1e293b;
  border: 1.5px solid #3b82f6;
  color: #e8eaed;
}

.footer {
  padding: 10px;
  background-color: #262626;
  border-left: 4px solid #f59e0b;
  color: #cccccc;
  font-size: 12px;
}";

    public string HtmlText
    {
        get => _htmlText;
        set => RaiseAndSetIfChanged(ref _htmlText, value);
    }

    public string CssText
    {
        get => _cssText;
        set => RaiseAndSetIfChanged(ref _cssText, value);
    }
}
