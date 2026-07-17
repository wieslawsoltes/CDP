using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xaml.Compiler.TypeSystem;
using Xaml.Compiler.TypeSystemImpl;

namespace Xaml.Compiler.Registry;

public class XamlSchemaRegistry
{
    private readonly ConcurrentDictionary<string, IXamlNamespace> _namespaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<(IXamlNamespace, string), IXamlType> _types = new();
    private readonly ConcurrentDictionary<(string AssemblyName, string ClrNamespace), string> _typeNsCache = new();
    private readonly object _resolveLock = new();

    public void RegisterNamespace(IXamlNamespace ns) => _namespaces[ns.Uri] = ns;

    public IEnumerable<IXamlNamespace> Namespaces => _namespaces.Values;

    public void EnsureNamespaceRegistered(string xmlNamespace)
    {
        if (xmlNamespace != null && xmlNamespace.StartsWith("clr-namespace:", StringComparison.OrdinalIgnoreCase))
        {
            if (!_namespaces.ContainsKey(xmlNamespace))
            {
                var parts = xmlNamespace.Substring("clr-namespace:".Length).Split(';');
                var clrNs = parts[0].Trim();
                var asmName = "";
                foreach (var part in parts.Skip(1))
                {
                    var kv = part.Trim();
                    if (kv.StartsWith("assembly=", StringComparison.OrdinalIgnoreCase))
                    {
                        asmName = kv.Substring("assembly=".Length).Trim();
                        break;
                    }
                }
                
                var targetAssemblies = string.IsNullOrEmpty(asmName) ? Array.Empty<string>() : new[] { asmName };
                var clrNamespaces = new[] { clrNs };
                var dynamicNs = new XamlNamespace(xmlNamespace, "local", targetAssemblies, clrNamespaces);
                RegisterNamespace(dynamicNs);
            }
        }
    }

    public IXamlType? ResolveType(string xmlNamespace, string typeName)
    {
        EnsureNamespaceRegistered(xmlNamespace);

        if (xmlNamespace == null || !_namespaces.TryGetValue(xmlNamespace, out var ns)) return null;
        return _types.TryGetValue((ns, typeName), out var type) ? type : ResolveViaReflection(ns, typeName);
    }

    public IXamlProperty? ResolveProperty(IXamlType type, string propertyName)
    {
        if (propertyName.Contains('.'))
        {
            var parts = propertyName.Split('.');
            var ownerTypeName = parts[0];
            var subPropName = parts[1];
            var ownerType = ResolveType(type.Namespace.Uri, ownerTypeName);
            return ownerType?.GetProperty(subPropName);
        }
        return type.GetProperty(propertyName);
    }

    private IXamlType? ResolveViaReflection(IXamlNamespace ns, string typeName)
    {
        var key = (ns, typeName);
        lock (_resolveLock)
        {
            if (_types.TryGetValue(key, out var cachedType)) return cachedType;

            Type? resolvedType = null;

            var targetAssemblies = ns.TargetAssemblies;
            if (targetAssemblies == null || targetAssemblies.Count == 0)
            {
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in loadedAssemblies)
                {
                    foreach (var clrNs in ns.ClrNamespaces)
                    {
                        var fullTypeName = $"{clrNs}.{typeName}";
                        resolvedType = assembly.GetType(fullTypeName);
                        if (resolvedType != null) break;

                        var extensionTypeName = $"{clrNs}.{typeName}Extension";
                        resolvedType = assembly.GetType(extensionTypeName);
                        if (resolvedType != null) break;
                    }
                    if (resolvedType != null) break;
                }
            }
            else
            {
                foreach (var assemblyName in targetAssemblies)
                {
                    Assembly? assembly = null;
                    try
                    {
                        assembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
                        
                        if (assembly == null)
                        {
                            assembly = Assembly.Load(new AssemblyName(assemblyName));
                        }
                    }
                    catch
                    {
                        continue; // Skip assemblies that fail to load
                    }

                    if (assembly == null) continue;

                    foreach (var clrNs in ns.ClrNamespaces)
                    {
                        var fullTypeName = $"{clrNs}.{typeName}";
                        resolvedType = assembly.GetType(fullTypeName);
                        if (resolvedType != null) break;

                        // Support implicit MarkupExtension suffix resolution
                        var extensionTypeName = $"{clrNs}.{typeName}Extension";
                        resolvedType = assembly.GetType(extensionTypeName);
                        if (resolvedType != null) break;
                    }

                    if (resolvedType != null) break;
                }
            }

        if (resolvedType == null)
        {
            return null;
        }

        var xamlType = BuildXamlType(resolvedType, ns);
        _types[key] = xamlType;
        return xamlType;
        }
    }

    private IXamlType BuildXamlType(Type type, IXamlNamespace ns)
    {
        IXamlType? xamlBaseType = null;
        if (type.BaseType != null && type.BaseType != typeof(object))
        {
            var baseTypeNsUri = GetNamespaceUriForType(type.BaseType, ns);
            if (baseTypeNsUri != null)
            {
                xamlBaseType = ResolveType(baseTypeNsUri, type.BaseType.Name);
            }
        }

        return new XamlType(
            type.Name,
            ns,
            xamlBaseType,
            type,
            self => LoadPropertiesForType(type, self)
        );
    }

    private string GetNamespaceUriForType(Type type, IXamlNamespace currentNs)
    {
        var asmName = type.Assembly.GetName().Name ?? "";
        var clrNs = type.Namespace ?? "";
        var key = (asmName, clrNs);

        return _typeNsCache.GetOrAdd(key, _ =>
        {
            foreach (var ns in _namespaces.Values)
            {
                if (ns.TargetAssemblies.Contains(asmName, StringComparer.OrdinalIgnoreCase) &&
                    ns.ClrNamespaces.Contains(clrNs, StringComparer.OrdinalIgnoreCase))
                {
                    return ns.Uri;
                }
            }
            return $"clr-namespace:{clrNs};assembly={asmName}";
        });
    }

    private IReadOnlyCollection<IXamlProperty> LoadPropertiesForType(Type type, IXamlType declaringXamlType)
    {
        var properties = new List<IXamlProperty>();

        // 1. Standard Instance Properties
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var propInfo in type.GetProperties(flags))
        {
            var propXamlType = ResolveTypeForReflectionType(propInfo.PropertyType, declaringXamlType.Namespace);
            if (propXamlType != null)
            {
                var contentAttr = type.GetCustomAttributes(true)
                    .FirstOrDefault(a => a.GetType().Name == "ContentPropertyAttribute");
                bool isContent = false;
                if (contentAttr != null)
                {
                    var contentPropName = contentAttr.GetType().GetProperty("Name")?.GetValue(contentAttr) as string;
                    if (string.Equals(propInfo.Name, contentPropName, StringComparison.OrdinalIgnoreCase))
                    {
                        isContent = true;
                    }
                }
                properties.Add(new XamlProperty(propInfo.Name, declaringXamlType, propXamlType, propInfo, isContent));
            }
        }

        // 2. Attached Properties (Convention scanning: GetX and SetX static methods)
        var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        var getterMethods = staticMethods.Where(m => m.Name.StartsWith("Get") && m.GetParameters().Length == 1).ToList();
        var setterMethods = staticMethods.Where(m => m.Name.StartsWith("Set") && m.GetParameters().Length == 2).ToList();

        foreach (var getMethod in getterMethods)
        {
            var propName = getMethod.Name.Substring(3); // Remove "Get"
            var setMethod = setterMethods.FirstOrDefault(m => m.Name == $"Set{propName}");

            var propXamlType = ResolveTypeForReflectionType(getMethod.ReturnType, declaringXamlType.Namespace);
            if (propXamlType != null)
            {
                properties.Add(new XamlProperty(propName, declaringXamlType, propXamlType, getMethod, setMethod));
            }
        }

        return properties;
    }

    private IXamlType? ResolveTypeForReflectionType(Type clrType, IXamlNamespace fallbackNs)
    {
        var nsUri = GetNamespaceUriForType(clrType, fallbackNs);
        if (!_namespaces.ContainsKey(nsUri))
        {
            var asmName = clrType.Assembly.GetName().Name ?? "";
            var clrNs = clrType.Namespace ?? "";
            var dynamicNs = new XamlNamespace(nsUri, "local", new[] { asmName }, new[] { clrNs });
            RegisterNamespace(dynamicNs);
        }
        return ResolveType(nsUri, clrType.Name);
    }
}
