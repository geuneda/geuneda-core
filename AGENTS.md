# Geuneda.Core - AI 에이전트 가이드

> **관련 파일**: `CLAUDE.md`는 이 파일을 감쌉니다 — `CLAUDE.md`가 아니라 `AGENTS.md`를 편집하세요. `README.md`는 사용자 대상 문서입니다.

## 1. 패키지 개요
- **패키지**: `com.geuneda.core`
- **Unity**: 2021.3+
- **의존성**: 없음. 다른 Geuneda 패키지(gamedata, services, nativeui, notificationservice 등)가 이 패키지를 참조하는 바닥 계층입니다. 이 패키지는 어떤 Geuneda 패키지도 참조하지 않습니다.

## 2. 규칙

- **static 상태 허용 목록**(geuneda 결정 D2): 로거(`Log`)와 메인 스레드 디스패처만 static 가변 상태를 가질 수 있습니다. 그 밖의 서비스는 인터페이스로 만들고 services 의 `MainInstaller` 로 바인딩합니다. 허용 목록을 늘리려면 이 절을 먼저 고칩니다.
- static 가변 상태는 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 에서 기본값으로 되돌립니다. 도메인 리로드를 끈 Enter Play Mode 에서 이전 세션 상태가 남지 않게 하기 위해서입니다.
- 외부 패키지(UniTask, Newtonsoft 등)에 의존하지 않습니다. 필요하면 별도 패키지로 만듭니다.
- 런타임 어셈블리에서 `UnityEditor` 를 참조하지 않습니다.
- 기능 추가 시 `README.md` 기능 표, `CHANGELOG.md`, `package.json` 버전을 함께 갱신하고, geuneda-frameworks 메타 레포의 스킬 문서(`claude-skills/geuneda-core/`)도 맞춰 고칩니다.

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

## 4. 테스트

- `Tests/EditMode/` (`Geuneda.Core.Tests.EditMode`). 출력 대상은 기록용 `ILogSink` 로 바꾸고 `TearDown` 에서 되돌립니다.
