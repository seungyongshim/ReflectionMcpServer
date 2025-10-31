using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;

// Register MSBuild
if (!MSBuildLocator.IsRegistered)
{
    MSBuildLocator.RegisterDefaults();
}

var projectPath = @"Z:\2025\ReflectionMcpServer\src\ReflectionMcp.csproj";
var symbolName = "CSharpSyntaxTree";

Console.WriteLine($"Testing SymbolVisitor for: {symbolName}");
Console.WriteLine();

using var workspace = MSBuildWorkspace.Create();
var project = await workspace.OpenProjectAsync(projectPath);
var compilation = await project.GetCompilationAsync();

if (compilation == null)
{
    Console.WriteLine("Failed to get compilation");
    return;
}

// Search all referenced assemblies
var foundSymbols = new List<ISymbol>();

foreach (var reference in compilation.References)
{
    if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
    {
        var visitor = new TestSymbolVisitor(symbolName);
        visitor.Visit(assemblySymbol.GlobalNamespace);
        foundSymbols.AddRange(visitor.FoundSymbols);
        
        if (visitor.FoundSymbols.Any() && assemblySymbol.Name.Contains("CodeAnalysis"))
        {
            Console.WriteLine($"Assembly: {assemblySymbol.Name}");
            Console.WriteLine($"Found {visitor.FoundSymbols.Count} symbols");
            foreach (var sym in visitor.FoundSymbols.Take(20))
            {
                Console.WriteLine($"  - {sym.Kind}: {sym.Name} ({sym.ToDisplayString()})");
            }
            Console.WriteLine();
        }
    }
}

Console.WriteLine($"Total found: {foundSymbols.Count}");
Console.WriteLine($"By kind:");
foreach (var group in foundSymbols.GroupBy(s => s.Kind))
{
    Console.WriteLine($"  {group.Key}: {group.Count()}");
}

// Symbol visitor implementation
public class TestSymbolVisitor : SymbolVisitor<object?>
{
    private readonly string _searchTerm;
    private readonly HashSet<ISymbol> _visited = new(SymbolEqualityComparer.Default);
    public List<ISymbol> FoundSymbols { get; } = new();

    public TestSymbolVisitor(string searchTerm)
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
            foreach (var member in symbol.GetMembers())
            {
                // Skip constructors
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

        // Visit nested types
        foreach (var nestedType in symbol.GetTypeMembers())
        {
            nestedType.Accept(this);
        }

        return null;
    }
}
