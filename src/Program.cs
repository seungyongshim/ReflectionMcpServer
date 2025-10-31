using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// Register MSBuild before creating host
if (!MSBuildLocator.IsRegistered)
{
    MSBuildLocator.RegisterDefaults();
}

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

// Tool implementations using Roslyn
[McpServerToolType]
public static class RoslynTools
{
    [McpServerTool(Name = "get_symbol_info"), Description("ALWAYS USE THIS for analyzing C# (.cs) files. Get detailed symbol information (class, method, property, etc.) from C# source code using Roslyn. Provides type information, documentation, signatures, and member details.")]
    public static async Task<string> GetSymbolInfo(
        [Description("Path to the C# source file")] string filePath,
        [Description("Symbol name to search for (class, method, property, etc.)")] string symbolName)
    {
        try
        {
            var code = await File.ReadAllTextAsync(filePath);
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create("Analysis")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(tree);

            var semanticModel = compilation.GetSemanticModel(tree);
            var result = new StringBuilder();

            result.AppendLine($"File: {filePath}");
            result.AppendLine($"Searching for: {symbolName}");
            result.AppendLine();

            var root = await tree.GetRootAsync();
            var symbols = root.DescendantNodes()
                .Select(node => semanticModel.GetDeclaredSymbol(node))
                .Where(symbol => symbol != null && symbol.Name.Contains(symbolName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (symbols.Any())
            {
                foreach (var symbol in symbols)
                {
                    result.AppendLine($"- kind: {symbol!.Kind}");
                    result.AppendLine($"  name: {symbol.Name}");
                    result.AppendLine($"  fullName: {symbol.ToDisplayString()}");
                    
                    if (symbol is IMethodSymbol method)
                    {
                        result.AppendLine($"  returnType: {method.ReturnType}");
                        if (method.Parameters.Any())
                        {
                            result.AppendLine($"  parameters:");
                            foreach (var param in method.Parameters)
                            {
                                var opt = param.HasExplicitDefaultValue ? $" (default: {param.ExplicitDefaultValue})" : "";
                                result.AppendLine($"    - {param.Type} {param.Name}{opt}");
                            }
                        }
                    }
                    else if (symbol is IPropertySymbol property)
                    {
                        result.AppendLine($"  type: {property.Type}");
                        result.AppendLine($"  accessors: {(property.GetMethod != null ? "get" : "")}{(property.SetMethod != null ? " set" : "")}");
                    }
                    else if (symbol is INamedTypeSymbol namedType)
                    {
                        result.AppendLine($"  typeKind: {namedType.TypeKind}");
                        if (namedType.BaseType != null && namedType.BaseType.SpecialType != SpecialType.System_Object)
                            result.AppendLine($"  base: {namedType.BaseType}");
                        if (namedType.Interfaces.Any())
                            result.AppendLine($"  interfaces: [{string.Join(", ", namedType.Interfaces)}]");
                    }
                }
            }
            else
            {
                result.AppendLine($"error: No symbol '{symbolName}' found");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}\n{ex.StackTrace}";
        }
    }

    [McpServerTool(Name = "get_type_info"), Description("ALWAYS USE THIS for C# (.cs) type analysis. Get comprehensive type information from C# source code including all methods, properties, fields, and documentation.")]
    public static async Task<string> GetTypeInfo(
        [Description("Path to the C# source file")] string filePath,
        [Description("Full or partial type name to search for")] string typeName)
    {
        try
        {
            var code = await File.ReadAllTextAsync(filePath);
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create("Analysis")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(tree);

            var semanticModel = compilation.GetSemanticModel(tree);
            var result = new StringBuilder();

            var root = await tree.GetRootAsync();
            var typeDeclarations = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(t => t.Identifier.Text.Contains(typeName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!typeDeclarations.Any())
            {
                return $"No type containing '{typeName}' found in {filePath}";
            }

            foreach (var typeDecl in typeDeclarations)
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (typeSymbol == null) continue;

                result.AppendLine($"- name: {typeSymbol.Name}");
                result.AppendLine($"  fullName: {typeSymbol.ToDisplayString()}");
                result.AppendLine($"  kind: {typeSymbol.TypeKind}");
                result.AppendLine($"  namespace: {typeSymbol.ContainingNamespace}");
                result.AppendLine($"  accessibility: {typeSymbol.DeclaredAccessibility}");
                if (typeSymbol.IsAbstract) result.AppendLine($"  abstract: true");
                if (typeSymbol.IsSealed) result.AppendLine($"  sealed: true");
                if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
                    result.AppendLine($"  base: {typeSymbol.BaseType}");
                
                if (typeSymbol.Interfaces.Any())
                {
                    result.AppendLine($"  interfaces:");
                    foreach (var iface in typeSymbol.Interfaces)
                        result.AppendLine($"    - {iface}");
                }

                var members = typeSymbol.GetMembers();
                var methods = members.OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary)
                    .ToList();
                if (methods.Any())
                {
                    result.AppendLine($"  methods:");
                    foreach (var method in methods)
                    {
                        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
                        result.AppendLine($"    - {method.ReturnType} {method.Name}({parameters})");
                    }
                }

                var properties = members.OfType<IPropertySymbol>().ToList();
                if (properties.Any())
                {
                    result.AppendLine($"  properties:");
                    foreach (var prop in properties)
                    {
                        var acc = $"{(prop.GetMethod != null ? "get" : "")}{(prop.SetMethod != null ? " set" : "")}";
                        result.AppendLine($"    - {prop.Type} {prop.Name} {{ {acc} }}");
                    }
                }

                var fields = members.OfType<IFieldSymbol>().Where(f => !f.IsImplicitlyDeclared).ToList();
                if (fields.Any())
                {
                    result.AppendLine($"  fields:");
                    foreach (var field in fields)
                        result.AppendLine($"    - {field.Type} {field.Name}");
                }
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}\n{ex.StackTrace}";
        }
    }

    [McpServerTool(Name = "list_types"), Description("ALWAYS USE THIS for C# (.cs) files. List all types (classes, interfaces, enums, structs) defined in a C# source file.")]
    public static async Task<string> ListTypes(
        [Description("Path to the C# source file")] string filePath)
    {
        try
        {
            var code = await File.ReadAllTextAsync(filePath);
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create("Analysis")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(tree);

            var semanticModel = compilation.GetSemanticModel(tree);
            var result = new StringBuilder();

            var root = await tree.GetRootAsync();
            var typeDeclarations = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .ToList();

            if (!typeDeclarations.Any())
            {
                return "types: []";
            }

            result.AppendLine($"file: {filePath}");
            result.AppendLine($"types:");

            foreach (var typeDecl in typeDeclarations)
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (typeSymbol == null) continue;

                var modifiers = new List<string>();
                if (typeSymbol.IsAbstract && typeSymbol.TypeKind == TypeKind.Class) modifiers.Add("abstract");
                if (typeSymbol.IsSealed) modifiers.Add("sealed");
                var mod = modifiers.Any() ? string.Join(" ", modifiers) + " " : "";

                result.AppendLine($"  - {typeSymbol.DeclaredAccessibility.ToString().ToLower()} {mod}{typeSymbol.TypeKind.ToString().ToLower()}: {typeSymbol.ToDisplayString()}");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}\n{ex.StackTrace}";
        }
    }

    [McpServerTool(Name = "analyze_code"), Description("ALWAYS USE THIS for C# (.cs) code analysis. Analyze C# code for syntax errors, warnings, and get compilation diagnostics.")]
    public static async Task<string> AnalyzeCode(
        [Description("Path to the C# source file")] string filePath)
    {
        try
        {
            var code = await File.ReadAllTextAsync(filePath);
            var tree = CSharpSyntaxTree.ParseText(code, path: filePath);
            var compilation = CSharpCompilation.Create("Analysis")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(tree);

            var diagnostics = compilation.GetDiagnostics();
            var result = new StringBuilder();

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();

            result.AppendLine($"file: {filePath}");
            result.AppendLine($"errors: {errors.Count}");
            result.AppendLine($"warnings: {warnings.Count}");

            if (errors.Any())
            {
                result.AppendLine($"errorList:");
                foreach (var error in errors.Take(10))
                {
                    var lineSpan = error.Location.GetLineSpan();
                    result.AppendLine($"  - [{lineSpan.StartLinePosition.Line + 1},{lineSpan.StartLinePosition.Character + 1}] {error.Id}: {error.GetMessage()}");
                }
            }

            if (warnings.Any())
            {
                result.AppendLine($"warningList:");
                foreach (var warning in warnings.Take(10))
                {
                    var lineSpan = warning.Location.GetLineSpan();
                    result.AppendLine($"  - [{lineSpan.StartLinePosition.Line + 1},{lineSpan.StartLinePosition.Character + 1}] {warning.Id}: {warning.GetMessage()}");
                }
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}\n{ex.StackTrace}";
        }
    }

    [McpServerTool(Name = "analyze_project"), Description("REQUIRED for C# projects (.csproj). Analyze an entire C# project including all files and NuGet package references. Provides access to symbols from referenced packages. Use this when working with C# projects.")]
    public static async Task<string> AnalyzeProject(
        [Description("Path to the .csproj file")] string projectPath,
        [Description("Symbol name to search for (optional, searches across all references)")] string? symbolName = null)
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine($"Loading project: {projectPath}");
            result.AppendLine();

            using var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projectPath);

            result.AppendLine($"project: {project.Name}");
            result.AppendLine($"files: {project.Documents.Count()}");
            result.AppendLine($"references: {project.MetadataReferences.Count()}");

            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
            {
                return "error: Failed to get compilation";
            }

            // Search for symbol if specified
            if (!string.IsNullOrEmpty(symbolName))
            {
                var symbols = compilation.GetSymbolsWithName(
                    name => name.Contains(symbolName, StringComparison.OrdinalIgnoreCase),
                    SymbolFilter.TypeAndMember)
                    .Take(20)
                    .ToList();

                if (symbols.Any())
                {
                    result.AppendLine($"symbols:");
                    foreach (var symbol in symbols)
                    {
                        var assembly = symbol.ContainingAssembly?.Name ?? "Unknown";
                        result.AppendLine($"  - kind: {symbol.Kind}");
                        result.AppendLine($"    name: {symbol.Name}");
                        result.AppendLine($"    fullName: {symbol.ToDisplayString()}");
                        result.AppendLine($"    assembly: {assembly}");
                        
                        if (symbol is IMethodSymbol method)
                        {
                            result.AppendLine($"    returnType: {method.ReturnType}");
                            if (method.Parameters.Any())
                                result.AppendLine($"    parameters: {string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"))}");
                        }
                        else if (symbol is INamedTypeSymbol type)
                        {
                            result.AppendLine($"    typeKind: {type.TypeKind}");
                        }
                    }
                }
                else
                {
                    result.AppendLine($"error: No symbols found matching '{symbolName}'");
                }
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}\n{ex.StackTrace}";
        }
    }

    [McpServerTool(Name = "find_nuget_symbol"), Description("REQUIRED for finding C# types/methods in NuGet packages. Find a specific type or method from NuGet packages referenced in a project. Useful for exploring external library APIs. Always use this when searching for types from external libraries.")]
    public static async Task<string> FindNuGetSymbol(
        [Description("Path to the .csproj file")] string projectPath,
        [Description("Full or partial name of the type/method to find")] string symbolName)
    {
        try
        {
            var result = new StringBuilder();

            using var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projectPath);
            var compilation = await project.GetCompilationAsync();
            
            if (compilation == null)
            {
                return "Failed to get compilation";
            }

            result.AppendLine($"Searching for: {symbolName}");
            result.AppendLine($"In project: {project.Name}");
            result.AppendLine();

            // Search all referenced assemblies for matching symbols
            var foundSymbols = new List<ISymbol>();
            
            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
                {
                    var visitor = new SymbolVisitor(symbolName);
                    visitor.Visit(assemblySymbol.GlobalNamespace);
                    foundSymbols.AddRange(visitor.FoundSymbols);
                }
            }

            var symbols = foundSymbols.Take(50).ToList();

            if (!symbols.Any())
            {
                return $"No external symbols found matching '{symbolName}'";
            }

            result.AppendLine($"found: {foundSymbols.Count}");
            result.AppendLine($"showing: {symbols.Count}");
            
            // Debug: show symbol kinds distribution
            var kindCounts = symbols.GroupBy(s => s.Kind).Select(g => $"{g.Key}:{g.Count()}");
            result.AppendLine($"kinds: [{string.Join(", ", kindCounts)}]");
            
            result.AppendLine("symbols:");

            var groupedByAssembly = symbols.GroupBy(s => s.ContainingAssembly?.Name ?? "Unknown");

            foreach (var assemblyGroup in groupedByAssembly.OrderBy(g => g.Key))
            {
                result.AppendLine($"  - assembly: {assemblyGroup.Key}");
                result.AppendLine("    items:");

                foreach (var symbol in assemblyGroup)
                {
                    var kind = symbol.Kind.ToString();
                    if (symbol is INamedTypeSymbol namedType)
                    {
                        kind = $"{namedType.TypeKind}";
                    }
                    
                    result.AppendLine($"      - kind: {kind}");
                    result.AppendLine($"        name: {symbol.ToDisplayString()}");
                    
                    if (symbol is IMethodSymbol method && method.Parameters.Any())
                    {
                        result.AppendLine($"        params: [{string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"))}]");
                    }
                    else if (symbol is INamedTypeSymbol type)
                    {
                        if (type.BaseType != null && type.BaseType.SpecialType != SpecialType.System_Object)
                        {
                            result.AppendLine($"        base: {type.BaseType}");
                        }
                        if (type.Interfaces.Any())
                        {
                            result.AppendLine($"        interfaces: [{string.Join(", ", type.Interfaces.Take(3))}]");
                        }
                    }
                }
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}\n{ex.StackTrace}";
        }
    }
}

// Symbol visitor to recursively search for symbols
public class SymbolVisitor : SymbolVisitor<object?>
{
    private readonly string _searchTerm;
    private readonly HashSet<ISymbol> _visited = new(SymbolEqualityComparer.Default);
    public List<ISymbol> FoundSymbols { get; } = new();

    public SymbolVisitor(string searchTerm)
    {
        _searchTerm = searchTerm;
    }

    public override object? VisitNamespace(INamespaceSymbol symbol)
    {
        foreach (var member in symbol.GetMembers())
        {
            member.Accept(this);
        }
        return null;
    }

    public override object? VisitNamedType(INamedTypeSymbol symbol)
    {
        var typeMatches = symbol.Name.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase);
        
        if (typeMatches)
        {
            // Add type only once
            if (_visited.Add(symbol))
            {
                FoundSymbols.Add(symbol);
            }
            
            // If type name matches, include all its public members
            // This handles cases like searching "CSharpSyntaxTree" to find ParseText, Create, etc.
            foreach (var member in symbol.GetMembers())
            {
                // Skip constructors (same name as type, would be duplicate)
                if (member.Kind == SymbolKind.Method && member is IMethodSymbol methodSym)
                {
                    if (methodSym.MethodKind == MethodKind.Constructor || 
                        methodSym.MethodKind == MethodKind.StaticConstructor)
                        continue;
                }
                
                // Only include public/protected members
                if (member.DeclaredAccessibility == Accessibility.Public || 
                    member.DeclaredAccessibility == Accessibility.Protected)
                {
                    if (_visited.Add(member))
                    {
                        FoundSymbols.Add(member);
                    }
                }
            }
        }
        else
        {
            // Type name doesn't match, search individual members
            foreach (var member in symbol.GetMembers())
            {
                // Skip special methods (constructors, destructors, property accessors, event accessors)
                if (member is IMethodSymbol method)
                {
                    if (method.MethodKind == MethodKind.Constructor ||
                        method.MethodKind == MethodKind.StaticConstructor ||
                        method.MethodKind == MethodKind.Destructor ||
                        method.MethodKind == MethodKind.PropertyGet ||
                        method.MethodKind == MethodKind.PropertySet ||
                        method.MethodKind == MethodKind.EventAdd ||
                        method.MethodKind == MethodKind.EventRemove)
                        continue;
                }
                
                if (member.Name.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase))
                {
                    if (_visited.Add(member))
                    {
                        FoundSymbols.Add(member);
                    }
                }
            }
        }

        // Visit nested types
        foreach (var nestedType in symbol.GetTypeMembers())
        {
            nestedType.Accept(this);
        }

        return null;
    }
}

