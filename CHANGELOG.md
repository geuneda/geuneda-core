# Changelog
이 패키지의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 형식은 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](http://semver.org/spec/v2.0.0.html)을 따릅니다.

## [0.5.0] - 2026-10-17

**New**:
- 인증 암호화 `AesEncryption`(상태 없는 static): `Encrypt(key, plaintext, associatedData = null)` 와 `Decrypt(key, data, associatedData = null)`(`Result<byte[]>`). 형식 버전 1 은 `버전(1) || IV(16) || AES-256-CBC 암호문(PKCS7) || HMAC-SHA256 태그(32)` 의 encrypt-then-MAC 입니다. 32바이트 키를 HKDF-SHA256 으로 암호 키와 MAC 키로 나누고, 태그에 연관 데이터와 그 길이를 넣으며, 태그를 상수 시간으로 비교한 뒤에만 복호화합니다. UnityEngine 을 참조하지 않아 서버(.NET)가 `Runtime/Cryptography/`, `Runtime/Results/` 를 링크해 같은 형식을 읽고 쓸 수 있습니다. Unity Mono 의 `AesGcm` 은 `PlatformNotSupportedException` 이라 쓰지 않습니다
- 기기 키 `IDeviceKeyProvider`/`DeviceKeyProvider`(`new DeviceKeyProvider()`): `PlatformSecureStore` 에 이름별 무작위 32바이트 키를 만들어 두고 돌려줍니다(`GetOrCreate`). 보안 저장소를 쓸 수 없으면 실패하고 고정 키·평문으로 대신하지 않습니다. 키체인·키스토어가 없는 곳(에디터, Windows, macOS, Linux, WebGL, Android API 22 이하)은 늘 실패합니다. 읽기가 `Failed` 면 새로 만들지 않고, 쓰기에 실패하면 그 항목을 지우며, 새 키는 다시 읽어 확인한 뒤 돌려줍니다. 기기 키로 암호화한 파일은 다른 기기로 옮기면 풀 수 없습니다
- 공용 결과 코드 `InvalidFormat`(6), `UnsupportedVersion`(7), `IntegrityCheckFailed`(8), `SecureStoreUnavailable`(9)
- 결과 코드 범위 표에 `com.geuneda.iap`(7000), `com.geuneda.ads`(8000), `com.geuneda.consent`(9000), `com.geuneda.notice`(10000), `com.geuneda.coupon`(11000), `com.geuneda.webview`(12000) 블록 등록

**Changed**:
- Android 보안 저장소 플러그인(`GeunedaSecureStore.java`): SharedPreferences 디스크 쓰기·삭제가 실패하면 그 항목의 메모리 값을 원래대로 되돌립니다. 전에는 실패한 쓰기의 값이 같은 실행 동안 읽혀, 다음 실행에 없을 값을 쓸 수 있었습니다
- D2 static 허용 목록(`AGENTS.md`)에 gamedata 의 보안 값 변조 알림(`SecureValueMonitor`)과 services 의 `MainInstaller` 를 넣고, 가변 상태가 없는 static 순수 함수는 목록과 관계없음을 적었습니다(문서만)

## [0.4.0] - 2026-10-14

**New**:
- 단조 시계 `IMonotonicClock`/`MonotonicClock`: 기기 시계 변경에 영향받지 않고 줄어들지 않는 경과 시간(`Elapsed`). Android 는 libc `CLOCK_BOOTTIME`, macOS 는 libc `CLOCK_MONOTONIC`, iOS 는 새 플러그인 `GeunedaMonotonicClock.mm`(`clock_gettime_nsec_np(CLOCK_MONOTONIC)`)이라 기기가 잠든 동안에도 흐릅니다. 그 밖의 플랫폼과 네이티브 호출 실패는 `Stopwatch`(`IncludesSleep` 이 false)
- 결과 코드 범위 표에 `com.geuneda.servertime`(6000 ~ 6999) 등록

## [0.3.0] - 2026-10-14

**New**:
- 결정론적 난수 생성기 `Pcg32`(PCG32 XSH-RR): `new Pcg32(seed, stream)`(스트림 번호는 SplitMix64 로 섞어 증가값을 만듦), `NextUInt()`, 편향 없는 범위 `NextUInt(bound)`/`NextInt(min, max)`/`NextIntInclusive(min, max)`(Lemire), O(log n) 건너뛰기 `Advance(delta)`(2^64 를 법으로 해 뒤로도 감), 쓴 출력 수 `Position`. 정수 연산만 쓰고 모두 unchecked 라 서버(.NET)가 파일을 링크해 같은 수열을 재현할 수 있습니다. `default` 값은 `InvalidOperationException`

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
