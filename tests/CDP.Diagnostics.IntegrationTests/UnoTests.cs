using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WinUI.Diagnostics.Cdp;
using WinUI.Diagnostics.Cdp.Domains;
using Xunit;

namespace CDP.Diagnostics.IntegrationTests;

public class UnoTests
{
    private static void BypassThreadAccess()
    {
        try
        {
            var assembly = System.Reflection.Assembly.Load("Uno.UI.Dispatching");
            var nativeDispatcherType = assembly.GetType("Uno.UI.Dispatching.NativeDispatcher");
            if (nativeDispatcherType != null)
            {
                var mainProp = nativeDispatcherType.GetProperty("Main", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var mainDispatcher = mainProp?.GetValue(null);
                if (mainDispatcher != null)
                {
                    var hasThreadAccessField = nativeDispatcherType.GetField("_hasThreadAccess", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance);
                    hasThreadAccessField?.SetValue(mainDispatcher, true);
                }
            }

            var dispatcherQueueType = typeof(Microsoft.UI.Dispatching.DispatcherQueue);
            var currentField = dispatcherQueueType.GetField("_current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance);
            var dq = currentField?.GetValue(null);
            if (dq == null)
            {
                var ctor = dispatcherQueueType.GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (ctor != null)
                {
                    dq = ctor.Invoke(null);
                    currentField?.SetValue(null, dq);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TEST DEBUG] Bypass failed: {ex}");
        }
    }

    private static void SetWindowContent(Window window, UIElement content)
    {
        var windowType = typeof(Window);
        var windowImplField = windowType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => f.Name == "_windowImplementation");
        var windowImpl = windowImplField?.GetValue(window);
        if (windowImpl != null)
        {
            var contentManagerField = windowImpl.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => f.Name == "_contentManager");
            var contentManager = contentManagerField?.GetValue(windowImpl);
            if (contentManager != null)
            {
                var contentField = contentManager.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(f => f.Name == "_content");
                contentField?.SetValue(contentManager, content);
            }
        }
    }

    [Fact]
    public async Task TestApplicationDomain()
    {
        CdpServer.EnsureInitialized();
        var session = new CdpSession(null!, null);

        var getDbRes = await ApplicationDomain.HandleAsync(session, "getDatabases", new JsonObject());
        Assert.NotNull(getDbRes["databases"]);

        var getResRes = await ApplicationDomain.HandleAsync(session, "getResources", new JsonObject());
        Assert.NotNull(getResRes["resources"]);
    }

    [Fact]
    public async Task TestAuditsDomain()
    {
        CdpServer.EnsureInitialized();
        var session = new CdpSession(null!, null);

        var diagRes = await AuditsDomain.HandleAsync(session, "runDiagnostics", new JsonObject());
        Assert.NotNull(diagRes["accessibilityScore"]);
        Assert.NotNull(diagRes["issues"]);
    }

    [Fact]
    public async Task TestBrowserDomain()
    {
        CdpServer.EnsureInitialized();
        var session = new CdpSession(null!, null);

        var verRes = await BrowserDomain.HandleAsync(session, "getVersion", new JsonObject());
        Assert.Equal("1.3", verRes["protocolVersion"]?.GetValue<string>());
        Assert.Equal("Uno/WinUI/3.0", verRes["product"]?.GetValue<string>());
    }

    [Fact]
    public async Task TestEmulationDomain()
    {
        CdpServer.EnsureInitialized();
        var session = new CdpSession(null!, null);

        var metricsRes = await EmulationDomain.HandleAsync(session, "setDeviceMetricsOverride", new JsonObject { ["width"] = 1024, ["height"] = 768 });
        Assert.NotNull(metricsRes);

        var clearRes = await EmulationDomain.HandleAsync(session, "clearDeviceMetricsOverride", new JsonObject());
        Assert.NotNull(clearRes);
    }

    private class SampleMcpTool : IMcpTool
    {
        public string Name => "sampleTool";
        public string Description => "Sample test tool";
        public JsonObject? InputSchema => new JsonObject { ["type"] = "object" };
        public Task<JsonNode?> InvokeAsync(JsonObject input)
        {
            return Task.FromResult<JsonNode?>(new JsonObject { ["status"] = "ok" });
        }
    }

    [Fact]
    public async Task TestWebMcpDomain()
    {
        CdpServer.EnsureInitialized();
        var session = new CdpSession(null!, null);

        McpToolRegistry.RegisterTool(new SampleMcpTool());

        var enableRes = await WebMcpDomain.HandleAsync(session, "enable", new JsonObject());
        Assert.NotNull(enableRes);

        var invokeRes = await WebMcpDomain.HandleAsync(session, "invokeTool", new JsonObject
        {
            ["toolName"] = "sampleTool",
            ["input"] = new JsonObject()
        });
        Assert.NotNull(invokeRes["invocationId"]);
    }

    [Fact]
    public void TestPopupAndSecondaryWindowSupport()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) return;

        try
        {
            // 1. Bypass thread access
            BypassThreadAccess();

            // 2. Initialize CdpServer
            CdpServer.EnsureInitialized();

            // 3. Create mock grids for windows
            var mainGrid = new Grid { Name = "MainGrid" };
            var secondaryGrid = new Grid { Name = "SecondaryGrid" };

            // Create main Window normally
            var mainWindow = new Window();
            SetWindowContent(mainWindow, mainGrid);

            // Register main Window in CdpServer
            CdpServer.Register(mainWindow, "MainWindow");

            Window? secondaryWindow = null;

            try
            {
                // Reflective setup for secondary window
                var coreWindowWindowType = typeof(Window).Assembly.GetType("Uno.UI.Xaml.Controls.CoreWindowWindow");
                Assert.NotNull(coreWindowWindowType);

                var windowImplField = typeof(Window).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(f => f.Name == "_windowImplementation");
                Assert.NotNull(windowImplField);

                var contentManagerType = typeof(Window).Assembly.GetType("Uno.UI.Xaml.Controls.ContentManager");
                Assert.NotNull(contentManagerType);

                var contentManagerField = coreWindowWindowType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(f => f.Name == "_contentManager");
                Assert.NotNull(contentManagerField);

                var contentField = contentManagerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(f => f.Name == "_content");
                Assert.NotNull(contentField);

                // Find a secondary Window instance that gets iterated AFTER mainWindow in GetWindows() ConcurrentDictionary
                for (int i = 0; i < 100; i++)
                {
                    var tempWindow = (Window)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Window));
                    var tempImpl = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(coreWindowWindowType);
                    windowImplField.SetValue(tempWindow, tempImpl);
                    var tempContentManager = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(contentManagerType);
                    contentManagerField.SetValue(tempImpl, tempContentManager);
                    contentField.SetValue(tempContentManager, secondaryGrid);

                    CdpServer.Register(tempWindow, "SecondaryWindow");

                    var firstWin = CdpServer.GetWindows().FirstOrDefault().Window;
                    if (firstWin == mainWindow)
                    {
                        secondaryWindow = tempWindow;
                        break;
                    }
                    CdpServer.Unregister(tempWindow);
                }

                Assert.NotNull(secondaryWindow);

                var windowsList = CdpServer.GetWindows().ToList();
                Console.WriteLine($"[TEST DEBUG] Registered Windows Count: {windowsList.Count}");
                foreach (var w in windowsList)
                {
                    Console.WriteLine($"  Window: Id={w.Id}, Title={w.Title}, WinObj={w.Window}, Content={w.Window.Content}");
                }

                // 4. Set up mock XamlRoot and VisualTree/PopupRoot
                var visualTreeType = typeof(Window).Assembly.GetType("Uno.UI.Xaml.Core.VisualTree");
                Assert.NotNull(visualTreeType);

                var visualTree = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(visualTreeType);
                var xamlRootCtor = typeof(XamlRoot).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault();
                Assert.NotNull(xamlRootCtor);

                var xamlRoot = (XamlRoot)xamlRootCtor.Invoke(new object[] { visualTree });

                // Set XamlRoot on VisualTree
                var setXamlRootMethod = visualTreeType.GetMethod("set_XamlRoot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                setXamlRootMethod?.Invoke(visualTree, new object[] { xamlRoot });

                // Create PopupRoot and set on VisualTree
                var popupRootType = typeof(Window).Assembly.GetType("Microsoft.UI.Xaml.Controls.Primitives.PopupRoot");
                Assert.NotNull(popupRootType);

                var popupRoot = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(popupRootType);
                var setPopupRootMethod = visualTreeType.GetMethod("set_PopupRoot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                setPopupRootMethod?.Invoke(visualTree, new object[] { popupRoot });

                // Initialize _children (UIElementCollection) on popupRoot (which is a Panel)
                var collectionType = typeof(UIElementCollection);
                var ctorCollection = collectionType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault();
                Assert.NotNull(ctorCollection);
                var collection = ctorCollection.Invoke(new object[] { popupRoot });

                var panelType = typeof(Panel);
                var childrenFieldOnPanel = panelType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(f => f.Name == "_children" && f.FieldType.Name == "UIElementCollection");
                Assert.NotNull(childrenFieldOnPanel);
                childrenFieldOnPanel.SetValue(popupRoot, collection);

                // Associate XamlRoot with mainGrid and secondaryGrid
                var setForElementMethod = typeof(XamlRoot).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "SetForElement");
                Assert.NotNull(setForElementMethod);

                setForElementMethod.Invoke(null, new object[] { mainGrid, null, xamlRoot });
                setForElementMethod.Invoke(null, new object[] { secondaryGrid, null, xamlRoot });

                // 5. Create uninitialized Popup
                var popup = (Popup)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Popup));
                var popupBtn = new Button { Name = "PopupBtn" };

                try
                {
                    popup.SetValue(Popup.ChildProperty, popupBtn);
                }
                catch { }

                try
                {
                    popup.SetValue(Popup.IsOpenProperty, true);
                }
                catch { }

                // Get popup weak reference
                var provider = (Uno.UI.DataBinding.IWeakReferenceProvider)popup;
                var weakRef = provider.WeakReference;
                Assert.NotNull(weakRef);

                // Initialize _openPopups list on popupRoot
                var weakRefType = typeof(Window).Assembly.GetType("Uno.UI.DataBinding.ManagedWeakReference");
                var listType = typeof(List<>).MakeGenericType(weakRefType);
                var listInstance = Activator.CreateInstance(listType);
                var addMethodOnList = listType.GetMethod("Add");
                addMethodOnList?.Invoke(listInstance, new object[] { weakRef });

                var openPopupsField = popupRootType.GetField("_openPopups", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(openPopupsField);
                openPopupsField.SetValue(popupRoot, listInstance);

                // Add popup to PopupRoot using reflection _children backing field on UIElement (which is the base of Panel)
                var uiElementType = typeof(UIElement);
                var childrenField = uiElementType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(f => f.Name == "_children" && f.DeclaringType == typeof(UIElement));
                Assert.NotNull(childrenField);

                var childrenList = childrenField.GetValue(popupRoot);
                if (childrenList == null)
                {
                    childrenList = Activator.CreateInstance(childrenField.FieldType);
                    childrenField.SetValue(popupRoot, childrenList);
                }
                var addMethod = childrenList.GetType().GetMethod("Add", new[] { typeof(UIElement) });
                Assert.NotNull(addMethod);
                addMethod.Invoke(childrenList, new object[] { popup });

                // 6. Verify GetChildren anchors secondary window content and popups to main content
                var children = CdpVisualTreeHelper.GetChildren(mainGrid, true).ToList();
                Console.WriteLine($"[TEST DEBUG] GetChildren returned: {children.Count} children.");
                foreach (var c in children)
                {
                    var nameProp = c.GetType().GetProperty("Name");
                    var name = nameProp?.GetValue(c);
                    Console.WriteLine($"  Child: Name={name}, Type={c.GetType().FullName}");
                }

                Assert.Contains(secondaryGrid, children);
                Assert.Contains(popupBtn, children);

                // 7. Verify GetParent maps popup children and secondary window content back to main window Content
                var parentOfSecondary = CdpVisualTreeHelper.GetParent(secondaryGrid, true);
                Assert.Equal(mainGrid, parentOfSecondary);

                var parentOfPopupChild = CdpVisualTreeHelper.GetParent(popupBtn, true);
                Assert.Equal(mainGrid, parentOfPopupChild);

                // 8. Verify QuerySelector locates element inside popup
                var foundBtn = SelectorEngine.QuerySelector(mainGrid, "#PopupBtn", true);
                Assert.NotNull(foundBtn);
                Assert.Equal(popupBtn, foundBtn);

                Console.WriteLine("[TEST SUCCESS] All Uno popup and secondary window integration tests passed!");
            }
            finally
            {
                CdpServer.Unregister(mainWindow);
                if (secondaryWindow != null)
                {
                    CdpServer.Unregister(secondaryWindow);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TEST FAILURE] Test threw: {ex}");
            throw;
        }
    }
}
