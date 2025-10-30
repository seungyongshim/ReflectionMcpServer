# .NET Reflection CLI Tool

.NET 어셈블리를 리플렉션으로 분석하는 CLI 도구입니다.

## 기능

### 1. `method` - 메서드 시그니처 조회
특정 메서드의 시그니처, 파라미터, 반환 타입을 조회합니다.

**사용법:**
```powershell
dotnet run -- method <assembly> <methodName> [typeName]
```

**Examples:**
```powershell
# SpawnNamed 메서드를 모든 타입에서 검색
dotnet run -- method Proto.Actor.dll SpawnNamed

# 특정 타입에서만 검색
dotnet run -- method Proto.Actor.dll SpawnNamed Proto.RootContext
```

### 2. `type` - 타입 정보 조회
타입의 상세 정보(메서드, 프로퍼티, 인터페이스 등)를 조회합니다.

**사용법:**
```powershell
dotnet run -- type <assembly> <typeName>
```

**Example:**
```powershell
dotnet run -- type Proto.Actor.dll Proto.IRootContext
```

### 3. `list` - 타입 목록 조회
어셈블리의 모든 타입을 나열합니다.

**사용법:**
```powershell
dotnet run -- list <assembly> [filter]
```

**Examples:**
```powershell
# 모든 타입 나열
dotnet run -- list Proto.Actor.dll

# RootContext가 포함된 타입만 필터링
dotnet run -- list Proto.Actor.dll RootContext
```

## 설치 및 빌드

```powershell
cd Z:\2025\nexon-ads-nxlogforwarder\ReflectionMcp
dotnet build
```

## 실제 사용 예제

Proto.Actor의 SpawnNamed 메서드 확인:

```powershell
cd Z:\2025\nexon-ads-nxlogforwarder\ReflectionMcp
dotnet run -- method ..\src\NxLogForwarder.Container\bin\Debug\net8.0\Proto.Actor.dll SpawnNamed
```

**출력:**
```
Assembly: Proto.Actor v1.0.0.0
Searching for method: SpawnNamed

╔═══ Found in: Proto.RootContext
║ Modifiers: public virtual
║ Signature: Proto.PID SpawnNamed(Proto.Props, System.String, System.Action`1[Proto.IContext])
║ Parameters:
║   - Props props
║   - String name
║   - Action`1 callback =
║ Return Type: Proto.PID
╚═══
```

이를 통해 `SpawnNamed(Props props, string name, Action<IContext> callback = null)` 메서드 시그니처를 확인할 수 있습니다.

## 팁

- **절대 경로** 또는 **상대 경로** 모두 사용 가능
- 여러 타입에서 동일한 메서드를 찾을 때 매우 유용
- `callback` 파라미터는 선택적(기본값 null)

