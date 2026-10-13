# Geuneda Core

다른 Geuneda 패키지가 함께 쓰는 바닥 패키지입니다. 의존성이 없습니다.

| 기능 | 타입 | 네임스페이스 |
|------|------|-------------|
| 모듈별 로거 | `ModuleLogger`, `Log`, `LogLevel`, `LogEntry`, `ILogSink`, `UnityConsoleSink` | `Geuneda.Core` |

## 설치

`Packages/manifest.json` 에 추가합니다.

```json
{
  "dependencies": {
    "com.geuneda.core": "https://github.com/geuneda/geuneda-core.git"
  }
}
```

## 모듈별 로거

모듈마다 로거를 하나 두고, 로그에 모듈 이름을 붙여 출력합니다.

```csharp
using System;
using Geuneda.Core;

public sealed class InventoryService
{
    private static readonly ModuleLogger Logger = new ModuleLogger("Game.Inventory");

    public void Add(string itemId, int count)
    {
        Logger.Debug($"Add {itemId} x{count}");    // 기본으로 꺼져 있고 릴리스 빌드에서는 호출이 제거됨

        if (count <= 0)
        {
            throw Logger.Fatal(new ArgumentOutOfRangeException(nameof(count), count, "Count must be positive"));
        }

        Logger.Info($"Added {itemId}");
    }
}
```

### 레벨

| 레벨 | 기본 | 용도 |
|------|------|------|
| `Verbose` | 꺼짐 | 아주 자세한 추적 |
| `Debug` | 꺼짐 | 개발 중 디버깅 |
| `Info` | 켜짐 | 정상 흐름의 주요 사건 |
| `Warning` | 켜짐 | 계속 진행할 수 있는 문제 |
| `Error` | 켜짐 | 기능 실패 |
| `Fatal` | 켜짐 | 더 진행할 수 없는 오류 |

- `LogLevel` 은 비트 플래그입니다. `Enable(LogLevel.Debug | LogLevel.Verbose)`, `Disable(LogLevel.Info)`, `EnabledLevels = LogLevel.Error | LogLevel.Fatal` 로 로거마다 바꿉니다.
- 메시지를 만드는 비용이 크면 `IsEnabled(LogLevel.Info)` 로 먼저 확인하세요.
- `Verbose` 와 `Debug` 는 `[Conditional]` 메서드입니다. 에디터, 개발 빌드(`DEVELOPMENT_BUILD`), 또는 Scripting Define Symbols 에 `GEUNEDA_LOG_DEBUG` 가 있을 때만 컴파일되고, 릴리스 빌드에서는 인자 계산(문자열 보간 포함)까지 호출이 사라집니다.

### 예외

- `Error(exception, message)` 는 예외와 함께 Error 로그를 남깁니다. 메시지를 생략하면 예외 메시지를 씁니다.
- `throw logger.Fatal(exception)` 은 Fatal 로그를 남기고 같은 예외를 돌려줍니다. 던진 예외를 아무도 잡지 않으면 Unity 가 한 번 더 콘솔에 씁니다.

### 출력 대상

- 기본 출력은 `UnityConsoleSink` 입니다. `[모듈] 메시지` 를 Unity 콘솔에 쓰고(Verbose/Debug/Info 는 Log, Warning 은 Warning, Error/Fatal 은 Error), 예외는 `Debug.LogException` 으로 따로 씁니다.
- `Log.Sink` 에 `ILogSink` 를 넣으면 모든 로거의 출력이 바뀝니다. 파일, 원격 수집, 테스트 기록에 씁니다. `Log.ResetSink()` 로 되돌립니다.
- `LogEntry` 에는 레벨, 모듈, 메시지, 예외, 컨텍스트 오브젝트, 호출자 멤버·파일·줄이 들어 있습니다. 호출자 정보는 `[Caller*]` 특성으로 자동으로 채워집니다.
- 출력 대상이 예외를 던져도 로그를 남긴 쪽으로 전파되지 않고 Unity 콘솔에 남습니다. 여러 스레드에서 불릴 수 있으므로 출력 대상은 스레드 안전하게 만드세요.
- 플레이 세션이 시작될 때(`SubsystemRegistration`) 출력 대상과 살아 있는 모든 로거의 레벨을 기본값으로 되돌리므로, 도메인 리로드를 끈 에디터에서도 이전 세션의 설정이 남지 않습니다. 출력 대상과 레벨은 `AfterAssembliesLoaded` 이후에 바꾸세요.
- 레벨은 여러 스레드에서 읽고 바꿔도 됩니다(`Enable`/`Disable` 은 동시 변경을 잃지 않음).

## 요구 사항

- Unity 2021.3 이상
- 의존성 없음. 어셈블리 `Geuneda.Core`

변경 이력은 [CHANGELOG.md](CHANGELOG.md) 를 참고하세요.

## 라이선스

MIT. [LICENSE.md](LICENSE.md) 를 참고하세요.
