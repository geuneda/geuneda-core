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

## 8. 설치 ID 와 기기 ID (`Runtime/Identity/`)

- `AppIdentity` 는 인스턴스이고 `IAppIdentity` 로 바인딩합니다(D2, `MainInstaller` 는 인터페이스만 받음). 저장소는 internal `IKeyValueStore`(기본 `PlayerPrefsStore`, 쓰면 바로 `PlayerPrefs.Save`)와 `ISecureStore`(iOS 플레이어만 `IosKeychainStore`, 그 밖은 null)로 주입하고, 테스트는 internal 생성자로 가짜를 넣습니다.
- 기기 ID 에는 `PlatformSecureStore` 를 쓰지 않습니다. Android 키스토어 값은 앱을 지우면 사라지므로 Android 는 ANDROID_ID 기반 `SystemInfo.deviceUniqueIdentifier` 를 씁니다.
- 키체인 읽기 결과가 `Failed` 면 새 값을 쓰지 마세요. 항목이 있는데 못 읽은 것일 수 있어 덮어쓰면 기기 ID 가 바뀝니다. 그 실행만 플랫폼 ID 로 대체합니다.
- 키(`geuneda.core.installId`, `geuneda.core.deviceId`)와 키체인 서비스 이름(`<번들 ID>.geuneda`)을 바꾸면 기존 설치의 ID 가 바뀝니다.

## 9. 보안 저장소 (`Runtime/SecureStorage/`, `Runtime/Plugins/`)

- `PlatformSecureStore` 는 인자(빈 키, null 값, 키·값의 NUL 문자)를 검사한 뒤 플랫폼 구현(internal)에 넘깁니다. NUL 검사를 빼지 마세요. iOS 플러그인이 C 문자열로 다뤄 키가 잘리면 다른 항목을 덮어씁니다. 플랫폼 구현: iOS 플레이어 `IosKeychainStore`, Android 플레이어 API 23 이상 `AndroidKeystoreStore`, 그 밖은 `PlayerPrefsSecureStore`(키 접두사 `geuneda.secure.`). 구현은 실패를 반환값으로 바꾸고 예외를 내보내지 않습니다.
- `GeunedaKeychain.mm` 은 ARC 여부와 관계없이 컴파일되도록 NSString 을 만들지 않고 바이트를 복사합니다. `GeunedaKeychain_Copy` 는 malloc 한 문자열을 돌려주고 IL2CPP 가 해제합니다(길이 0 이면 빈 문자열, 성공 상태에서 결과가 없으면 `errSecInternalError`). `GeunedaKeychain_Set` 은 갱신 후 없으면 추가하고, 추가할 때만 접근성(`AfterFirstUnlockThisDeviceOnly`)을 넣습니다. 함수 이름은 `IosKeychainStore` 의 `DllImport` 와 같아야 합니다. 플러그인 meta 는 iOS 만 켜고 `FrameworkDependencies: Security;` 입니다.
- `GeunedaSecureStore.java`(meta 는 Android 만 켬)는 키스토어 별칭과 SharedPreferences 파일 이름이 모두 `geuneda.core.securestore` 이고, 값은 Base64(IV 12바이트 + 암호문 + 태그 16바이트)이며 저장 키(UTF-8)를 GCM 인증 데이터(AAD)로 넣습니다(항목 간 암호문 교체 검출). 키가 없거나, 형식이 깨졌거나, 태그가 맞지 않는(`AEADBadTagException`) 값은 지우고 null(NotFound)을 돌려줍니다. 그 밖의 예외는 C# 에서 `AndroidJavaException` 으로 잡아 `Failed`/false 입니다. 별칭, 파일 이름, 저장 형식을 바꾸면 기존 값을 읽지 못합니다.
- C# 가 Java 클래스를 이름으로만 부르므로 R8 축소 빌드에서 지워질 수 있습니다. `Editor/AndroidProguardRulesHook.cs`(`#if UNITY_ANDROID`, `IPostGenerateGradleAndroidProject`)가 `AndroidProguardRules.Rule` 을 unityLibrary 의 `proguard-unity.txt`(unityLibrary 의 consumerProguardFiles)에 넣습니다. Java 클래스 이름이나 공개 메서드를 바꾸면 규칙도 바꿉니다.
- 기기 동작(키 분실, 깨진 값, 앱 재시작 뒤 유지, 축소 빌드)은 에디터 테스트로 확인할 수 없습니다. 플랫폼 코드를 바꾸면 iOS 시뮬레이터와 Android 기기(또는 에뮬레이터)에서 다시 확인합니다.

## 10. 결정론적 난수 (`Runtime/Random/`)

- `Pcg32` 의 수열(생성자, `NextUInt` 의 XSH-RR 출력, SplitMix64 스트림, Lemire 범위, `NextIntInclusive` 의 전체 범위 처리)은 저장된 시드·위치와 서버 재현의 호환 불변식입니다. 바꾸면 같은 시드의 수열이 달라지므로 바꾸지 말고 새 타입을 만드세요. `Pcg32Test` 의 기대값(공식 데모 벡터와 Python 참조 구현으로 만든 값)을 고쳐서 테스트를 맞추지 마세요.
- 서버가 이 파일 하나를 링크할 수 있어야 합니다. UnityEngine 이나 core 의 다른 타입을 참조하지 말고, 연산은 `unchecked` 블록에 둡니다(오버플로 검사를 켠 .NET 프로젝트에서 2026-10-14 확인).
- 범위 함수는 인자 검사를 진행 전에 하고, 성공하면 언제나 출력을 하나 이상 씁니다(데이터의 범위 값이 바뀌어도 수열 정렬이 덜 흔들리게). 2의 거듭제곱 마스크 같은 빠른 경로를 넣으면 수열이 바뀝니다.
- 증가값 0 은 `default` 값의 표시로 씁니다. 생성 경로는 언제나 홀수 증가값을 넣습니다.

## 11. 단조 시계 (`Runtime/Time/`)

- `MonotonicClock` 은 Android 플레이어 `CLOCK_BOOTTIME`(7), macOS 에디터·플레이어 `CLOCK_MONOTONIC`(6)을 libc `clock_gettime` 으로, iOS 플레이어는 `Plugins/iOS/GeunedaMonotonicClock.mm`(`clock_gettime_nsec_np(CLOCK_MONOTONIC)`)으로 읽습니다. Darwin 의 `CLOCK_MONOTONIC` 은 잠자기를 포함합니다(포함하지 않는 것은 `CLOCK_UPTIME_RAW`. 2026-10-14 Mac 에서 두 값 차이가 잠든 시간과 같음을 확인). 잠자기를 빼는 시계로 바꾸면 백그라운드에서 돌아온 뒤 서버 시각이 뒤처집니다.
- 플랫폼 분기는 에디터 define(`UNITY_EDITOR_OSX`)을 먼저 봅니다. 에디터에서도 빌드 대상의 `UNITY_ANDROID`/`UNITY_IOS` 가 켜지기 때문입니다.
- `timespec` 필드는 `IntPtr` 입니다(32비트 Android 에서 `time_t`·`long` 이 32비트). iOS 에서 `__Internal` 로 시스템 함수(`clock_gettime_nsec_np`)를 직접 선언하지 마세요. IL2CPP 생성 코드의 선언이 시스템 헤더와 충돌할 수 있어 플러그인 함수 `GeunedaMonotonicClock_Nanoseconds` 를 거칩니다. 플러그인 meta 는 iOS 만 켭니다.
- 네이티브 호출 실패는 생성자에서 한 번 확인해 `Stopwatch` 로 바꿉니다. 같은 인스턴스 안에서 시계를 바꾸면 값이 튀므로 읽을 때 바꾸지 마세요.
- 기기 확인(2026-10-14): Android 13 에뮬레이터 IL2CPP High 코드 제거에서 `IncludesSleep` true, `SystemClock.elapsedRealtime` 과 1ms 안. iOS 27 시뮬레이터에서 true, 값이 Mac `CLOCK_MONOTONIC` 과 같음. 실제 기기의 깊은 잠자기 포함은 플랫폼 문서 근거이고 직접 재지는 않았습니다.

## 12. 테스트

- `Tests/EditMode/` (`Geuneda.Core.Tests.EditMode`). 출력 대상은 기록용 `ILogSink` 로 바꾸고 `TearDown` 에서 되돌립니다. 디스패처 큐는 `ResetState`/`ProcessQueue` 를 직접 불러 확인합니다(InternalsVisibleTo).
- 설정 에셋 실제 Resources 테스트는 `AssetDatabase.GenerateUniqueAssetPath` 로 만든 자기 임시 폴더(`Assets/GeunedaCoreConfigTestTemp*`)에만 에셋을 만들고, 만든 경우에만 `TearDown` 에서 지웁니다. 고정 경로를 지우지 마세요.
- `Tests/PlayMode/` (`Geuneda.Core.Tests.PlayMode`). PlayerLoop 연결과 실제 프레임 처리처럼 플레이 중에만 확인할 수 있는 것.
