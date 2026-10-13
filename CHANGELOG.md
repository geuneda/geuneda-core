# Changelog
이 패키지의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 형식은 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](http://semver.org/spec/v2.0.0.html)을 따릅니다.

## [Unreleased]

**New**:
- 모듈별 로거 `ModuleLogger`: 레벨 비트 플래그(`LogLevel`, 기본 Verbose/Debug 꺼짐), `[Caller*]` 호출자 정보, `throw logger.Fatal(exception)`, 릴리스 빌드에서 Verbose/Debug 호출 제거(`GEUNEDA_LOG_DEBUG` 로 유지 가능)
- 교체 가능한 출력 대상 `Log.Sink`(`ILogSink`, 기본 `UnityConsoleSink`). 플레이 세션 시작 시 출력 대상과 살아 있는 로거의 레벨을 기본값으로 되돌림
- 레벨 읽기·변경은 스레드 안전(`Volatile`, `Interlocked`)
- 결과 규약 `Result`, `Result<T>`: 정상 실패는 코드로 반환, 오용은 즉시 예외. `default` 는 `Unknown` 실패, 실패한 결과의 `Value` 는 `InvalidOperationException`, `FromException` 으로 예외를 코드로 변환
- 빌드 환경 `AppEnv`(`Current`, `IsDev`/`IsQA`/`IsProd`, `Name`)와 `AppEnvironment`(Dev 1, QA 2, Prod 3). 값은 `GEUNEDA_ENV_DEV`/`GEUNEDA_ENV_QA`/`GEUNEDA_ENV_PROD` define 으로 정하고, 없으면 에디터와 개발 빌드는 Dev, 릴리스 빌드는 Prod. 둘 이상 정의하면 컴파일 오류
- 메인 스레드 디스패처 `MainThreadDispatcher`(`Post`, `IsMainThread`): PlayerLoop Update 의 MonoBehaviour Update 바로 앞에서 쌓인 작업을 모두 실행(프레임당 제한 없음), 작업 예외 격리, 플레이 세션 시작 시 남은 작업 리셋
- 설정 에셋 `ConfigAssetLoader`(에디터 전용 `.user` 개인 덮어쓰기 → Resources → 대체 로더 대리자, 캐시 없음), `[ConfigAssetPath]`, 환경별 값 `EnvValue<T>`. `.user` 는 `Editor/Resources` 에 두고(빌드에서 빠짐), 에디터 어셈블리 `Geuneda.Core.Editor` 의 빌드 전 훅이 Editor 폴더 밖이나 StreamingAssets 안의 `*.user` 에셋이 있으면 플레이어 빌드를 실패시킴. `.gitignore` 에 `*.user.asset(.meta)` 추가
- 공용 결과 코드 `ResultCodes`(0 성공, 1 Unknown, 2 Cancelled, 3 Timeout, 4 NetworkError, 5 InProgress)와 패키지별 1000 단위 코드 범위 규칙(README)
