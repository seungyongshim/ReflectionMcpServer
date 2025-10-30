using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using ModelContextProtocol;

// MCP Server for .NET reflection queries
var server = new MCPServer(new ServerOptions(
    serverInfo: new()
    {
        Name = "reflection-mcp-server",
        Version = "1.0.0"
    },
    capabilities: new()
    {
        Tools = new()
    }
));

// Add reflection tools
server.AddTool("get_method_signature", 
    "Get method signature, parameters, and return type from a .NET assembly",
    new
    {
        type = "object",
        properties = new
        {
            assembly_path = new
            {
                type = "string",
                description = "Path to the .NET assembly file"
            },
            method_name = new
            {
                type = "string",
                description = "Name of the method to search for"
            },
            type_name = new
            {
                type = "string",
                description = "Optional: Full name of the type containing the method"
            }
        },
        required = new[] { "assembly_path", "method_name" }
    },
    async (arguments) =>
    {
        var assemblyPath = arguments["assembly_path"]?.ToString() ?? "";
        var methodName = arguments["method_name"]?.ToString() ?? "";
        var typeName = arguments.ContainsKey("type_name") ? arguments["type_name"]?.ToString() : null;
        
        return await Task.FromResult(GetMethodSignature(assemblyPath, methodName, typeName));
    });

server.AddTool("get_type_info", 
    "Get detailed type information including methods, properties, and interfaces",
    new
    {
        type = "object",
        properties = new
        {
            assembly_path = new
            {
                type = "string",
                description = "Path to the .NET assembly file"
            },
            type_name = new
            {
                type = "string",
                description = "Full name of the type to inspect"
            }
        },
        required = new[] { "assembly_path", "type_name" }
    },
    async (arguments) =>
    {
        var assemblyPath = arguments["assembly_path"]?.ToString() ?? "";
        var typeName = arguments["type_name"]?.ToString() ?? "";
        
        return await Task.FromResult(GetTypeInfo(assemblyPath, typeName));
    });

server.AddTool("list_types", 
    "List all types in a .NET assembly with optional filtering",
    new
    {
        type = "object",
        properties = new
        {
            assembly_path = new
            {
                type = "string",
                description = "Path to the .NET assembly file"
            },
            filter = new
            {
                type = "string",
                description = "Optional: Filter types by name (case-insensitive)"
            }
        },
        required = new[] { "assembly_path" }
    },
    async (arguments) =>
    {
        var assemblyPath = arguments["assembly_path"]?.ToString() ?? "";
        var filter = arguments.ContainsKey("filter") ? arguments["filter"]?.ToString() : null;
        
        return await Task.FromResult(ListTypes(assemblyPath, filter));
    });

// Start server with stdio transport
await server.ConnectToStdioAsync();

// Tool handler methods
static string GetMethodSignature(string assemblyPath, string methodName, string? typeName)
{
    try
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        var result = new StringBuilder();

        result.AppendLine($"Assembly: {assembly.GetName().Name} v{assembly.GetName().Version}");
        result.AppendLine($"Searching for method: {methodName}");
        if (!string.IsNullOrEmpty(typeName))
        {
            result.AppendLine($"In type: {typeName}");
        }
        result.AppendLine();

        var types = string.IsNullOrEmpty(typeName)
            ? assembly.GetTypes()
            : [assembly.GetType(typeName) ?? throw new TypeLoadException($"Type {typeName} not found")];

        var foundAny = false;

        foreach (var type in types)
        {
            if (type == null)
            {
                continue;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.Name == methodName);

            foreach (var method in methods)
            {
                foundAny = true;
                result.AppendLine($"╔═══ Found in: {type.FullName}");
                result.AppendLine($"║ Modifiers: {GetModifiers(method)}");
                result.AppendLine($"║ Signature: {method}");
                result.AppendLine($"║ Parameters:");

                foreach (var param in method.GetParameters())
                {
                    var optional = param.IsOptional ? $" = {param.DefaultValue}" : "";
                    result.AppendLine($"║   - {param.ParameterType.Name} {param.Name}{optional}");
                }

                result.AppendLine($"║ Return Type: {method.ReturnType.FullName}");
                result.AppendLine($"╚═══");
                result.AppendLine();
            }
        }

        if (!foundAny)
        {
            result.AppendLine($"✗ No method named '{methodName}' found.");
        }

        return result.ToString();
    }
    catch (Exception ex)
    {
        return $"Error: {ex.Message}";
    }
}

static string GetTypeInfo(string assemblyPath, string typeName)
{
    try
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        var type = assembly.GetType(typeName)
            ?? throw new TypeLoadException($"Type {typeName} not found");

        var result = new StringBuilder();

        result.AppendLine($"╔═══ Type Information");
        result.AppendLine($"║ Full Name: {type.FullName}");
        result.AppendLine($"║ Namespace: {type.Namespace}");
        result.AppendLine($"║ Assembly: {type.Assembly.GetName().Name}");
        result.AppendLine($"║ Is Class: {type.IsClass}");
        result.AppendLine($"║ Is Interface: {type.IsInterface}");
        result.AppendLine($"║ Is Abstract: {type.IsAbstract}");
        result.AppendLine($"║ Is Sealed: {type.IsSealed}");
        result.AppendLine($"╚═══");
        result.AppendLine();

        if (type.BaseType != null)
        {
            result.AppendLine($"Base Type: {type.BaseType.FullName}");
            result.AppendLine();
        }

        var interfaces = type.GetInterfaces();
        if (interfaces.Length > 0)
        {
            result.AppendLine("Interfaces:");
            foreach (var iface in interfaces)
            {
                result.AppendLine($"  - {iface.FullName}");
            }
            result.AppendLine();
        }

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        if (methods.Length > 0)
        {
            result.AppendLine("Methods:");
            foreach (var method in methods)
            {
                var modifiers = GetModifiers(method);
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                result.AppendLine($"  {modifiers} {method.ReturnType.Name} {method.Name}({parameters})");
            }
            result.AppendLine();
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        if (properties.Length > 0)
        {
            result.AppendLine("Properties:");
            foreach (var prop in properties)
            {
                result.AppendLine($"  {prop.PropertyType.Name} {prop.Name}");
            }
        }

        return result.ToString();
    }
    catch (Exception ex)
    {
        return $"Error: {ex.Message}";
    }
}

static string ListTypes(string assemblyPath, string? filter)
{
    try
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        var types = assembly.GetTypes()
            .Where(t => string.IsNullOrEmpty(filter) || (t.FullName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(t => t.FullName);

        var result = new StringBuilder();
        result.AppendLine($"Assembly: {assembly.GetName().Name} v{assembly.GetName().Version}");
        result.AppendLine($"Types found: {types.Count()}");
        result.AppendLine();

        foreach (var type in types)
        {
            var kind = type.IsInterface ? "interface" : type.IsClass ? "class" : type.IsEnum ? "enum" : "struct";
            result.AppendLine($"  {kind,-10} {type.FullName}");
        }

        return result.ToString();
    }
    catch (Exception ex)
    {
        return $"Error: {ex.Message}";
    }
}

static string GetModifiers(MethodInfo method)
{
    var modifiers = new List<string>();

    if (method.IsPublic)
    {
        modifiers.Add("public");
    }
    else if (method.IsPrivate)
    {
        modifiers.Add("private");
    }
    else if (method.IsFamily)
    {
        modifiers.Add("protected");
    }
    else if (method.IsAssembly)
    {
        modifiers.Add("internal");
    }

    if (method.IsStatic)
    {
        modifiers.Add("static");
    }
    if (method.IsAbstract)
    {
        modifiers.Add("abstract");
    }
    if (method.IsVirtual && !method.IsAbstract)
    {
        modifiers.Add("virtual");
    }

    return string.Join(" ", modifiers);
}
