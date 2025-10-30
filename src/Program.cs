using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.NET;
using ModelContextProtocol.NET.Server;

// MCP Server for .NET reflection queries
var serverInfo = new Implementation 
{ 
    Name = "Reflection MCP Server", 
    Version = "1.0.0" 
};

var builder = new McpServerBuilder(serverInfo).AddStdioTransport();

// Add reflection tools
builder.Tools.AddFunction(
    name: "get_method_signature",
    description: "Get method signature, parameters, and return type from a .NET assembly",
    parameterTypeInfo: ReflectionJsonContext.Default.GetMethodSignatureParams,
    handler: GetMethodSignature
);

builder.Tools.AddFunction(
    name: "get_type_info",
    description: "Get detailed type information including methods, properties, and interfaces",
    parameterTypeInfo: ReflectionJsonContext.Default.GetTypeInfoParams,
    handler: GetTypeInfo
);

builder.Tools.AddFunction(
    name: "list_types",
    description: "List all types in a .NET assembly with optional filtering",
    parameterTypeInfo: ReflectionJsonContext.Default.ListTypesParams,
    handler: ListTypes
);

var server = builder.Build();
server.Start();
await Task.Delay(-1); // Wait indefinitely

// Tool handler methods
static TextContent GetMethodSignature(GetMethodSignatureParams parameters, CancellationToken ct)
{
    try
    {
        var assembly = Assembly.LoadFrom(parameters.AssemblyPath);
        var result = new StringBuilder();

        result.AppendLine($"Assembly: {assembly.GetName().Name} v{assembly.GetName().Version}");
        result.AppendLine($"Searching for method: {parameters.MethodName}");
        if (!string.IsNullOrEmpty(parameters.TypeName))
        {
            result.AppendLine($"In type: {parameters.TypeName}");
        }
        result.AppendLine();

        var types = string.IsNullOrEmpty(parameters.TypeName)
            ? assembly.GetTypes()
            : [assembly.GetType(parameters.TypeName) ?? throw new TypeLoadException($"Type {parameters.TypeName} not found")];

        var foundAny = false;

        foreach (var type in types)
        {
            if (type == null)
            {
                continue;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.Name == parameters.MethodName);

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
            result.AppendLine($"✗ No method named '{parameters.MethodName}' found.");
        }

        return new TextContent { Text = result.ToString() };
    }
    catch (Exception ex)
    {
        return new TextContent { Text = $"Error: {ex.Message}" };
    }
}

static TextContent GetTypeInfo(GetTypeInfoParams parameters, CancellationToken ct)
{
    try
    {
        var assembly = Assembly.LoadFrom(parameters.AssemblyPath);
        var type = assembly.GetType(parameters.TypeName)
            ?? throw new TypeLoadException($"Type {parameters.TypeName} not found");

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

        return new TextContent { Text = result.ToString() };
    }
    catch (Exception ex)
    {
        return new TextContent { Text = $"Error: {ex.Message}" };
    }
}

static TextContent ListTypes(ListTypesParams parameters, CancellationToken ct)
{
    try
    {
        var assembly = Assembly.LoadFrom(parameters.AssemblyPath);
        var types = assembly.GetTypes()
            .Where(t => string.IsNullOrEmpty(parameters.Filter) || (t.FullName?.Contains(parameters.Filter, StringComparison.OrdinalIgnoreCase) ?? false))
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

        return new TextContent { Text = result.ToString() };
    }
    catch (Exception ex)
    {
        return new TextContent { Text = $"Error: {ex.Message}" };
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

// Parameter classes for MCP tools
public record GetMethodSignatureParams(
    [property: JsonPropertyName("assembly_path")] string AssemblyPath,
    [property: JsonPropertyName("method_name")] string MethodName,
    [property: JsonPropertyName("type_name")] string? TypeName = null
);

public record GetTypeInfoParams(
    [property: JsonPropertyName("assembly_path")] string AssemblyPath,
    [property: JsonPropertyName("type_name")] string TypeName
);

public record ListTypesParams(
    [property: JsonPropertyName("assembly_path")] string AssemblyPath,
    [property: JsonPropertyName("filter")] string? Filter = null
);

// JSON serialization context for NativeAOT compatibility
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(GetMethodSignatureParams))]
[JsonSerializable(typeof(GetTypeInfoParams))]
[JsonSerializable(typeof(ListTypesParams))]
internal partial class ReflectionJsonContext : JsonSerializerContext
{
}
