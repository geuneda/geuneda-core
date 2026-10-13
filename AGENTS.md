# Geuneda.Core - AI 에이전트 가이드

> **관련 파일**: `CLAUDE.md`는 이 파일을 감쌉니다 — `CLAUDE.md`가 아니라 `AGENTS.md`를 편집하세요. `README.md`는 사용자 대상 문서입니다.

## 1. 패키지 개요
- **패키지**: `com.geuneda.core`
- **Unity**: 2021.3+
- **의존성**: 없음. 다른 Geuneda 패키지(gamedata, services, nativeui, notificationservice 등)가 이 패키지를 참조하는 바닥 계층입니다. 이 패키지는 어떤 Geuneda 패키지도 참조하지 않습니다.

## 2. 규칙

- **static 상태 허용 목록**(geuneda 결정 D2): 로거(`Log`)와 메인 스레드 디스패처(`MainThreadDispatcher`)만 static 가변 상태를 가질 수 있습니다. 그 밖의 서비스는 인터페이스로 만들고 services 의 `MainInstaller` 로 바인딩합니다. 허용 목록을 늘리려면 이 절을 먼저 고칩니다.
- static 가변 상태는 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 에서 기본값으로 되돌립니다. 도메인 리로드를 끈 Enter Play Mode 에서 이전 세션 상태가 남지 않게 하기 위해서입니다.
- 외부 패키지(UniTask, Newtonsoft 등)에 의존하지 않습니다. 필요하면 별도 패키지로 만듭니다.
- 런타임 어셈블리에서 `UnityEditor` 를 참조하지 않습니다.
- 기능 추가 시 `README.md` 기능 표, `CHANGELOG.md`, `package.json` 버전을 함께 갱신하고(첫 릴리스 전에는 버전을 0.1.0 으로 두고 `[Unreleased]` 에 쌓습니다), geuneda-frameworks 메타 레포의 스킬 문서(`claude-skills/geuneda-core/`)도 맞춰 고칩니다.

## 3. 로거 (`Runtime/Logging/`)

| 타입 | 역할 |
|---|---|
| `LogLevel` | `[Flags] byte` 레벨. Verbose 1 ~ Fatal 0x20 |
| `ModuleLogger` | 모듈 하나의 로거. 레벨 필터, `[Caller*]`, `Fatal<T>` 는 같은 예외 반환. `Verbose`/`Debug` 는 `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("GEUNEDA_LOG_DEBUG")]` |
| `Log` | static 출력 대상(`Sink`, volatile)과 로거 목록(약한 참조, 목록이 커지면 정리). `SubsystemRegistration` 에서 `UnityConsoleSink` 로, 살아 있는 로거의 레벨을 `DefaultLevels` 로 초기화 |
| `ILogSink` / `LogEntry` | 출력 계약과 로그 한 건(readonly struct, `in` 으로 전달) |
| `UnityConsoleSink` | `[모듈] 메시지` 를 `Debug.LogFormat(type, LogOption.None, context, "{0}", message)` 로 쓰고 예외는 `Debug.LogException` |

- 출력 대상의 예외는 `ModuleLogger.Write` 에서 잡아 `Debug.LogException` 으로 남깁니다. 로그 호출이 호출자 흐름을 끊으면 안 됩니다.
- 레벨은 `int` 백킹 필드를 `Volatile.Read/Write` 로 읽고 쓰며, `Enable`/`Disable` 은 `Interlocked.CompareExchange` 루프입니다. 일반 자동 프로퍼티로 되돌리지 마세요.
- 새 `ModuleLogger` 는 생성자에서 `Log.Register` 로 등록됩니다(세션 시작 때 레벨 리셋용).
- `ModuleLogger` 안에서는 메서드 이름 `Debug` 때문에 Unity 의 `Debug` 를 `UnityEngine.Debug` 로 적어야 합니다.
- `Verbose`/`Debug` 의 조건부 컴파일은 호출하는 쪽 어셈블리의 심볼로 결정됩니다.

## 4. 결과 규약 (`Runtime/Results/`)

| 타입 | 역할 |
|---|---|
| `Result` | 값 없는 결과. `bool _isSuccess` + `int _code`. `default` 는 `Unknown` 실패가 되도록 `Code` 가 `_code == 0` 인 실패를 `Unknown` 으로 보고함 |
| `Result<T>` | `Result` 와 값을 담음. 실패에서 `Value` 는 `InvalidOperationException`. `Result` 로 암시적 변환 |
| `ResultCodes` | 공용 코드 1~999 와 `FromException`(OCE → Cancelled, TimeoutException → Timeout, 그 밖 Unknown) |

- 규약: 실행 중 일어날 수 있는 실패(네트워크, 타임아웃, 취소, 서버 거절, 중복 실행)는 `Result` 코드로 돌려주고, 오용(잘못된 인자·상태)은 즉시 예외로 거절합니다. 새 패키지의 공개 비동기 API 는 이 규약을 따릅니다.
- `FromException` 은 변환만 합니다. 정상 실패 예외만 타입별로 잡아 넘기고, `catch (Exception)` 으로 오용·버그를 결과로 숨기지 않습니다. 정상 실패(취소 포함)는 Error 로 남기지 않습니다. README 예제는 `Tests/EditMode/Results/ResultConventionTest.cs` 와 같은 모양으로 유지합니다.
- `default` 가 성공으로 보이면 안 됩니다. 성공 여부를 코드 값만으로 판단하도록 바꾸지 마세요.
- 공용 코드는 모든 패키지가 같은 뜻으로 쓰는 것만 추가합니다. 값은 바꾸거나 재사용하지 않습니다(저장·전송된 코드와 어긋남).
- 패키지별 코드 블록은 `README.md` 의 "결과 코드 범위" 표에 등록합니다.

## 5. 빌드 환경 (`Runtime/Environment/`)

- `AppEnv.Current` 는 `#if` 로 정한 const 플래그를 `Resolve` 에 넘기는 읽기 전용 프로퍼티입니다. static 가변 상태가 아닙니다.
- 결정은 `AppEnv.Resolve`(PROD, QA, DEV, 그 밖은 에디터·개발 빌드면 Dev, 릴리스면 Prod)이고, `#if` 로 정한 const 플래그를 넘깁니다. 진리표 테스트는 `AppEnvTest` 입니다.
- `AppEnv.cs` 맨 위 `#error` 는 환경 define 중복만 막습니다. define 없는 릴리스 컴파일을 `#error` 로 막지 마세요. Addressables 콘텐츠 빌드가 개발 빌드 옵션과 추가 define 없이 플레이어 스크립트를 컴파일해 콘텐츠 빌드가 깨집니다(2026-10-13 재현). 운영 빌드에서 코드를 빼는 조건은 `#if !GEUNEDA_ENV_PROD && (UNITY_EDITOR || DEVELOPMENT_BUILD || GEUNEDA_ENV_DEV || GEUNEDA_ENV_QA)` 입니다(`Current != Prod` 와 같음, `AppEnvTest.StripCondition_MatchesNonProd`). `Resolve` 규칙을 바꾸면 이 조건과 문서도 함께 바꾸세요.
- `IsProd` 등은 `const` 가 아니라 프로퍼티입니다. `const` 로 바꾸면 소비 코드에 도달 불가 코드 경고(CS0162)가 납니다.
- define 조합별 컴파일 결과는 `[Explicit]` 테스트 `AppEnvPlayerCompileTest`(플레이어 스크립트 컴파일, 느림)로 확인합니다. 규칙을 바꾸면 `--filter Geuneda.Core.Tests.AppEnvPlayerCompileTest` 로 직접 실행하세요.

## 6. 메인 스레드 디스패처 (`Runtime/Threading/`)

- `MainThreadDispatcher` 는 D2 의 두 번째 static 허용 대상입니다. `SubsystemRegistration` 의 `Initialize` 가 상태를 리셋하고(`ResetState`: 남은 작업 버림, 메인 스레드 ID 기록) PlayerLoop Update 단계의 `ScriptRunBehaviourUpdate` 바로 앞에 `MainThreadDispatcherUpdate` 단계를 넣습니다(없으면 Update 맨 앞). 이전 세션의 같은 단계는 지우고 넣습니다. Update 맨 앞(index 0)에 넣지 마세요. UniTask 가 `AfterAssembliesLoaded` 에서 맨 앞에 자기 단계를 넣어 위치가 바뀝니다(도메인 리로드 끔, 플레이어 빌드).
- 큐는 lock + 리스트 두 개를 바꿔 쓰는 방식입니다. 처리 중에 들어온 작업은 다음 처리로 갑니다. 작업 안에서 `ProcessQueue` 가 다시 불리면 아무것도 하지 않습니다(처리 중인 리스트 보호).
- Percent ActionQueue 의 프레임당 1개 제한은 따르지 않습니다. 처리 시점의 작업을 모두 실행합니다.
- 작업 예외는 잡아서 `ModuleLogger` 로 Error 를 남깁니다. 처리 루프 밖으로 예외를 내보내지 마세요(PlayerLoop 단계가 예외를 던지면 그 프레임의 남은 작업이 밀립니다).
- 런타임 어셈블리라 편집 모드 처리(`EditorApplication.update`)는 넣지 않았습니다.

## 7. 설정 에셋 (`Runtime/Config/`, `Editor/`)

- `ConfigAssetLoader` 는 인스턴스이고 캐시가 없습니다(D2: static 가변 상태 금지). 대체 로더도 생성자로 받습니다. 정적 캐시나 전역 대체 로더를 추가하지 마세요.
- 읽는 순서는 에디터에서만 `.user`, Resources, 대체 로더입니다. `.user` 판단은 컴파일 상수(`UNITY_EDITOR`)이고, 테스트는 internal 생성자로 Resources 함수와 `.user` 여부를 바꿉니다.
- `Geuneda.Core.Editor` 의 `UserConfigAssetBuildGuard` 는 Editor 폴더 밖, 또는 `Assets/StreamingAssets` 안(그 아래 Editor 폴더도 그대로 복사됨)의 `*.user.*`(대소문자 무시)가 있으면 플레이어 빌드를 실패시킵니다. Resources 만 보면 씬 참조나 Addressables 로 들어가는 경우를 놓치므로 범위를 좁히지 마세요. 경로 판단은 `FindUserAssetsThatCanShip` 순수 함수로 테스트합니다. `.user` 는 `Editor/Resources` 에 둡니다(에디터에서 읽히고 빌드에서 빠짐).
- `EnvValue<T>` 는 직렬화 struct 입니다. 필드 이름(`_dev`, `_qa`, `_prod`)을 바꾸면 기존 에셋 값이 사라집니다.

## 8. 테스트

- `Tests/EditMode/` (`Geuneda.Core.Tests.EditMode`). 출력 대상은 기록용 `ILogSink` 로 바꾸고 `TearDown` 에서 되돌립니다. 디스패처 큐는 `ResetState`/`ProcessQueue` 를 직접 불러 확인합니다(InternalsVisibleTo).
- 설정 에셋 실제 Resources 테스트는 `AssetDatabase.GenerateUniqueAssetPath` 로 만든 자기 임시 폴더(`Assets/GeunedaCoreConfigTestTemp*`)에만 에셋을 만들고, 만든 경우에만 `TearDown` 에서 지웁니다. 고정 경로를 지우지 마세요.
- `Tests/PlayMode/` (`Geuneda.Core.Tests.PlayMode`). PlayerLoop 연결과 실제 프레임 처리처럼 플레이 중에만 확인할 수 있는 것.
