# CSharpSyntaxTree의 모든 정적 메서드를 찾는 스크립트

Add-Type -Path "Z:\2025\ReflectionMcpServer\src\bin\Debug\net9.0\Microsoft.CodeAnalysis.dll"
Add-Type -Path "Z:\2025\ReflectionMcpServer\src\bin\Debug\net9.0\Microsoft.CodeAnalysis.CSharp.dll"

$type = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]

Write-Host "=== CSharpSyntaxTree Static Factory Methods ===" -ForegroundColor Green
Write-Host ""

# 모든 정적 메서드 가져오기
$staticMethods = $type.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)

# 생성 관련 메서드 필터링 (ParseText, Create, Parse 등)
$factoryMethods = $staticMethods | Where-Object { 
    $_.Name -like "*Parse*" -or 
    $_.Name -like "*Create*" 
} | Sort-Object Name

foreach ($method in $factoryMethods) {
    Write-Host "Method: $($method.Name)" -ForegroundColor Cyan
    Write-Host "Return Type: $($method.ReturnType.FullName)"
    
    $parameters = $method.GetParameters()
    if ($parameters.Count -gt 0) {
        Write-Host "Parameters:"
        foreach ($param in $parameters) {
            $paramType = $param.ParameterType.FullName
            if ($param.ParameterType.IsGenericType) {
                $paramType = $param.ParameterType.ToString()
            }
            $optional = if ($param.IsOptional) { " (Optional, Default: $($param.DefaultValue))" } else { "" }
            Write-Host "  - $paramType $($param.Name)$optional"
        }
    } else {
        Write-Host "Parameters: None"
    }
    Write-Host ""
}

Write-Host "=== Total Count: $($factoryMethods.Count) ===" -ForegroundColor Green
