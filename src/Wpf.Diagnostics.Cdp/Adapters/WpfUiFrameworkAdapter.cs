using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Chrome.DevTools.Protocol;
using Xaml.Compiler.Adapters;

namespace Wpf.Diagnostics.Cdp.Adapters
{
    public class WpfUiFrameworkAdapter : IUiFrameworkAdapter
    {
        private readonly INodeMap _nodeMap;

        public WpfUiFrameworkAdapter(INodeMap nodeMap)
        {
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
                return LogicalTreeHelper.GetParent(depObj) ?? VisualTreeHelper.GetParent(depObj);
            }
            return null;
        }

        public IReadOnlyCollection<object> GetChildren(object parent)
        {
            if (parent is DependencyObject depObj)
            {
                var list = new List<object>();
                var logicalChildren = LogicalTreeHelper.GetChildren(depObj);
                if (logicalChildren != null)
                {
                    foreach (var child in logicalChildren)
                    {
                        if (child != null)
                        {
                            list.Add(child);
                        }
                    }
                }

                if (list.Count == 0 && depObj is Visual visual)
                {
                    int count = VisualTreeHelper.GetChildrenCount(visual);
                    for (int i = 0; i < count; i++)
                    {
                        var child = VisualTreeHelper.GetChild(visual, i);
                        if (child != null)
                        {
                            list.Add(child);
                        }
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
            var dispatcher = depObj.Dispatcher;
            await dispatcher.InvokeAsync(() =>
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

                SetDependencyOrReflectionProperty(depObj, propertyName, valueString);
            });
        }

        public async Task RemoveAttributeLiveAsync(object control, string propertyName)
        {
            if (control is not DependencyObject depObj) return;
            var dispatcher = depObj.Dispatcher;
            await dispatcher.InvokeAsync(() =>
            {
                if (depObj is FrameworkElement fe)
                {
                    if (propertyName.Equals("name", StringComparison.OrdinalIgnoreCase) || propertyName.Equals("id", StringComparison.OrdinalIgnoreCase))
                    {
                        fe.Name = "";
                        return;
                    }
                }

                ClearDependencyOrReflectionProperty(depObj, propertyName);
            });
        }

        public async Task RemoveNodeLiveAsync(object control)
        {
            if (control is not DependencyObject depObj) return;
            var dispatcher = depObj.Dispatcher;
            await dispatcher.InvokeAsync(() =>
            {
                var parent = LogicalTreeHelper.GetParent(depObj) ?? VisualTreeHelper.GetParent(depObj);
                if (parent == null) return;

                if (parent is Panel panel)
                {
                    if (control is UIElement uiElement)
                    {
                        panel.Children.Remove(uiElement);
                    }
                }
                else if (parent is HeaderedContentControl headeredControl && headeredControl.Header == control)
                {
                    headeredControl.Header = null;
                }
                else if (parent is HeaderedItemsControl headeredItems && headeredItems.Header == control)
                {
                    headeredItems.Header = null;
                }
                else if (parent is ContentControl contentControl)
                {
                    if (contentControl.Content == control) contentControl.Content = null;
                }
                else if (parent is Decorator decorator)
                {
                    if (decorator.Child == control) decorator.Child = null;
                }
            });
        }

        public async Task<object> InstantiateXamlFragmentAsync(string xamlFragment, Dictionary<string, string> inheritedNamespaces)
        {
            var currentDispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            return await currentDispatcher.InvokeAsync(() =>
            {
                var parserContext = new ParserContext();
                foreach (var ns in inheritedNamespaces)
                {
                    parserContext.XmlnsDictionary.Add(ns.Key, ns.Value);
                }
                var loaded = XamlReader.Parse(xamlFragment, parserContext);
                return loaded;
            });
        }

        public async Task<bool> ReplaceChildLiveAsync(object oldChild, object newChild)
        {
            if (oldChild is not DependencyObject oldDep || newChild is not DependencyObject newDep) return false;
            var dispatcher = oldDep.Dispatcher;
            return await dispatcher.InvokeAsync(() =>
            {
                var parent = LogicalTreeHelper.GetParent(oldDep) ?? VisualTreeHelper.GetParent(oldDep);
                if (parent == null) return false;

                if (parent is Panel panel)
                {
                    if (oldChild is UIElement oldUI && newChild is UIElement newUI)
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
                else if (parent is Decorator decorator)
                {
                    if (decorator.Child == oldChild)
                    {
                        if (newChild is UIElement newUI)
                        {
                            decorator.Child = newUI;
                            return true;
                        }
                    }
                }
                return false;
            });
        }

        private void SetDependencyOrReflectionProperty(DependencyObject depObj, string propertyName, string valueString)
        {
            var type = depObj.GetType();
            var dpd = DependencyPropertyDescriptor.FromName(propertyName, type, type);
            if (dpd != null)
            {
                var converted = ConvertValue(dpd.PropertyType, valueString);
                dpd.SetValue(depObj, converted);
            }
            else
            {
                var prop = type.GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null && prop.CanWrite)
                {
                    var converted = ConvertValue(prop.PropertyType, valueString);
                    prop.SetValue(depObj, converted);
                }
            }
        }

        private void ClearDependencyOrReflectionProperty(DependencyObject depObj, string propertyName)
        {
            var type = depObj.GetType();
            var dpd = DependencyPropertyDescriptor.FromName(propertyName, type, type);
            if (dpd != null)
            {
                depObj.ClearValue(dpd.DependencyProperty);
            }
            else
            {
                var prop = type.GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(depObj, prop.PropertyType.IsValueType ? Activator.CreateInstance(prop.PropertyType) : null);
                }
            }
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
