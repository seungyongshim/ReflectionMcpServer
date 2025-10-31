# 수정된 find_nuget_symbol 도구 테스트
# CSharpSyntaxTree 검색 시 ParseText, Create 등 정적 메서드가 나오는지 확인

$projectPath = "Z:\2025\ReflectionMcpServer\src\ReflectionMcp.csproj"

Write-Host "=== Testing find_nuget_symbol for CSharpSyntaxTree ===" -ForegroundColor Green
Write-Host ""

# MCP 서버에 요청 보내기
$request = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/call"
    params = @{
        name = "find_nuget_symbol"
        arguments = @{
            projectPath = $projectPath
            symbolName = "CSharpSyntaxTree"
        }
    }
} | ConvertTo-Json -Depth 10

Write-Host "Request:" -ForegroundColor Cyan
Write-Host $request
Write-Host ""

# 서버 실행 및 테스트
$process = Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$projectPath`"" -NoNewWindow -PassThru -RedirectStandardInput "input.txt" -RedirectStandardOutput "output.txt" -RedirectStandardError "error.txt"

# 요청 전송
Set-Content -Path "input.txt" -Value $request

Start-Sleep -Seconds 3

# 결과 읽기
if (Test-Path "output.txt") {
    Write-Host "Response:" -ForegroundColor Yellow
    Get-Content "output.txt" | Write-Host
}

if (Test-Path "error.txt") {
    Write-Host "`nErrors:" -ForegroundColor Red
    Get-Content "error.txt" | Write-Host
}

# 프로세스 종료
if (!$process.HasExited) {
    $process.Kill()
}

# 임시 파일 정리
Remove-Item "input.txt" -ErrorAction SilentlyContinue
Remove-Item "output.txt" -ErrorAction SilentlyContinue
Remove-Item "error.txt" -ErrorAction SilentlyContinue

Write-Host "`n=== Expected: ParseText, Create methods should appear ===" -ForegroundColor Green
