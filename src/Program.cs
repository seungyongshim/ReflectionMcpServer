using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using StreamJsonRpc;

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

builder.Services.AddSingleton<LspClientManager>();

await builder.Build().RunAsync();

// LSP Client Manager
public class LspClientManager : IDisposable
{
    private Process? _lspProcess;
    private JsonRpc? _jsonRpc;
    private bool _isInitialized = false;

    public async Task<JsonRpc> GetOrCreateLspClient()
    {
        if (_jsonRpc != null && _isInitialized)
        {
            return _jsonRpc;
        }

        // Find C# extension path
        var csharpExtPath = FindCSharpExtension();
        if (string.IsNullOrEmpty(csharpExtPath))
        {
            throw new Exception("C# Dev Kit extension not found. Please install it in VS Code.");
        }

        // Start Roslyn language server
        var serverPath = Path.Combine(csharpExtPath, "roslyn", "Microsoft.CodeAnalysis.LanguageServer.dll");
        if (!File.Exists(serverPath))
        {
            throw new Exception($"Language server not found at: {serverPath}");
        }

        _lspProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{serverPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _lspProcess.Start();

        _jsonRpc = JsonRpc.Attach(_lspProcess.StandardInput.BaseStream, _lspProcess.StandardOutput.BaseStream);

        // Initialize LSP
        await InitializeLsp();

        return _jsonRpc;
    }

    private async Task InitializeLsp()
    {
        var initParams = new
        {
            processId = Environment.ProcessId,
            clientInfo = new { name = "ReflectionMcpServer", version = "1.0.0" },
            capabilities = new
            {
                textDocument = new
                {
                    hover = new { contentFormat = new[] { "markdown", "plaintext" } },
                    definition = new { linkSupport = true },
                    references = new { },
                    documentSymbol = new { },
                    completion = new { }
                },
                workspace = new
                {
                    symbol = new { },
                    workspaceFolders = true
                }
            },
            workspaceFolders = new object[] { }
        };

        var result = await _jsonRpc!.InvokeAsync<object>("initialize", initParams);
        await _jsonRpc.NotifyAsync("initialized", new { });
        _isInitialized = true;
    }

    private string? FindCSharpExtension()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var vscodeExtPath = Path.Combine(userProfile, ".vscode", "extensions");

        if (!Directory.Exists(vscodeExtPath))
        {
            return null;
        }

        var csharpDirs = Directory.GetDirectories(vscodeExtPath, "ms-dotnettools.csharp-*")
            .OrderByDescending(d => d)
            .FirstOrDefault();

        return csharpDirs;
    }

    public void Dispose()
    {
        _jsonRpc?.Dispose();
        _lspProcess?.Kill();
        _lspProcess?.Dispose();
    }
}

// Tool implementations
[McpServerToolType]
public static class LspTools
{
    private static LspClientManager? _lspManager;

    public static void SetLspManager(LspClientManager manager)
    {
        _lspManager = manager;
    }
    [McpServerTool(Name = "get_symbol_info"), Description("Get detailed symbol information (class, method, property, etc.) from C# source code using LSP. Provides type information, documentation, signatures, and member details by communicating with C# Dev Kit language server.")]
    public static async Task<string> GetSymbolInfo(
        [Description("Path to the C# source file")] string filePath,
        [Description("Symbol name to search for (class, method, property, etc.)")] string symbolName)
    {
        try
        {
            if (_lspManager == null)
            {
                return "Error: LSP Manager not initialized";
            }

            var lsp = await _lspManager.GetOrCreateLspClient();
            var result = new StringBuilder();

            // Open document
            var fileUri = new Uri(filePath).ToString();
            var text = await File.ReadAllTextAsync(filePath);

            await lsp.NotifyAsync("textDocument/didOpen", new
            {
                textDocument = new
                {
                    uri = fileUri,
                    languageId = "csharp",
                    version = 1,
                    text = text
                }
            });

            // Get document symbols
            var symbols = await lsp.InvokeAsync<object[]>("textDocument/documentSymbol", new
            {
                textDocument = new { uri = fileUri }
            });

            result.AppendLine($"File: {filePath}");
            result.AppendLine($"Searching for: {symbolName}");
            result.AppendLine();

            if (symbols != null && symbols.Length > 0)
            {
                var symbolJson = JsonSerializer.Serialize(symbols, new JsonSerializerOptions { WriteIndented = true });
                result.AppendLine("Symbols found:");
                result.AppendLine(symbolJson);
            }
            else
            {
                result.AppendLine("No symbols found.");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}\n{ex.StackTrace}";
        }
    }

    [McpServerTool(Name = "get_hover_info"), Description("Get hover information (documentation, type info) for a symbol at a specific position in C# source code using LSP. Returns the same information that appears when hovering over code in VS Code with C# Dev Kit.")]
    public static async Task<string> GetHoverInfo(
        [Description("Path to the C# source file")] string filePath,
        [Description("Line number (0-based)")] int line,
        [Description("Character position (0-based)")] int character)
    {
        try
        {
            if (_lspManager == null)
            {
                return "Error: LSP Manager not initialized";
            }

            var lsp = await _lspManager.GetOrCreateLspClient();
            var fileUri = new Uri(filePath).ToString();
            var text = await File.ReadAllTextAsync(filePath);

            await lsp.NotifyAsync("textDocument/didOpen", new
            {
                textDocument = new
                {
                    uri = fileUri,
                    languageId = "csharp",
                    version = 1,
                    text = text
                }
            });

            var hover = await lsp.InvokeAsync<object>("textDocument/hover", new
            {
                textDocument = new { uri = fileUri },
                position = new { line, character }
            });

            var result = new StringBuilder();
            result.AppendLine($"File: {filePath}");
            result.AppendLine($"Position: Line {line}, Character {character}");
            result.AppendLine();

            if (hover != null)
            {
                var hoverJson = JsonSerializer.Serialize(hover, new JsonSerializerOptions { WriteIndented = true });
                result.AppendLine("Hover Information:");
                result.AppendLine(hoverJson);
            }
            else
            {
                result.AppendLine("No hover information available at this position.");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}\n{ex.StackTrace}";
        }
    }

    [McpServerTool(Name = "find_references"), Description("Find all references to a symbol in C# source code using LSP. Shows where a class, method, property, or variable is used throughout the codebase.")]
    public static async Task<string> FindReferences(
        [Description("Path to the C# source file")] string filePath,
        [Description("Line number where the symbol is defined (0-based)")] int line,
        [Description("Character position (0-based)")] int character)
    {
        try
        {
            if (_lspManager == null)
            {
                return "Error: LSP Manager not initialized";
            }

            var lsp = await _lspManager.GetOrCreateLspClient();
            var fileUri = new Uri(filePath).ToString();
            var text = await File.ReadAllTextAsync(filePath);

            await lsp.NotifyAsync("textDocument/didOpen", new
            {
                textDocument = new
                {
                    uri = fileUri,
                    languageId = "csharp",
                    version = 1,
                    text = text
                }
            });

            var references = await lsp.InvokeAsync<object[]>("textDocument/references", new
            {
                textDocument = new { uri = fileUri },
                position = new { line, character },
                context = new { includeDeclaration = true }
            });

            var result = new StringBuilder();
            result.AppendLine($"File: {filePath}");
            result.AppendLine($"Position: Line {line}, Character {character}");
            result.AppendLine();

            if (references != null && references.Length > 0)
            {
                result.AppendLine($"Found {references.Length} reference(s):");
                var refJson = JsonSerializer.Serialize(references, new JsonSerializerOptions { WriteIndented = true });
                result.AppendLine(refJson);
            }
            else
            {
                result.AppendLine("No references found.");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}\n{ex.StackTrace}";
        }
    }

    [McpServerTool(Name = "go_to_definition"), Description("Go to definition of a symbol in C# source code using LSP. Returns the location where a class, method, property, or variable is defined.")]
    public static async Task<string> GoToDefinition(
        [Description("Path to the C# source file")] string filePath,
        [Description("Line number (0-based)")] int line,
        [Description("Character position (0-based)")] int character)
    {
        try
        {
            if (_lspManager == null)
            {
                return "Error: LSP Manager not initialized";
            }

            var lsp = await _lspManager.GetOrCreateLspClient();
            var fileUri = new Uri(filePath).ToString();
            var text = await File.ReadAllTextAsync(filePath);

            await lsp.NotifyAsync("textDocument/didOpen", new
            {
                textDocument = new
                {
                    uri = fileUri,
                    languageId = "csharp",
                    version = 1,
                    text = text
                }
            });

            var definition = await lsp.InvokeAsync<object>("textDocument/definition", new
            {
                textDocument = new { uri = fileUri },
                position = new { line, character }
            });

            var result = new StringBuilder();
            result.AppendLine($"File: {filePath}");
            result.AppendLine($"Position: Line {line}, Character {character}");
            result.AppendLine();

            if (definition != null)
            {
                result.AppendLine("Definition:");
                var defJson = JsonSerializer.Serialize(definition, new JsonSerializerOptions { WriteIndented = true });
                result.AppendLine(defJson);
            }
            else
            {
                result.AppendLine("No definition found.");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}\n{ex.StackTrace}";
        }
    }
}
