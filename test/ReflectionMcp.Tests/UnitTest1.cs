namespace ReflectionMcp.Tests;

public class RoslynToolsTests
{
    private readonly string _testFile = @"Z:\2025\ReflectionMcpServer\test\Calculator.cs";
    private readonly string _testProject = @"Z:\2025\ReflectionMcpServer\src\ReflectionMcp.csproj";

    [Fact]
    public async Task ListTypes_ShouldReturnCalculatorAndInterface()
    {
        // Act
        var result = await RoslynTools.ListTypes(_testFile);

        // Assert
        Assert.Contains("Calculator", result);
        Assert.Contains("IOperation", result);
    }

    [Fact]
    public async Task GetTypeInfo_Calculator_ShouldReturnClassDetails()
    {
        // Act
        var result = await RoslynTools.GetTypeInfo(_testFile, "Calculator");

        // Assert
        Assert.Contains("Calculator", result);
        Assert.Contains("Add", result);
        Assert.Contains("Multiply", result);
        Assert.Contains("LastResult", result);
    }

    [Fact]
    public async Task GetSymbolInfo_Add_ShouldReturnMethodSignature()
    {
        // Act
        var result = await RoslynTools.GetSymbolInfo(_testFile, "Add");

        // Assert
        Assert.Contains("Add", result);
        Assert.Contains("int", result);
        Assert.Contains("Method", result);
    }

    [Fact]
    public async Task AnalyzeProject_ShouldReturnProjectInfo()
    {
        // Act
        var result = await RoslynTools.AnalyzeProject(_testProject);

        // Assert
        Assert.Contains("ReflectionMcp", result);
        Assert.Contains("files:", result);
        Assert.Contains("references:", result);
    }

    [Fact]
    public async Task FindNuGetSymbol_IHost_ShouldReturnHostSymbols()
    {
        // Act
        var result = await RoslynTools.FindNuGetSymbol(_testProject, "IHost");

        // Assert
        Assert.Contains("IHost", result);
        Assert.Contains("Microsoft.Extensions.Hosting", result);
    }

    [Fact]
    public async Task AnalyzeProject_WithSymbolName_ShouldFindRoslynTools()
    {
        // Act
        var result = await RoslynTools.AnalyzeProject(_testProject, "RoslynTools");

        // Assert
        Assert.Contains("RoslynTools", result);
        Assert.Contains("Class", result);
    }
}
