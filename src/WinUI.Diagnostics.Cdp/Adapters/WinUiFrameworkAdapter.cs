using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;
using Chrome.DevTools.Protocol;
using Xaml.Compiler.Adapters;

namespace WinUI.Diagnostics.Cdp.Adapters
{
    public class WinUiFrameworkAdapter : IUiFrameworkAdapter
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly INodeMap _nodeMap;

        public WinUiFrameworkAdapter(DispatcherQueue dispatcherQueue, INodeMap nodeMap)
        {
            _dispatcherQueue = dispatcherQueue;
            _nodeMap = nodeMap;
        }

        public IReadOnlyCollection<string> XamlFileExtensions { get; } = new[] { "xaml" };

        public string DefaultXmlNamespace => "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        public bool IsControl(object target)
        {
            return target is DependencyObject;
        }

        public object? GetParent(object control)
        {
            if (control is DependencyObject depObj)
            {
                return VisualTreeHelper.GetParent(depObj);
            }
            return null;
        }

        public IReadOnlyCollection<object> GetChildren(object parent)
        {
            if (parent is DependencyObject depObj)
            {
                int count = VisualTreeHelper.GetChildrenCount(depObj);
                var list = new List<object>();
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null)
                    {
                        list.Add(child);
                    }
                }
                return list;
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
            if (control is not DependencyObject depObj) return;
            await _dispatcherQueue.InvokeAsync(() =>
            {
                if (depObj is FrameworkElement fe)
                {
                    if (propertyName.Equals("name", StringComparison.OrdinalIgnoreCase) || propertyName.Equals("id", StringComparison.OrdinalIgnoreCase))
                    {
                        fe.Name = valueString;
                        return;
                    }
                }

                if (propertyName.Equals("text", StringComparison.OrdinalIgnoreCase))
                {
                    if (depObj is TextBox textBox)
                    {
                        textBox.Text = valueString;
                        return;
                    }
                    if (depObj is TextBlock textBlock)
                    {
                        textBlock.Text = valueString;
                        return;
                    }
                    if (depObj is ContentControl cc)
                    {
                        cc.Content = valueString;
                        return;
                    }
                }

                var type = depObj.GetType();
                var prop = type.GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null && prop.CanWrite)
                {
                    var converted = ConvertValue(prop.PropertyType, valueString);
                    prop.SetValue(depObj, converted);
                }
            });
        }

        public async Task RemoveAttributeLiveAsync(object control, string propertyName)
        {
            if (control is not DependencyObject depObj) return;
            await _dispatcherQueue.InvokeAsync(() =>
            {
                if (depObj is FrameworkElement fe)
                {
                    if (propertyName.Equals("name", StringComparison.OrdinalIgnoreCase) || propertyName.Equals("id", StringComparison.OrdinalIgnoreCase))
                    {
                        fe.Name = "";
                        return;
                    }
                }

                var type = depObj.GetType();
                var prop = type.GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(depObj, prop.PropertyType.IsValueType ? Activator.CreateInstance(prop.PropertyType) : null);
                }
            });
        }

        public async Task RemoveNodeLiveAsync(object control)
        {
            if (control is not DependencyObject depObj) return;
            await _dispatcherQueue.InvokeAsync(() =>
            {
                var parent = VisualTreeHelper.GetParent(depObj);
                if (parent == null) return;

                if (parent is Panel panel)
                {
                    if (control is UIElement uiElement)
                    {
                        panel.Children.Remove(uiElement);
                    }
                }
                else if (parent is ContentControl contentControl)
                {
                    if (contentControl.Content == control) contentControl.Content = null;
                }
                else if (parent is Border border)
                {
                    if (border.Child == control) border.Child = null;
                }
            });
        }

        public async Task<object> InstantiateXamlFragmentAsync(string xamlFragment, Dictionary<string, string> inheritedNamespaces)
        {
            return await _dispatcherQueue.InvokeAsync(() =>
            {
                var loaded = XamlReader.Load(xamlFragment);
                return loaded;
            });
        }

        public async Task<bool> ReplaceChildLiveAsync(object oldChild, object newChild)
        {
            if (oldChild is not DependencyObject oldDep || newChild is not UIElement newUI) return false;
            return await _dispatcherQueue.InvokeAsync(() =>
            {
                var parent = VisualTreeHelper.GetParent(oldDep);
                if (parent == null) return false;

                if (parent is Panel panel)
                {
                    if (oldChild is UIElement oldUI)
                    {
                        int index = panel.Children.IndexOf(oldUI);
                        if (index != -1)
                        {
                            panel.Children.RemoveAt(index);
                            panel.Children.Insert(index, newUI);
                            return true;
                        }
                    }
                }
                else if (parent is ContentControl contentControl)
                {
                    if (contentControl.Content == oldChild)
                    {
                        contentControl.Content = newChild;
                        return true;
                    }
                }
                else if (parent is Border border)
                {
                    if (border.Child == oldChild)
                    {
                        border.Child = newUI;
                        return true;
                    }
                }
                return false;
            });
        }

        private object? ConvertValue(Type targetType, string value)
        {
            if (targetType == typeof(string)) return value;
            var converter = TypeDescriptor.GetConverter(targetType);
            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                return converter.ConvertFromInvariantString(value);
            }
            return Convert.ChangeType(value, targetType);
        }
    }
}
