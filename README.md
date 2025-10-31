# C# Roslyn MCP Server

Roslyn (Microsoft.CodeAnalysis)을 사용하여 C# 소스 코드를 분석하는 MCP(Model Context Protocol) 서버입니다.

## 개요

이 MCP 서버는 Roslyn API를 활용하여 C# 소스 코드 및 프로젝트를 분석하고, 타입 정보, 심볼 정보, 코드 진단, NuGet 패키지 탐색 등을 제공합니다. Claude Desktop이나 다른 MCP 클라이언트와 함께 사용하여 C# 코드베이스를 효율적으로 이해하고 분석할 수 있습니다.

## 제공 기능

### 1. `list_types` - 타입 목록 조회
C# 소스 파일에 정의된 모든 타입(클래스, 인터페이스, 열거형, 구조체)을 나열합니다.

**파라미터:**
- `filePath` (필수): C# 소스 파일 경로

**예제 출력:**
```yaml
file: Calculator.cs
types:
  - public class: Calculator
  - public enum: CalculationType
```

### 2. `get_symbol_info` - 심볼 정보 조회
C# 소스 파일에서 특정 심볼(클래스, 메서드, 프로퍼티 등)의 상세 정보를 조회합니다.

**파라미터:**
- `filePath` (필수): C# 소스 파일 경로
- `symbolName` (필수): 검색할 심볼 이름 (대소문자 무시, 부분 일치)

**예제 출력:**
```yaml
- kind: Method
  name: Add
  fullName: Calculator.Add(int, int)
  returnType: int
  parameters:
    - int a
    - int b
```

### 3. `get_type_info` - 타입 정보 조회
타입의 상세 정보(메서드, 프로퍼티, 필드, 상속 관계 등)를 조회합니다.

**파라미터:**
- `filePath` (필수): C# 소스 파일 경로
- `typeName` (필수): 검색할 타입 이름 (대소문자 무시, 부분 일치)

**예제 출력:**
```yaml
- name: Calculator
  fullName: Calculator
  kind: Class
  namespace: TestApp
  accessibility: Public
  methods:
    - int Add(int a, int b)
    - int Subtract(int a, int b)
  properties:
    - string Name { get set }
```

### 4. `analyze_code` - 코드 분석
C# 코드의 구문 오류, 경고, 컴파일 진단 정보를 분석합니다.

**파라미터:**
- `filePath` (필수): C# 소스 파일 경로

**예제 출력:**
```yaml
file: Calculator.cs
errors: 0
warnings: 2
warningList:
  - [10,5] CS0168: The variable 'temp' is declared but never used
  - [15,12] CS0219: The variable 'result' is assigned but its value is never used
```

### 5. `analyze_project` - 프로젝트 분석
.csproj 파일을 기반으로 전체 프로젝트를 분석합니다. NuGet 패키지를 포함한 모든 참조를 로드하여 프로젝트 전체 컨텍스트에서 심볼 검색이 가능합니다.

**파라미터:**
- `projectPath` (필수): .csproj 파일 경로
- `symbolName` (선택): 검색할 심볼 이름

**예제 출력:**
```yaml
project: ReflectionMcp
files: 1
references: 224
symbols:
  - kind: NamedType
    name: RoslynTools
    fullName: RoslynTools
    assembly: ReflectionMcp
    typeKind: Class
```

### 6. `find_nuget_symbol` - NuGet 패키지에서 심볼 찾기
프로젝트가 참조하는 NuGet 패키지에서 타입이나 메서드를 찾습니다. 외부 라이브러리 API를 탐색할 때 유용합니다.

**파라미터:**
- `projectPath` (필수): .csproj 파일 경로
- `symbolName` (필수): 검색할 타입/메서드의 전체 또는 부분 이름

**예제 출력:**
```yaml
Searching for: IHost
In project: ReflectionMcp

found: 15
showing: 15
kinds: [Interface:5, Class:3, Method:7]
symbols:
  - assembly: Microsoft.Extensions.Hosting.Abstractions
    items:
      - kind: Interface
        name: Microsoft.Extensions.Hosting.IHost
        base: System.IDisposable
      - kind: Method
        name: Microsoft.Extensions.Hosting.IHost.Start()
        returnType: void
```

## 설치 방법

### 방법 1: dnx로 바로 실행 (.NET 10+, 권장)

.NET 10 이상에서는 `dnx` 명령어로 설치 없이 바로 실행할 수 있습니다:

```powershell
dnx ReflectionMcpServer
```

특정 버전 지정:
```powershell
dnx ReflectionMcpServer@1.0.0
```

### 방법 2: .NET 도구로 설치

전역 도구로 설치하여 어디서든 사용:

```powershell
dotnet tool install -g ReflectionMcpServer
```

업데이트:
```powershell
dotnet tool update -g ReflectionMcpServer
```

### 방법 3: 소스에서 빌드

```powershell
git clone https://github.com/seungyongshim/ReflectionMcpServer.git
cd ReflectionMcpServer/src
dotnet build -c Release
```

## Claude Desktop 설정

Claude Desktop에서 이 MCP 서버를 사용하려면 설정 파일을 수정해야 합니다.

**설정 파일 위치:**
- Windows: `%APPDATA%\Claude\claude_desktop_config.json`
- macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`

### dnx 사용 (.NET 10+, 권장)

```json
{
  "mcpServers": {
    "reflection": {
      "command": "dnx",
      "args": ["ReflectionMcpServer", "--yes"]
    }
  }
}
```

### 전역 도구로 설치한 경우

```json
{
  "mcpServers": {
    "reflection": {
      "command": "reflection-mcp"
    }
  }
}
```

### 소스에서 빌드한 경우

개발/디버깅용으로 프로젝트에서 직접 실행:

```json
{
  "mcpServers": {
    "reflection": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "Z:\\2025\\ReflectionMcpServer\\src\\ReflectionMcp.csproj"
      ]
    }
  }
}
```

또는 빌드된 실행 파일을 직접 사용:

```json
{
  "mcpServers": {
    "reflection": {
      "command": "Z:\\2025\\ReflectionMcpServer\\src\\bin\\Release\\net9.0\\ReflectionMcp.exe"
    }
  }
}
```

**주의:** Windows 경로는 백슬래시를 이스케이프(`\\`)해야 합니다.

## 사용 예제

Claude Desktop에서 이 MCP 서버를 사용하여 다양한 분석 작업을 수행할 수 있습니다:

### 기본 코드 분석

```
Calculator.cs 파일에 있는 모든 타입을 나열해줘
```

```
Calculator 클래스의 상세 정보를 보여줘
```

```
Add 메서드의 정보를 알려줘
```

### 코드 품질 검사

```
Calculator.cs 파일의 코드를 분석해서 오류와 경고를 찾아줘
```

### 프로젝트 레벨 분석

```
ReflectionMcp.csproj 프로젝트를 분석해줘
```

```
프로젝트에서 IHost 인터페이스를 찾아줘
```

### NuGet 패키지 탐색

```
프로젝트가 참조하는 패키지에서 IHostBuilder를 찾아줘
```

```
Microsoft.Extensions.Hosting 패키지에서 사용 가능한 확장 메서드를 보여줘
```

## 테스트

### 단위 테스트 실행

```powershell
cd test/ReflectionMcp.Tests
dotnet test
```

### 통합 테스트

```powershell
cd test/TestRunner
dotnet run
```

### 수동 테스트

PowerShell 스크립트를 사용한 기능 테스트:

```powershell
# Roslyn 기본 기능 테스트
.\test\test_roslyn.ps1

# MCP 서버 통신 테스트
.\test\test_mcp.ps1

# 심볼 방문자 테스트
.\test\test_symbol_visitor.cs
```

## 기술 스택

- **.NET 9.0**: 최신 .NET 런타임
- **ModelContextProtocol 0.4.0-preview.3**: MCP 서버 구현
- **Microsoft.CodeAnalysis.CSharp 4.11.0**: Roslyn 컴파일러 API
- **Microsoft.CodeAnalysis.CSharp.Workspaces 4.11.0**: 작업 공간 및 프로젝트 분석
- **Microsoft.Build.Locator 1.7.8**: MSBuild 위치 확인
- **Microsoft.CodeAnalysis.Workspaces.MSBuild 4.11.0**: MSBuild 프로젝트 로딩
- **stdio transport**: 표준 입출력 기반 통신

## 아키텍처

이 서버는 다음과 같은 계층 구조로 동작합니다:

1. **MCP Server Layer**: stdio를 통해 Claude Desktop과 통신
2. **Tool Layer**: 6개의 도구(메서드)를 통해 기능 제공
3. **Roslyn Layer**: Microsoft.CodeAnalysis API를 사용한 코드 분석
4. **MSBuild Layer**: 프로젝트 및 참조 로딩

## 배포

### NuGet.org 배포

패키지는 GitHub Release 생성 시 자동으로 NuGet.org에 배포됩니다.

**자동 배포 프로세스:**

1. GitHub 저장소에서 **Releases** → **Draft a new release** 클릭
2. 태그 생성: `v1.0.0` (버전 번호)
3. Release 제목과 설명 작성
4. **Publish release** 클릭
5. GitHub Actions가 자동으로 빌드 및 NuGet.org 배포

### 로컬에서 패키지 생성

```powershell
cd src
dotnet pack -c Release -o ../artifacts
```

생성된 패키지:
- `ReflectionMcpServer.{version}.nupkg`: 일반 패키지
- `ReflectionMcpServer.{version}.snupkg`: 심볼 패키지

### GitHub Packages 배포 (선택사항)

GitHub Packages를 통한 배포도 지원됩니다:

```powershell
# GitHub Packages 소스 추가
dotnet nuget add source "https://nuget.pkg.github.com/seungyongshim/index.json" \
  --name github \
  --username YOUR_USERNAME \
  --password YOUR_GITHUB_PAT

# 패키지 설치
dotnet tool install -g ReflectionMcpServer --add-source github
```

**참고:** GitHub PAT(Personal Access Token)은 `read:packages` 권한이 필요합니다.

## 라이선스

MIT License - 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.

## 기여

이슈 제기와 풀 리퀘스트를 환영합니다!

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 관련 링크

- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Roslyn Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/)
- [Claude Desktop](https://claude.ai/download)

