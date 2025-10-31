# C# LSP MCP Server

C# Dev Kit의 Language Server Protocol을 사용하여 C# 소스 코드를 분석하는 MCP(Model Context Protocol) 서버입니다.

## 개요

이 MCP 서버는 C# Dev Kit의 Roslyn Language Server와 통신하여 실시간으로 C# 코드 정보를 조회할 수 있는 도구를 제공합니다. Claude Desktop이나 다른 MCP 클라이언트와 함께 사용하여 C# 소스 코드를 분석할 수 있습니다.

## 사전 요구사항

- VS Code에 C# Dev Kit 확장이 설치되어 있어야 합니다

## 기능

### 1. `get_symbol_info` - 심볼 정보 조회
C# 소스 파일에서 심볼(클래스, 메서드, 프로퍼티 등)의 상세 정보를 조회합니다.

**파라미터:**
- `file_path` (필수): C# 소스 파일 경로
- `symbol_name` (필수): 검색할 심볼 이름

### 2. `get_hover_info` - 호버 정보 조회
특정 위치의 심볼에 대한 호버 정보(문서, 타입 정보)를 조회합니다. VS Code에서 코드 위에 마우스를 올렸을 때 나타나는 정보와 동일합니다.

**파라미터:**
- `file_path` (필수): C# 소스 파일 경로
- `line` (필수): 라인 번호 (0부터 시작)
- `character` (필수): 문자 위치 (0부터 시작)

### 3. `find_references` - 참조 찾기
C# 소스 코드에서 심볼의 모든 참조를 찾습니다.

**파라미터:**
- `file_path` (필수): C# 소스 파일 경로
- `line` (필수): 심볼이 정의된 라인 번호 (0부터 시작)
- `character` (필수): 문자 위치 (0부터 시작)

### 4. `go_to_definition` - 정의로 이동
C# 소스 코드에서 심볼의 정의 위치를 반환합니다.

**파라미터:**
- `file_path` (필수): C# 소스 파일 경로
- `line` (필수): 라인 번호 (0부터 시작)
- `character` (필수): 문자 위치 (0부터 시작)

## 설치 및 사용

### 방법 1: dnx로 바로 실행 (.NET 10+)

.NET 10 이상에서는 `dnx` 명령어로 설치 없이 바로 실행할 수 있습니다:

```powershell
dnx ReflectionMcpServer
```

또는 특정 버전을 지정:

```powershell
dnx ReflectionMcpServer@1.0.0
```

### 방법 2: 전역 도구로 설치

```powershell
dotnet tool install -g ReflectionMcpServer
reflection-mcp
```

### 방법 3: 로컬 빌드 및 실행

```powershell
cd z:\2025\ReflectionMcpServer
dotnet build
```

## Claude Desktop 설정

### 방법 1: dnx 사용 (.NET 10+)

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

### 방법 2: 전역 도구 사용

```json
{
  "mcpServers": {
    "reflection": {
      "command": "reflection-mcp"
    }
  }
}
```

### 방법 3: 로컬 빌드 사용

Claude Desktop의 설정 파일에 다음을 추가하세요:

**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "reflection": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "z:\\2025\\ReflectionMcpServer\\src\\ReflectionMcp.csproj"
      ]
    }
  }
}
```

또는 빌드된 바이너리를 직접 실행:

```json
{
  "mcpServers": {
    "reflection": {
      "command": "z:\\2025\\ReflectionMcpServer\\src\\bin\\Debug\\net9.0\\ReflectionMcp.exe"
    }
  }
}
```

## 사용 예제

Claude Desktop에서 다음과 같이 요청할 수 있습니다:

```
Program.cs 파일에서 Main 메서드의 심볼 정보를 알려줘
```

```
Program.cs 파일의 10번째 줄, 20번째 문자에 있는 심볼의 호버 정보를 보여줘
```

```
MyClass.cs 파일의 5번째 줄, 15번째 문자에 있는 심볼의 모든 참조를 찾아줘
```

## 기술 스택

- .NET 9.0
- ModelContextProtocol 0.4.0-preview.3
- StreamJsonRpc 2.19.27
- stdio transport (표준 입출력 기반 통신)
- Roslyn Language Server (C# Dev Kit)

## 라이선스

MIT

## GitHub Packages 배포 방법

### 자동 배포

GitHub에서 Release를 생성하면 자동으로 GitHub Packages에 배포됩니다:

1. GitHub 저장소에서 **Releases** 클릭
2. **Draft a new release** 클릭
3. 태그 생성: `v1.0.0` (버전 번호 입력)
4. Release 제목과 설명 작성
5. **Publish release** 클릭

Release가 publish되면 자동으로 GitHub Actions가 실행되어 패키지가 배포됩니다.

### GitHub Packages에서 패키지 설치

```powershell
# nuget.config에 GitHub Packages 소스 추가
dotnet nuget add source "https://nuget.pkg.github.com/seungyongshim/index.json" --name github --username USERNAME --password GITHUB_PAT

# 패키지 설치
dotnet add package ReflectionMcpServer
```

**참고**: GitHub PAT(Personal Access Token)이 필요합니다:
- Settings → Developer settings → Personal access tokens → Tokens (classic)
- `read:packages` 권한 필요

### 로컬에서 패키지 생성

```powershell
cd src
dotnet pack --configuration Release --output ../artifacts
```

