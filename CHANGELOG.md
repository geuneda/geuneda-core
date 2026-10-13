# Changelog
이 패키지의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 형식은 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](http://semver.org/spec/v2.0.0.html)을 따릅니다.

## [0.2.0] - 2026-10-14

**New**:
- 보안 저장소 `ISecureStore`(`TryRead`/`TryWrite`/`TryDelete`, 결과 `SecureReadResult`)와 플랫폼 기본 구현 `PlatformSecureStore`: iOS 키체인(`AfterFirstUnlockThisDeviceOnly`), Android API 23 이상은 키스토어 AES-256-GCM 키로 암호화한 SharedPreferences(저장 키를 인증 데이터로 넣음, 복호화할 수 없는 값은 지우고 NotFound), 에디터와 그 밖의 플랫폼은 PlayerPrefs(암호화 없음, 에디터 밖에서는 Warning). 키와 값의 NUL 문자는 예외. Android 네이티브 플러그인 `GeunedaSecureStore.java`
- Android Gradle 프로젝트 생성 훅 `AndroidProguardRulesHook`(`Geuneda.Core.Editor`): R8 축소 빌드에서 Java 플러그인이 지워지지 않게 keep 규칙을 `unityLibrary/proguard-unity.txt` 에 넣음
- 결과 코드 범위 표에 `com.geuneda.account`(3000 ~ 3999) 등록

**Changed**:
- iOS 키체인 플러그인 `GeunedaKeychain.mm` 에 값 갱신(`GeunedaKeychain_Set`)과 삭제(`GeunedaKeychain_Delete`)를 추가하고 추가 전용 `GeunedaKeychain_Add` 를 뺐습니다. 길이 0 인 값도 읽습니다. 기존 항목과 키는 그대로라 기기 ID 는 바뀌지 않습니다

## [0.1.0] - 2026-10-13

**New**:
- 모듈별 로거 `ModuleLogger`: 레벨 비트 플래그(`LogLevel`, 기본 Verbose/Debug 꺼짐), `[Caller*]` 호출자 정보, `throw logger.Fatal(exception)`, 릴리스 빌드에서 Verbose/Debug 호출 제거(`GEUNEDA_LOG_DEBUG` 로 유지 가능)
- 교체 가능한 출력 대상 `Log.Sink`(`ILogSink`, 기본 `UnityConsoleSink`). 플레이 세션 시작 시 출력 대상과 살아 있는 로거의 레벨을 기본값으로 되돌림
- 레벨 읽기·변경은 스레드 안전(`Volatile`, `Interlocked`)
- 결과 규약 `Result`, `Result<T>`: 정상 실패는 코드로 반환, 오용은 즉시 예외. `default` 는 `Unknown` 실패, 실패한 결과의 `Value` 는 `InvalidOperationException`, `FromException` 으로 예외를 코드로 변환
- 빌드 환경 `AppEnv`(`Current`, `IsDev`/`IsQA`/`IsProd`, `Name`)와 `AppEnvironment`(Dev 1, QA 2, Prod 3). 값은 `GEUNEDA_ENV_DEV`/`GEUNEDA_ENV_QA`/`GEUNEDA_ENV_PROD` define 으로 정하고, 없으면 에디터와 개발 빌드는 Dev, 릴리스 빌드는 Prod. 둘 이상 정의하면 컴파일 오류
- 메인 스레드 디스패처 `MainThreadDispatcher`(`Post`, `IsMainThread`): PlayerLoop Update 의 MonoBehaviour Update 바로 앞에서 쌓인 작업을 모두 실행(프레임당 제한 없음), 작업 예외 격리, 플레이 세션 시작 시 남은 작업 리셋
- 설정 에셋 `ConfigAssetLoader`(에디터 전용 `.user` 개인 덮어쓰기 → Resources → 대체 로더 대리자, 캐시 없음), `[ConfigAssetPath]`, 환경별 값 `EnvValue<T>`. `.user` 는 `Editor/Resources` 에 두고(빌드에서 빠짐), 에디터 어셈블리 `Geuneda.Core.Editor` 의 빌드 전 훅이 Editor 폴더 밖이나 StreamingAssets 안의 `*.user` 에셋이 있으면 플레이어 빌드를 실패시킴. `.gitignore` 에 `*.user.asset(.meta)` 추가
- 설치 ID·기기 ID `IAppIdentity`/`AppIdentity`: 설치 ID 는 PlayerPrefs GUID(앱 데이터가 지워지면 새로, 백업 복원 시 이전 값), 기기 ID 는 iOS 키체인 GUID(재설치해도 유지, 읽기 실패 때 덮어쓰지 않고 identifierForVendor 로 대체), 그 밖은 `SystemInfo.deviceUniqueIdentifier`. iOS 네이티브 플러그인 `GeunedaKeychain.mm`
- 공용 결과 코드 `ResultCodes`(0 성공, 1 Unknown, 2 Cancelled, 3 Timeout, 4 NetworkError, 5 InProgress)와 패키지별 1000 단위 코드 범위 규칙(README)
