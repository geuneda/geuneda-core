# Geuneda Core

다른 Geuneda 패키지가 함께 쓰는 바닥 패키지입니다. 의존성이 없습니다.

| 기능 | 타입 | 네임스페이스 |
|------|------|-------------|
| 모듈별 로거 | `ModuleLogger`, `Log`, `LogLevel`, `LogEntry`, `ILogSink`, `UnityConsoleSink` | `Geuneda.Core` |
| 결과 규약 | `Result`, `Result<T>`, `ResultCodes` | `Geuneda.Core` |
| 빌드 환경(DEV/QA/PROD) | `AppEnv`, `AppEnvironment` | `Geuneda.Core` |
| 메인 스레드 디스패처 | `MainThreadDispatcher` | `Geuneda.Core` |
| 설정 에셋 | `ConfigAssetLoader`, `ConfigAssetPathAttribute`, `EnvValue<T>` | `Geuneda.Core` |
| 설치 ID, 기기 ID | `IAppIdentity`, `AppIdentity` | `Geuneda.Core` |

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
| 1000 ~ 1999 | `com.geuneda.http`(`HttpResultCodes`) |
| 그 밖의 1000 단위 블록(2000 ~ 99999) | 패키지별. 패키지가 처음 코드를 정의할 때 다음 빈 블록을 이 표에 등록합니다 |
| 100000 이상 | 게임 |

- 패키지 코드는 패키지의 `<패키지>ResultCodes` static 클래스에 상수로 둡니다. 예: 블록이 1000 이면 `public const int InvalidResponse = 1001;`
- 서버가 내려주는 오류 코드는 통신 패키지가 자기 블록 안의 코드로 옮기거나, 응답 본문에 따로 둡니다.
- 다음 빈 블록은 2000 입니다.

## 빌드 환경

앱이 붙는 서버와 설정의 환경(Dev, QA, Prod)은 컴파일 상수입니다. 빌드마다 Scripting Define Symbols(또는 Unity 6 Build Profile 의 Scripting Defines, 빌드 스크립트의 `extraScriptingDefines`)에 많아야 하나를 정의합니다.

| define | `AppEnv.Current` | `AppEnv.Name` |
|--------|------------------|---------------|
| `GEUNEDA_ENV_PROD` | `AppEnvironment.Prod` | `prod` |
| `GEUNEDA_ENV_QA` | `AppEnvironment.QA` | `qa` |
| `GEUNEDA_ENV_DEV` | `AppEnvironment.Dev` | `dev` |
| 없음, 에디터·개발 빌드 | `AppEnvironment.Dev` | `dev` |
| 없음, 릴리스 빌드 | `AppEnvironment.Prod` | `prod` |
| 둘 이상 | 컴파일 오류 | |

```csharp
using Geuneda.Core;

var baseUrl = AppEnv.IsProd ? "https://api.example.com" : "https://api-dev.example.com";
Logger.Info($"env={AppEnv.Name}");

#if !GEUNEDA_ENV_PROD && (UNITY_EDITOR || DEVELOPMENT_BUILD || GEUNEDA_ENV_DEV || GEUNEDA_ENV_QA)
// 운영 빌드에서는 컴파일되지 않는 치트 메뉴
CheatMenu.Register();
#endif
```

- define 이 없는 릴리스 빌드(개발 빌드가 아닌 빌드)는 스토어 빌드로 보고 Prod 입니다. QA 서버를 쓰는 릴리스 빌드는 `GEUNEDA_ENV_QA` 를 꼭 넣으세요. 넣지 않으면 운영 서버에 붙습니다.
- 프로젝트 Scripting Define Symbols 에 넣은 define 은 에디터에도 적용됩니다. 에디터에서 다른 환경을 시험하려면 여기에 넣습니다. 빌드 스크립트의 `extraScriptingDefines` 는 그 플레이어 컴파일에만 적용되어 에디터의 `AppEnv.Current` 는 바뀌지 않습니다.
- `AppEnv.IsProd` 같은 프로퍼티로 분기한 코드는 빌드에 남습니다. 운영 빌드에서 반드시 빠져야 하는 코드는 위 예제처럼 `#if !GEUNEDA_ENV_PROD && (UNITY_EDITOR || DEVELOPMENT_BUILD || GEUNEDA_ENV_DEV || GEUNEDA_ENV_QA)` 로 감쌉니다. 이 조건은 `AppEnv.Current` 가 Prod 가 아닌 경우와 같습니다. define 없는 릴리스 빌드도 Prod 라 `#if !GEUNEDA_ENV_PROD` 만으로는 빠지지 않고, `GEUNEDA_ENV_PROD` 를 넣은 개발 빌드·에디터도 Prod 라 `UNITY_EDITOR || DEVELOPMENT_BUILD` 만으로도 안 됩니다.
- Addressables 콘텐츠 빌드는 개발 빌드 옵션과 `extraScriptingDefines` 없이(Addressables 2.x 는 `ADDRESSABLES_ADD_DEFINES` 를 넣었을 때만 전달) 플레이어 스크립트를 컴파일합니다. 그래서 콘텐츠의 타입 정보는 플레이어와 다른 환경으로 컴파일될 수 있습니다. 직렬화 필드를 환경 define 으로 바꾸지 마세요.
- 실행 중에는 바뀌지 않습니다. 환경마다 다른 값(서버 주소 등)은 코드나 설정 에셋에 두고 `AppEnv.Current` 로 고릅니다.

## 메인 스레드 디스패처

SDK 콜백이나 스레드 풀처럼 다른 스레드에서 받은 작업을 Unity 메인 스레드에서 실행합니다. Unity API 는 대부분 메인 스레드에서만 부를 수 있습니다.

```csharp
using Geuneda.Core;

// SDK 가 백그라운드 스레드에서 부르는 콜백
private void OnPurchaseCompleted(string productId)
{
    MainThreadDispatcher.Post(() => _shopView.ShowReceipt(productId));
}

if (!MainThreadDispatcher.IsMainThread)
{
    // 메인 스레드가 아니면 Unity API 를 직접 부르지 않는다
}
```

- 플레이 세션이 시작될 때(`SubsystemRegistration`) PlayerLoop Update 단계에서 MonoBehaviour `Update` 를 실행하는 `ScriptRunBehaviourUpdate` 바로 앞에 처리 단계를 넣습니다. GameObject 가 필요 없고, 같은 프레임의 `Update` 보다 먼저 실행됩니다. UniTask 처럼 Update 맨 앞에 단계를 넣는 라이브러리와 함께 써도 이 위치는 유지됩니다.
- 한 번 처리할 때 그때까지 쌓인 작업을 넣은 순서대로 모두 실행합니다(프레임당 개수 제한 없음). 처리 중에 넣은 작업은 다음 프레임에 실행합니다.
- 메인 스레드에서 `Post` 해도 바로 실행하지 않고 다음 처리 때 실행합니다. 바로 실행하려면 `IsMainThread` 를 먼저 확인합니다.
- 작업 하나가 예외를 던져도 `Geuneda.Core.MainThreadDispatcher` 로거로 Error 를 남기고 나머지를 계속 실행합니다.
- 플레이 중에 쓰도록 만든 것입니다. 편집 모드에서의 실행은 보장하지 않습니다. 플레이 세션이 시작될 때 이전 세션에 남은 작업은 버리므로, `Post` 는 `AfterAssembliesLoaded` 이후에 합니다.
- `IsMainThread` 는 플레이 세션이 시작될 때 기록한 메인 스레드와 비교합니다. 에디터를 연 뒤 한 번도 플레이하지 않았으면 false 이고, 한 번 기록한 뒤에는 편집 모드에서도 메인 스레드면 true 입니다.
- PlayerLoop 를 통째로 새로 설정하는 코드(`PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop())` 등)가 있으면 처리 단계가 빠집니다. 다른 라이브러리처럼 `GetCurrentPlayerLoop` 를 고쳐 설정하세요.
- UniTask 를 쓰는 코드는 `await UniTask.SwitchToMainThread()` 도 쓸 수 있습니다. 이 디스패처는 외부 의존성 없이 콜백 SDK 를 감쌀 때 씁니다.

## 설정 에셋

게임 ID, 서버 주소 같은 설정은 ScriptableObject 에셋에 두고, 부트스트랩에서 `ConfigAssetLoader` 로 한 번 읽어 services 의 `MainInstaller` 에 바인딩합니다.

```csharp
using Geuneda.Core;
using UnityEngine;

// Assets/Configs/Resources/Geuneda/GameConfig.asset
[ConfigAssetPath("Geuneda/GameConfig")]
[CreateAssetMenu(menuName = "Game/GameConfig")]
public sealed class GameConfig : ScriptableObject, IGameConfig
{
    [SerializeField] private int _gameId;
    [SerializeField] private EnvValue<string> _serverUrl;   // 인스펙터에서 dev/qa/prod 값을 각각 입력

    public int GameId => _gameId;
    public string ServerUrl => _serverUrl.Value;           // 이 빌드의 환경(AppEnv.Current) 값
}

// 부트스트랩
var configs = new ConfigAssetLoader();
MainInstaller.Bind<IGameConfig>(configs.Load<GameConfig>());
```

읽는 순서:

1. 에디터에서만 `<경로>.user` (개인 덮어쓰기, `Editor/Resources` 폴더에 둠)
2. `Resources/<경로>`
3. 생성자에 넘긴 대체 로더 `Func<string, Object>`(예: Addressables 동기 로드). 경로를 그대로 받습니다.

- 경로는 `[ConfigAssetPath("폴더/이름")]`(Resources 기준, 확장자 없이) 값이고, 속성이 없으면 타입 이름입니다.
- 어디에도 없거나 경로의 에셋 타입이 다르면 `Load` 는 `InvalidOperationException` 입니다(프로젝트 구성 오류). 없을 수 있는 설정은 `TryLoad` 를 씁니다.
- 로더는 캐시를 두지 않습니다. 부트스트랩에서 한 번 읽어 바인딩하고, 사용하는 쪽은 바인딩으로 받습니다.
- `.user` 개인 덮어쓰기: `Editor/Resources` 폴더(예: `Assets/Editor/Resources/Geuneda/GameConfig.user.asset`)에 같은 경로로 두면 에디터에서 그 에셋을 먼저 씁니다(Info 로그를 남김). Editor 폴더 안의 Resources 는 에디터에서 `Resources.Load` 로 읽히고 빌드에서는 빠지므로, 빌드 전에 치울 필요가 없습니다. 플레이어에서는 `.user` 를 읽지 않습니다.
- git 에 올리지 않도록 `.gitignore` 에 `*.user.asset` 과 `*.user.asset.meta` 를 넣으세요. 이름은 소문자 `.user` 로 씁니다.
- `.user` 에셋(대소문자 무시)이 `Editor` 폴더 밖에 있거나 `Assets/StreamingAssets` 안에 있으면 빌드 전 훅(`Geuneda.Core.Editor`)이 플레이어 빌드를 실패시킵니다. Resources, 빌드 씬의 참조, Addressables 그룹, StreamingAssets(안의 Editor 폴더까지 그대로 복사) 중 어디로든 빌드 파일에 들어갈 수 있어서입니다. Addressables 콘텐츠 빌드만 따로 할 때는 검사하지 않습니다.
- `EnvValue<T>` 는 dev/qa/prod 값을 모두 담고 `Value` 로 이 빌드의 환경 값을 고릅니다. 세 값이 모두 빌드에 들어가므로 비밀 값(키, 토큰)은 넣지 않습니다.

## 설치 ID 와 기기 ID

```csharp
using Geuneda.Core;

// 부트스트랩(메인 스레드). MainInstaller 는 인터페이스로만 바인딩한다
MainInstaller.Bind<IAppIdentity>(new AppIdentity());

// 사용하는 쪽
var identity = MainInstaller.Resolve<IAppIdentity>();
analytics.SetCommonField("installation_id", identity.InstallId);
```

| ID | 값 | 다시 설치하면 |
|----|----|---------------|
| `InstallId` | 첫 실행에 만든 GUID(32자리 16진수), PlayerPrefs 에 저장 | 새로 만듦. 단 Android 자동 백업·iOS 백업 복원으로 PlayerPrefs 가 되살아나면 이전 값 |
| `DeviceId` (iOS) | 첫 실행에 만든 GUID, 키체인에 저장(`AfterFirstUnlockThisDeviceOnly`) | 유지. 다른 기기로 옮겨지지 않음 |
| `DeviceId` (Android) | `SystemInfo.deviceUniqueIdentifier`(ANDROID_ID 기반, 앱 서명 키별) | 유지. 공장 초기화하면 바뀜 |
| `DeviceId` (에디터, 그 밖) | `SystemInfo.deviceUniqueIdentifier`. 지원하지 않으면 PlayerPrefs 에 저장한 GUID | 플랫폼에 따름 |

- 만들 때 한 번 정해지고 바뀌지 않습니다. PlayerPrefs 와 `SystemInfo` 를 쓰므로 메인 스레드에서 만들어 바인딩합니다.
- iOS 에서 키체인을 읽지 못하면(예: 기기를 한 번도 잠금 해제하지 않은 상태의 백그라운드 실행) 그 실행 동안 `identifierForVendor` 를 쓰고 Warning 을 남깁니다. 기존 값을 잃지 않도록 이때는 새 값을 쓰지 않습니다. 키체인에 쓰지 못해도 같은 방식으로 대체합니다.
- iOS 키체인은 `Runtime/Plugins/iOS/GeunedaKeychain.mm`(Security.framework)을 씁니다. 항목의 서비스 이름은 `<번들 ID>.geuneda` 라 다른 앱과 공유하지 않습니다.
- 다른 회사 앱의 데이터와 연결하는 추적에 쓰지 마세요(iOS 는 ATT 동의 대상). 개인정보 처리방침과 스토어 개인정보 항목에 기기 식별자 수집을 적습니다.

## 요구 사항

- Unity 2021.3 이상
- 의존성 없음. 어셈블리 `Geuneda.Core`

변경 이력은 [CHANGELOG.md](CHANGELOG.md) 를 참고하세요.

## 라이선스

MIT. [LICENSE.md](LICENSE.md) 를 참고하세요.
