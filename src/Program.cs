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
                    result.AppendLine($"╔═══ {symbol!.Kind}: {symbol.Name}");
                    result.AppendLine($"║ Full Name: {symbol.ToDisplayString()}");
                    result.AppendLine($"║ Type: {symbol.GetType().Name}");
                    
                    if (symbol is IMethodSymbol method)
                    {
                        result.AppendLine($"║ Return Type: {method.ReturnType}");
                        result.AppendLine($"║ Parameters:");
                        foreach (var param in method.Parameters)
                        {
                            var optional = param.HasExplicitDefaultValue ? $" = {param.ExplicitDefaultValue}" : "";
                            result.AppendLine($"║   - {param.Type} {param.Name}{optional}");
                        }
                    }
                    else if (symbol is IPropertySymbol property)
                    {
                        result.AppendLine($"║ Property Type: {property.Type}");
                        result.AppendLine($"║ Get: {property.GetMethod != null}");
                        result.AppendLine($"║ Set: {property.SetMethod != null}");
                    }
                    else if (symbol is INamedTypeSymbol namedType)
                    {
                        result.AppendLine($"║ Type Kind: {namedType.TypeKind}");
                        result.AppendLine($"║ Base Type: {namedType.BaseType}");
                        result.AppendLine($"║ Interfaces: {string.Join(", ", namedType.Interfaces)}");
                    }
                    
                    var docComment = symbol.GetDocumentationCommentXml();
                    if (!string.IsNullOrEmpty(docComment))
                    {
                        result.AppendLine($"║ Documentation:");
                        result.AppendLine($"║ {docComment}");
                    }
                    
                    result.AppendLine($"╚═══");
                    result.AppendLine();
                }
            }
            else
            {
                result.AppendLine($"✗ No symbol named '{symbolName}' found.");
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

                result.AppendLine($"╔═══ Type: {typeSymbol.Name}");
                result.AppendLine($"║ Full Name: {typeSymbol.ToDisplayString()}");
                result.AppendLine($"║ Kind: {typeSymbol.TypeKind}");
                result.AppendLine($"║ Namespace: {typeSymbol.ContainingNamespace}");
                result.AppendLine($"║ Accessibility: {typeSymbol.DeclaredAccessibility}");
                result.AppendLine($"║ Is Abstract: {typeSymbol.IsAbstract}");
                result.AppendLine($"║ Is Sealed: {typeSymbol.IsSealed}");
                result.AppendLine($"║ Base Type: {typeSymbol.BaseType}");
                
                if (typeSymbol.Interfaces.Any())
                {
                    result.AppendLine($"║ Interfaces:");
                    foreach (var iface in typeSymbol.Interfaces)
                    {
                        result.AppendLine($"║   - {iface}");
                    }
                }

                var members = typeSymbol.GetMembers();
                
                var methods = members.OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary)
                    .ToList();
                if (methods.Any())
                {
                    result.AppendLine($"║");
                    result.AppendLine($"║ Methods ({methods.Count}):");
                    foreach (var method in methods)
                    {
                        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
                        result.AppendLine($"║   {method.DeclaredAccessibility.ToString().ToLower()} {method.ReturnType} {method.Name}({parameters})");
                    }
                }

                var properties = members.OfType<IPropertySymbol>().ToList();
                if (properties.Any())
                {
                    result.AppendLine($"║");
                    result.AppendLine($"║ Properties ({properties.Count}):");
                    foreach (var prop in properties)
                    {
                        var accessors = $"{(prop.GetMethod != null ? "get;" : "")}{(prop.SetMethod != null ? " set;" : "")}";
                        result.AppendLine($"║   {prop.DeclaredAccessibility.ToString().ToLower()} {prop.Type} {prop.Name} {{ {accessors} }}");
                    }
                }

                var fields = members.OfType<IFieldSymbol>().ToList();
                if (fields.Any())
                {
                    result.AppendLine($"║");
                    result.AppendLine($"║ Fields ({fields.Count}):");
                    foreach (var field in fields)
                    {
                        result.AppendLine($"║   {field.DeclaredAccessibility.ToString().ToLower()} {field.Type} {field.Name}");
                    }
                }

                result.AppendLine($"╚═══");
                result.AppendLine();
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

            result.AppendLine($"File: {filePath}");
            result.AppendLine();

            var root = await tree.GetRootAsync();
            var typeDeclarations = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .ToList();

            if (!typeDeclarations.Any())
            {
                return "No types found in file.";
            }

            result.AppendLine($"Found {typeDeclarations.Count} type(s):");
            result.AppendLine();

            foreach (var typeDecl in typeDeclarations)
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (typeSymbol == null) continue;

                var kind = typeSymbol.TypeKind.ToString().ToLower();
                var accessibility = typeSymbol.DeclaredAccessibility.ToString().ToLower();
                var modifiers = new StringBuilder();
                
                if (typeSymbol.IsAbstract && typeSymbol.TypeKind == TypeKind.Class)
                    modifiers.Append("abstract ");
                if (typeSymbol.IsSealed)
                    modifiers.Append("sealed ");

                result.AppendLine($"{accessibility} {modifiers}{kind} {typeSymbol.ToDisplayString()}");
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

            result.AppendLine($"File: {filePath}");
            result.AppendLine();

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
            var infos = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Info).ToList();

            result.AppendLine($"Errors: {errors.Count}");
            result.AppendLine($"Warnings: {warnings.Count}");
            result.AppendLine($"Info: {infos.Count}");
            result.AppendLine();

            if (errors.Any())
            {
                result.AppendLine("═══ ERRORS ═══");
                foreach (var error in errors)
                {
                    var lineSpan = error.Location.GetLineSpan();
                    result.AppendLine($"[{lineSpan.StartLinePosition.Line + 1}:{lineSpan.StartLinePosition.Character + 1}] {error.Id}: {error.GetMessage()}");
                }
                result.AppendLine();
            }

            if (warnings.Any())
            {
                result.AppendLine("═══ WARNINGS ═══");
                foreach (var warning in warnings)
                {
                    var lineSpan = warning.Location.GetLineSpan();
                    result.AppendLine($"[{lineSpan.StartLinePosition.Line + 1}:{lineSpan.StartLinePosition.Character + 1}] {warning.Id}: {warning.GetMessage()}");
                }
                result.AppendLine();
            }

            if (!errors.Any() && !warnings.Any())
            {
                result.AppendLine("✓ No errors or warnings found.");
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

            result.AppendLine($"Project: {project.Name}");
            result.AppendLine($"Language: {project.Language}");
            result.AppendLine($"Files: {project.Documents.Count()}");
            result.AppendLine($"References: {project.MetadataReferences.Count()}");
            result.AppendLine();

            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
            {
                return "Failed to get compilation";
            }

            // List referenced assemblies
            result.AppendLine("Referenced Assemblies:");
            foreach (var reference in compilation.References)
            {
                if (reference is PortableExecutableReference peRef && peRef.Display != null)
                {
                    var assemblyName = Path.GetFileName(peRef.Display);
                    result.AppendLine($"  - {assemblyName}");
                }
            }
            result.AppendLine();

            // Search for symbol if specified
            if (!string.IsNullOrEmpty(symbolName))
            {
                result.AppendLine($"Searching for symbol: {symbolName}");
                result.AppendLine();

                var symbols = compilation.GetSymbolsWithName(
                    name => name.Contains(symbolName, StringComparison.OrdinalIgnoreCase),
                    SymbolFilter.TypeAndMember)
                    .Take(20)
                    .ToList();

                if (symbols.Any())
                {
                    result.AppendLine($"Found {symbols.Count} symbol(s) (showing first 20):");
                    result.AppendLine();

                    foreach (var symbol in symbols)
                    {
                        var assembly = symbol.ContainingAssembly?.Name ?? "Unknown";
                        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "";
                        
                        result.AppendLine($"╔═══ {symbol.Kind}: {symbol.Name}");
                        result.AppendLine($"║ Full Name: {symbol.ToDisplayString()}");
                        result.AppendLine($"║ Assembly: {assembly}");
                        result.AppendLine($"║ Namespace: {ns}");

                        if (symbol is IMethodSymbol method)
                        {
                            result.AppendLine($"║ Return Type: {method.ReturnType}");
                            result.AppendLine($"║ Parameters: {string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"))}");
                        }
                        else if (symbol is IPropertySymbol property)
                        {
                            result.AppendLine($"║ Type: {property.Type}");
                        }
                        else if (symbol is INamedTypeSymbol type)
                        {
                            result.AppendLine($"║ Type Kind: {type.TypeKind}");
                            result.AppendLine($"║ Base Type: {type.BaseType}");
                        }

                        result.AppendLine($"╚═══");
                        result.AppendLine();
                    }
                }
                else
                {
                    result.AppendLine($"✗ No symbols found matching '{symbolName}'");
                }
            }

            // Project diagnostics
            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                .Take(10)
                .ToList();

            if (diagnostics.Any())
            {
                result.AppendLine($"Diagnostics (showing first 10):");
                foreach (var diag in diagnostics)
                {
                    result.AppendLine($"  [{diag.Severity}] {diag.Id}: {diag.GetMessage()}");
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

            result.AppendLine($"Found {foundSymbols.Count} external symbol(s) (showing first 50):");
            result.AppendLine();

            var groupedByAssembly = symbols.GroupBy(s => s.ContainingAssembly?.Name ?? "Unknown");

            foreach (var assemblyGroup in groupedByAssembly.OrderBy(g => g.Key))
            {
                result.AppendLine($"Assembly: {assemblyGroup.Key}");
                result.AppendLine(new string('─', 60));

                foreach (var symbol in assemblyGroup)
                {
                    var kind = symbol.Kind.ToString();
                    if (symbol is INamedTypeSymbol namedType)
                    {
                        kind = $"{namedType.TypeKind}";
                    }
                    
                    result.AppendLine($"  {kind}: {symbol.ToDisplayString()}");
                    
                    if (symbol is IMethodSymbol method && method.Parameters.Any())
                    {
                        foreach (var param in method.Parameters)
                        {
                            result.AppendLine($"    - {param.Type} {param.Name}");
                        }
                    }
                    else if (symbol is INamedTypeSymbol type)
                    {
                        if (type.BaseType != null && type.BaseType.SpecialType != SpecialType.System_Object)
                        {
                            result.AppendLine($"    Base: {type.BaseType}");
                        }
                        if (type.Interfaces.Any())
                        {
                            result.AppendLine($"    Implements: {string.Join(", ", type.Interfaces.Take(3))}");
                        }
                    }
                }
                result.AppendLine();
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
        if (symbol.Name.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase))
        {
            FoundSymbols.Add(symbol);
        }

        // Visit nested types
        foreach (var member in symbol.GetTypeMembers())
        {
            member.Accept(this);
        }

        // Visit methods if searching for method names
        foreach (var member in symbol.GetMembers())
        {
            if (member is IMethodSymbol method && 
                method.Name.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                FoundSymbols.Add(method);
            }
        }

        return null;
    }
}

