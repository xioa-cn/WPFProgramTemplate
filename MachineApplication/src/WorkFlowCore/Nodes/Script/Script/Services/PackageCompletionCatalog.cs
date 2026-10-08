using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace CsxPad.Wpf.Services;

public sealed class PackageCompletionCatalog
{
    public static PackageCompletionCatalog Empty { get; } = new([], []);

    private readonly IReadOnlyDictionary<string, PackageTypeCompletion> _typesByName;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<PackageTypeCompletion>> _typesBySimpleName;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<PackageMemberCompletion>> _extensionMembersByNamespace;

    private PackageCompletionCatalog(
        IReadOnlyList<PackageTypeCompletion> types,
        IReadOnlyList<string> referencePaths)
    {
        Types = types;
        ReferencePaths = referencePaths;
        Namespaces = types
            .Select(type => type.Namespace)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        _typesByName = types
            .SelectMany(type => new[]
            {
                new KeyValuePair<string, PackageTypeCompletion>(type.FullName, type),
                new KeyValuePair<string, PackageTypeCompletion>(type.Name, type)
            })
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);
        _typesBySimpleName = types
            .GroupBy(type => type.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PackageTypeCompletion>)group
                    .OrderBy(type => type.Namespace, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        _extensionMembersByNamespace = types
            .Where(type => type.Kind == "static class" &&
                           type.Name.EndsWith("Extensions", StringComparison.Ordinal) &&
                           type.Namespace.Length > 0)
            .GroupBy(type => type.Namespace, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PackageMemberCompletion>)group
                    .SelectMany(type => type.Members)
                    .Where(member => member.Kind == "method")
                    .GroupBy(member => member.Name, StringComparer.Ordinal)
                    .Select(memberGroup => memberGroup.First())
                    .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<PackageTypeCompletion> Types { get; }

    public IReadOnlyList<string> Namespaces { get; }

    public IReadOnlyList<string> ReferencePaths { get; }

    public bool TryFindType(string typeName, out PackageTypeCompletion type) =>
        _typesByName.TryGetValue(RemoveGenericArguments(typeName), out type!);

    public IReadOnlyList<PackageTypeCompletion> FindTypes(string simpleName) =>
        _typesBySimpleName.TryGetValue(RemoveGenericArguments(simpleName), out var types) ? types : [];

    public IReadOnlyList<PackageMemberCompletion> GetExtensionMembers(IEnumerable<string> importedNamespaces) =>
        importedNamespaces
            .Distinct(StringComparer.Ordinal)
            .SelectMany(typeNamespace =>
                _extensionMembersByNamespace.TryGetValue(typeNamespace, out var members) ? members : [])
            .GroupBy(member => member.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static PackageCompletionCatalog Load(IEnumerable<string> assemblyPaths)
    {
        var types = new Dictionary<string, PackageTypeCompletion>(StringComparer.Ordinal);
        var referencePaths = assemblyPaths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var assemblyPath in referencePaths)
        {
            ReadAssembly(assemblyPath, types);
        }

        return new PackageCompletionCatalog(types.Values
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(type => type.Namespace, StringComparer.Ordinal)
            .ToArray(), referencePaths);
    }

    private static void ReadAssembly(
        string assemblyPath,
        IDictionary<string, PackageTypeCompletion> destination)
    {
        try
        {
            using var stream = new FileStream(
                assemblyPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var peReader = new PEReader(stream, PEStreamOptions.PrefetchMetadata);
            if (!peReader.HasMetadata)
            {
                return;
            }

            var reader = peReader.GetMetadataReader();
            foreach (var handle in reader.TypeDefinitions)
            {
                var definition = reader.GetTypeDefinition(handle);
                if ((definition.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public)
                {
                    continue;
                }

                var metadataName = reader.GetString(definition.Name);
                if (metadataName.Length == 0 || metadataName[0] == '<')
                {
                    continue;
                }

                var name = RemoveArity(metadataName);
                var typeNamespace = reader.GetString(definition.Namespace);
                var fullName = typeNamespace.Length == 0 ? name : $"{typeNamespace}.{name}";
                var kind = GetTypeKind(reader, definition);
                var members = ReadMembers(reader, definition);
                destination.TryAdd(
                    fullName,
                    new PackageTypeCompletion(name, typeNamespace, fullName, kind, members));
            }
        }
        catch (BadImageFormatException)
        {
            // Native and malformed assets are not useful for C# completion.
        }
        catch (IOException)
        {
            // A package may be replaced while its metadata is being refreshed.
        }
    }

    private static IReadOnlyList<PackageMemberCompletion> ReadMembers(
        MetadataReader reader,
        TypeDefinition definition)
    {
        var members = new Dictionary<string, PackageMemberCompletion>(StringComparer.Ordinal);

        foreach (var handle in definition.GetMethods())
        {
            var method = reader.GetMethodDefinition(handle);
            if ((method.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public ||
                (method.Attributes & MethodAttributes.SpecialName) != 0)
            {
                continue;
            }

            var name = reader.GetString(method.Name);
            members.TryAdd(
                $"method:{name}:{method.Attributes.HasFlag(MethodAttributes.Static)}",
                new PackageMemberCompletion(
                    name,
                    "method",
                    method.GetGenericParameters().Count,
                    method.Attributes.HasFlag(MethodAttributes.Static)));
        }

        foreach (var handle in definition.GetProperties())
        {
            var property = reader.GetPropertyDefinition(handle);
            var accessors = property.GetAccessors();
            if (!IsPublic(reader, accessors.Getter) && !IsPublic(reader, accessors.Setter))
            {
                continue;
            }

            var name = reader.GetString(property.Name);
            var accessorHandle = !accessors.Getter.IsNil ? accessors.Getter : accessors.Setter;
            var isStatic = !accessorHandle.IsNil &&
                           reader.GetMethodDefinition(accessorHandle).Attributes.HasFlag(MethodAttributes.Static);
            members.TryAdd(
                $"property:{name}:{isStatic}",
                new PackageMemberCompletion(name, "property", 0, isStatic));
        }

        return members.Values
            .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsPublic(MetadataReader reader, MethodDefinitionHandle handle)
    {
        if (handle.IsNil)
        {
            return false;
        }

        var attributes = reader.GetMethodDefinition(handle).Attributes;
        return (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
    }

    private static string GetTypeKind(MetadataReader reader, TypeDefinition definition)
    {
        if ((definition.Attributes & TypeAttributes.Interface) != 0)
        {
            return "interface";
        }

        var baseTypeName = GetTypeName(reader, definition.BaseType);
        if (baseTypeName == "Enum")
        {
            return "enum";
        }

        if (baseTypeName == "ValueType")
        {
            return "struct";
        }

        if (baseTypeName is "Delegate" or "MulticastDelegate")
        {
            return "delegate";
        }

        var abstractAndSealed = TypeAttributes.Abstract | TypeAttributes.Sealed;
        return (definition.Attributes & abstractAndSealed) == abstractAndSealed ? "static class" : "class";
    }

    private static string GetTypeName(MetadataReader reader, EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeDefinition => reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
        HandleKind.TypeReference => reader.GetString(reader.GetTypeReference((TypeReferenceHandle)handle).Name),
        _ => string.Empty
    };

    private static string RemoveArity(string name)
    {
        var arityIndex = name.IndexOf('`');
        return arityIndex < 0 ? name : name[..arityIndex];
    }

    private static string RemoveGenericArguments(string typeName)
    {
        var genericIndex = typeName.IndexOf('<');
        return (genericIndex < 0 ? typeName : typeName[..genericIndex]).Trim();
    }
}

public sealed record PackageTypeCompletion(
    string Name,
    string Namespace,
    string FullName,
    string Kind,
    IReadOnlyList<PackageMemberCompletion> Members);

public sealed record PackageMemberCompletion(string Name, string Kind, int GenericArity, bool IsStatic);
