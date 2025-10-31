using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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
    [McpServerTool(Name = "get_symbol_info"), Description("Get detailed symbol information (class, method, property, etc.) from C# source code using Roslyn. Provides type information, documentation, signatures, and member details.")]
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

    [McpServerTool(Name = "get_type_info"), Description("Get comprehensive type information from C# source code including all methods, properties, fields, and documentation.")]
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

    [McpServerTool(Name = "list_types"), Description("List all types (classes, interfaces, enums, structs) defined in a C# source file.")]
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

    [McpServerTool(Name = "analyze_code"), Description("Analyze C# code for syntax errors, warnings, and get compilation diagnostics.")]
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
}
