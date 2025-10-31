using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Testing Roslyn Tools ===\n");

        var testFile = @"Z:\2025\ReflectionMcpServer\test\Calculator.cs";

        // Test 1: List Types
        Console.WriteLine("Test 1: List Types");
        Console.WriteLine("==================");
        var result1 = await RoslynTools.ListTypes(testFile);
        Console.WriteLine(result1);
        Console.WriteLine();

        // Test 2: Get Type Info
        Console.WriteLine("Test 2: Get Type Info for 'Calculator'");
        Console.WriteLine("========================================");
        var result2 = await RoslynTools.GetTypeInfo(testFile, "Calculator");
        Console.WriteLine(result2);
        Console.WriteLine();

        // Test 3: Get Symbol Info
        Console.WriteLine("Test 3: Get Symbol Info for 'Add'");
        Console.WriteLine("===================================");
        var result3 = await RoslynTools.GetSymbolInfo(testFile, "Add");
        Console.WriteLine(result3);
        Console.WriteLine();

        // Test 4: Analyze Code
        Console.WriteLine("Test 4: Analyze Code");
        Console.WriteLine("====================");
        var result4 = await RoslynTools.AnalyzeCode(testFile);
        Console.WriteLine(result4);
        Console.WriteLine();

        Console.WriteLine("=== All Tests Complete ===");
    }
}
