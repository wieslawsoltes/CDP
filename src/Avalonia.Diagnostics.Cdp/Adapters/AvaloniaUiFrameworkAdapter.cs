using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Diagnostics.Cdp.Domains;
using Chrome.DevTools.Protocol;
using Xaml.Compiler.Adapters;
using Avalonia.Controls.Primitives;

namespace Avalonia.Diagnostics.Cdp.Adapters
{
    public class AvaloniaUiFrameworkAdapter : IUiFrameworkAdapter
    {
        private readonly INodeMap _nodeMap;

        public AvaloniaUiFrameworkAdapter(INodeMap nodeMap)
        {
            _nodeMap = nodeMap;
        }

        public IReadOnlyCollection<string> XamlFileExtensions { get; } = new[] { "axaml", "xaml" };

        public string DefaultXmlNamespace => "https://github.com/avaloniaui";

        public bool IsControl(object target)
        {
            return target is Control;
        }

        public object? GetParent(object control)
        {
            if (control is ILogical logical)
            {
                return logical.LogicalParent;
            }
            if (control is Visual visual)
            {
                return visual.GetVisualParent();
            }
            return null;
        }

        public IReadOnlyCollection<object> GetChildren(object parent)
        {
            if (parent is ILogical logical)
            {
                return logical.LogicalChildren
                    .OfType<object>()
                    .Where(child => child is Visual || child is StyledElement)
                    .ToList();
            }
            return Array.Empty<object>();
        }

        public string GetTypeName(object control)
        {
            return control.GetType().Name;
        }

        public string? GetClassFullName(object control)
        {
            return control.GetType().FullName;
        }

        public object? GetPropertyValue(object control, string propertyName)
        {
            if (control == null) return null;
            var prop = control.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            return prop?.GetValue(control);
        }

        public async Task ApplyAttributeLiveAsync(object control, string propertyName, string valueString)
        {
            if (control is not Control ctrl) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (propertyName.Equals("class", StringComparison.OrdinalIgnoreCase))
                {
                    ctrl.Classes.Clear();
                    var classes = valueString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var cls in classes)
                    {
                        ctrl.Classes.Add(cls);
                    }
                }
                else if (propertyName.Equals("name", StringComparison.OrdinalIgnoreCase) || propertyName.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    ctrl.Name = valueString;
                }
                else if (propertyName.Equals("text", StringComparison.OrdinalIgnoreCase))
                {
                    if (ctrl is TextBlock textBlock)
                    {
                        textBlock.Text = valueString;
                    }
                    else if (ctrl is TextBox textBox)
                    {
                        textBox.Text = valueString;
                    }
                    else
                    {
                        if (ctrl is HeaderedContentControl headeredControl)
                        {
                            if (headeredControl.Header is string || headeredControl.Content is not string)
                            {
                                headeredControl.Header = valueString;
                            }
                            else
                            {
                                headeredControl.Content = valueString;
                            }
                        }
                        else if (ctrl is ContentControl contentControl)
                        {
                            contentControl.Content = valueString;
                        }
                        else if (ctrl is HeaderedItemsControl headeredItemsControl)
                        {
                            headeredItemsControl.Header = valueString;
                        }
                    }
                }
                else
                {
                    CssDomain.SetControlProperty(ctrl, propertyName, valueString);
                }
            });
        }

        public async Task RemoveAttributeLiveAsync(object control, string propertyName)
        {
            if (control is not Control ctrl) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (propertyName.Equals("class", StringComparison.OrdinalIgnoreCase))
                {
                    ctrl.Classes.Clear();
                }
                else if (propertyName.Equals("name", StringComparison.OrdinalIgnoreCase) || propertyName.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    ctrl.Name = null;
                }
            });
        }

        public async Task RemoveNodeLiveAsync(object control)
        {
            if (control is not Control ctrl) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var parent = ctrl.Parent;
                if (parent is Panel panel)
                {
                    panel.Children.Remove(ctrl);
                }
                else if (parent is ContentControl contentControl)
                {
                    if (contentControl.Content == ctrl) contentControl.Content = null;
                }
                else if (parent is Decorator decorator)
                {
                    if (decorator.Child == ctrl) decorator.Child = null;
                }
                else if (parent is HeaderedContentControl headeredControl)
                {
                    if (headeredControl.Content == ctrl) headeredControl.Content = null;
                    else if (headeredControl.Header == ctrl) headeredControl.Header = null;
                }
                else if (parent is HeaderedItemsControl headeredItemsControl)
                {
                    if (headeredItemsControl.Header == ctrl) headeredItemsControl.Header = null;
                }
                else if (ctrl.Parent == null && ctrl.GetVisualParent() is Panel visualPanel)
                {
                    visualPanel.Children.Remove(ctrl);
                }
            });
        }

        public async Task<object> InstantiateXamlFragmentAsync(string xamlFragment, Dictionary<string, string> inheritedNamespaces)
        {
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var loaded = Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xamlFragment);
                return loaded;
            });
        }

        public async Task<bool> ReplaceChildLiveAsync(object oldChild, object newChild)
        {
            if (oldChild is not Control oldCtrl || newChild is not Control newCtrl) return false;
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var parent = oldCtrl.Parent;
                if (parent is Panel panel)
                {
                    int index = panel.Children.IndexOf(oldCtrl);
                    if (index != -1)
                    {
                        panel.Children[index] = newCtrl;
                        return true;
                    }
                }
                else if (parent is ContentControl contentControl)
                {
                    if (contentControl.Content == oldCtrl)
                    {
                        contentControl.Content = newCtrl;
                        return true;
                    }
                }
                else if (parent is Decorator decorator)
                {
                    if (decorator.Child == oldCtrl)
                    {
                        decorator.Child = newCtrl;
                        return true;
                    }
                }
                else if (parent is HeaderedContentControl headeredControl)
                {
                    if (headeredControl.Content == oldCtrl)
                    {
                        headeredControl.Content = newCtrl;
                        return true;
                    }
                    else if (headeredControl.Header == oldCtrl)
                    {
                        headeredControl.Header = newCtrl;
                        return true;
                    }
                }
                else if (parent is HeaderedItemsControl headeredItemsControl)
                {
                    if (headeredItemsControl.Header == oldCtrl)
                    {
                        headeredItemsControl.Header = newCtrl;
                        return true;
                    }
                }
                else if (oldCtrl.Parent == null && oldCtrl.GetVisualParent() is Panel visualPanel)
                {
                    int index = visualPanel.Children.IndexOf(oldCtrl);
                    if (index != -1)
                    {
                        visualPanel.Children[index] = newCtrl;
                        return true;
                    }
                }
                return false;
            });
        }
    }
}
