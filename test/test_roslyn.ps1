$testFile = "Z:\2025\ReflectionMcpServer\test\Calculator.cs"
$exePath = "Z:\2025\ReflectionMcpServer\src\bin\Debug\net9.0\ReflectionMcp.dll"

Write-Host "=== Testing Roslyn MCP Server ===" -ForegroundColor Green
Write-Host ""

# Test 1: list_types
Write-Host "Test 1: Listing all types in Calculator.cs" -ForegroundColor Cyan
$initMsg = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/call"
    params = @{
        name = "list_types"
        arguments = @{
            filePath = $testFile
        }
    }
} | ConvertTo-Json -Depth 10 -Compress

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "`"$exePath`""
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi
[void]$process.Start()

Start-Sleep -Milliseconds 500

$process.StandardInput.WriteLine($initMsg)
$process.StandardInput.Flush()

Start-Sleep -Seconds 1

$output = ""
while ($process.StandardOutput.Peek() -ge 0) {
    $line = $process.StandardOutput.ReadLine()
    $output += $line + "`n"
}

if ($output) {
    Write-Host "Response:" -ForegroundColor Yellow
    Write-Host $output
} else {
    Write-Host "No response received" -ForegroundColor Red
    $errors = $process.StandardError.ReadToEnd()
    if ($errors) {
        Write-Host "Errors:" -ForegroundColor Red
        Write-Host $errors
    }
}

$process.Kill()
$process.Dispose()

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Green
