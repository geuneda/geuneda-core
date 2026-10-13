# Geuneda Core

다른 Geuneda 패키지가 함께 쓰는 바닥 패키지입니다. 의존성이 없습니다.

| 기능 | 타입 | 네임스페이스 |
|------|------|-------------|
| 모듈별 로거 | `ModuleLogger`, `Log`, `LogLevel`, `LogEntry`, `ILogSink`, `UnityConsoleSink` | `Geuneda.Core` |
| 결과 규약 | `Result`, `Result<T>`, `ResultCodes` | `Geuneda.Core` |

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

## 결과 규약

실행 중 일어날 수 있는 실패는 예외 대신 `Result` 로 돌려주고, 호출자가 코드를 고쳐야 하는 오용은 즉시 예외로 거절합니다.

| 상황 | 처리 |
|------|------|
| 네트워크 오류, 타임아웃, 취소, 서버 거절, 같은 작업이 이미 진행 중, 사용자가 거부 | `Result` 실패 코드로 반환. 예외를 밖으로 던지지 않음 |
| SDK·네트워크 계층이 던진, 정상 실패를 뜻하는 예외(취소, 타임아웃, 연결 실패) | 경계에서 **타입별로** 잡아 코드로 바꿈. 오용·버그 예외까지 잡는 `catch (Exception)` 은 쓰지 않음 |
| null·범위 밖 인자, 초기화 전 호출, 해제된 객체 사용 | 즉시 예외(`ArgumentException`, `InvalidOperationException`, `ObjectDisposedException`). 필요하면 `throw Logger.Fatal(...)` |

```csharp
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Geuneda.Core;

public sealed class ProfileClient
{
    private static readonly ModuleLogger Logger = new ModuleLogger("Game.Profile");

    private bool _isLoading;

    public async Task<Result<Profile>> LoadAsync(string userId, CancellationToken token)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User id is empty", nameof(userId));     // 오용: 즉시 거절
        }

        if (_isLoading)
        {
            return Result<Profile>.Fail(ResultCodes.InProgress);                 // 정상 실패: 코드
        }

        _isLoading = true;
        // 정상 실패에 해당하는 예외만 잡는다. ObjectDisposedException 같은 오용·버그 예외는 그대로 전파한다
        try
        {
            return Result<Profile>.Ok(await FetchAsync(userId, token));
        }
        catch (OperationCanceledException)
        {
            return Result<Profile>.Fail(ResultCodes.Cancelled);                  // 정상 취소: 오류 로그를 남기지 않음
        }
        catch (TimeoutException exception)
        {
            Logger.Warning($"Profile request timed out: {exception.Message}");
            return Result<Profile>.FromException(exception);                      // Timeout
        }
        catch (HttpRequestException exception)
        {
            Logger.Warning($"Profile request failed: {exception.Message}");
            return Result<Profile>.Fail(ResultCodes.NetworkError, exception.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }
}

// 호출하는 쪽
var result = await client.LoadAsync(userId, token);
if (result.TryGetValue(out var profile))
{
    Show(profile);
}
else if (result.Code == ResultCodes.NetworkError || result.Code == ResultCodes.Timeout)
{
    ShowRetryPopup();
}
```

- `IsSuccess` 가 true 면 `Code` 는 `ResultCodes.Success`(0)입니다. 실패 코드는 1 이상이고, `Fail` 에 0 이하를 넣으면 `ArgumentOutOfRangeException` 입니다.
- `default(Result)` 와 `default(Result<T>)` 는 성공이 아니라 `ResultCodes.Unknown` 실패입니다. 채우지 않은 결과가 성공으로 보이지 않게 하기 위해서입니다.
- 실패한 `Result<T>` 의 `Value` 를 읽으면 `InvalidOperationException` 입니다. `IsSuccess` 를 먼저 보거나 `TryGetValue` 를 씁니다.
- `Message` 는 로그와 디버깅용입니다. 사용자에게 보여줄 문구는 `Code` 로 찾습니다.
- `Result<T>` 는 `Result` 로 암시적 변환됩니다(값은 버림). 실패를 값이 없는 결과로 그대로 넘길 때 씁니다. 다른 `T` 로 넘길 때는 `Result<U>.Fail(result.Code, result.Message)` 입니다.
- `Result` 를 돌려주는 API 는 취소도 예외 대신 `ResultCodes.Cancelled` 로 돌려줍니다.
- 정상 실패는 Error 로 남기지 않습니다. 취소는 남기지 않거나 Debug, 네트워크·타임아웃은 Warning 이 기준입니다. Error 와 Fatal 은 기능이 실패했거나 버그일 때 씁니다.
- `ResultCodes.FromException` 과 `Result.FromException` 은 코드로 **바꾸기만** 합니다(`OperationCanceledException` → `Cancelled`, `TimeoutException` → `Timeout`, 그 밖 → `Unknown`). 어떤 예외를 결과로 바꿀지는 catch 로 고릅니다. 모든 예외를 넘기면 오용과 버그가 `Unknown` 으로 숨겨집니다.
- 네트워크 예외는 SDK 마다 종류가 달라 `FromException` 이 분류하지 않습니다. 통신 패키지가 자기 예외를 `NetworkError` 로 바꿉니다.

### 공용 코드 (`ResultCodes`)

| 코드 | 이름 | 뜻 |
|------|------|----|
| 0 | `Success` | 성공 |
| 1 | `Unknown` | 분류하지 않은 실패. 예외 변환의 기본값, `default` 결과 |
| 2 | `Cancelled` | 호출자나 사용자가 취소 |
| 3 | `Timeout` | 정해진 시간 안에 끝나지 않음 |
| 4 | `NetworkError` | 네트워크에 연결할 수 없거나 연결이 끊김 |
| 5 | `InProgress` | 같은 작업이 이미 진행 중이라 거절 |

### 결과 코드 범위

패키지끼리, 그리고 게임과 코드가 겹치지 않도록 범위를 나눕니다.

| 범위 | 소유 |
|------|------|
| 0 | 성공 |
| 1 ~ 999 | core 공용 코드(`ResultCodes`) |
| 1000 단위 블록(1000 ~ 99999) | 패키지별. 패키지가 처음 코드를 정의할 때 다음 빈 블록을 이 표에 등록합니다 |
| 100000 이상 | 게임 |

- 패키지 코드는 패키지의 `<패키지>ResultCodes` static 클래스에 상수로 둡니다. 예: 블록이 1000 이면 `public const int InvalidResponse = 1001;`
- 서버가 내려주는 오류 코드는 통신 패키지가 자기 블록 안의 코드로 옮기거나, 응답 본문에 따로 둡니다.
- 아직 등록된 패키지 블록은 없습니다.

## 요구 사항

- Unity 2021.3 이상
- 의존성 없음. 어셈블리 `Geuneda.Core`

변경 이력은 [CHANGELOG.md](CHANGELOG.md) 를 참고하세요.

## 라이선스

MIT. [LICENSE.md](LICENSE.md) 를 참고하세요.
