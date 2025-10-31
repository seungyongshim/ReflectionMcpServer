# Test MCP Server
$serverPath = "Z:\2025\ReflectionMcpServer\src\bin\Debug\net9.0\ReflectionMcp.dll"

Write-Host "Starting MCP Server..." -ForegroundColor Green

# Initialize message
$initMsg = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
        capabilities = @{}
        clientInfo = @{
            name = "test-client"
            version = "1.0.0"
        }
    }
} | ConvertTo-Json -Depth 10 -Compress

Write-Host "Sending initialize message..." -ForegroundColor Yellow
Write-Host $initMsg

# Start the server process
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "`"$serverPath`""
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi
$process.Start() | Out-Null

# Send initialize message
$process.StandardInput.WriteLine($initMsg)
$process.StandardInput.Flush()

# Wait a bit for response
Start-Sleep -Seconds 2

# Read response
$output = ""
while ($process.StandardOutput.Peek() -ge 0) {
    $output += $process.StandardOutput.ReadLine() + "`n"
}

Write-Host "`nServer Response:" -ForegroundColor Green
Write-Host $output

# Read stderr
$errors = $process.StandardError.ReadToEnd()
if ($errors) {
    Write-Host "`nServer Errors/Logs:" -ForegroundColor Red
    Write-Host $errors
}

# Cleanup
$process.Kill()
$process.Dispose()
