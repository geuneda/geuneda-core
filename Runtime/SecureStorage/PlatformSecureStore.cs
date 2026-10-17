using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 플랫폼 기본 보안 저장소입니다. 부트스트랩에서 만들어 services 의 <c>MainInstaller</c> 에
	/// <see cref="ISecureStore"/> 로 바인딩합니다(<c>MainInstaller.Bind&lt;ISecureStore&gt;(new PlatformSecureStore())</c>).
	/// </summary>
	/// <remarks>
	/// <list type="bullet">
	/// <item>iOS: 키체인(<c>AfterFirstUnlockThisDeviceOnly</c>)입니다. 앱을 지워도 남고, 백업을 다른 기기에 복원하면 옮겨지지 않습니다.</item>
	/// <item>
	/// Android(API 23 이상): 키스토어의 AES-256-GCM 키로 암호화해 앱 전용 SharedPreferences 에 둡니다. 앱을 지우면 사라집니다.
	/// 키는 기기 밖으로 나가지 않으므로, 백업에서 복원된 값은 읽을 수 없어 지우고 <see cref="SecureReadResult.NotFound"/> 로 봅니다.
	/// </item>
	/// <item>에디터, 그 밖의 플랫폼, Android API 22 이하: PlayerPrefs 에 암호화 없이 둡니다. 에디터가 아니면 만들 때 Warning 을 남깁니다.</item>
	/// </list>
	/// 키와 값에 NUL 문자(<c>\0</c>)는 쓸 수 없습니다(네이티브 저장소가 C 문자열로 다룸).
	/// </remarks>
	public sealed class PlatformSecureStore : ISecureStore
	{
		private readonly ISecureStore _store = CreatePlatformStore();

		/// <summary>키체인이나 키스토어로 보호하는지입니다. PlayerPrefs 평문 대체(에디터 포함)면 false 입니다.</summary>
		internal bool IsProtected => !(_store is PlayerPrefsSecureStore);

		/// <inheritdoc />
		/// <exception cref="ArgumentException"><paramref name="key"/> 가 null 이거나 빈 문자열이거나 NUL 문자를 담은 경우</exception>
		public SecureReadResult TryRead(string key, out string value)
		{
			ValidateKey(key);
			return _store.TryRead(key, out value);
		}

		/// <inheritdoc />
		/// <exception cref="ArgumentException"><paramref name="key"/> 가 null 이거나 빈 문자열이거나 NUL 문자를 담은 경우, <paramref name="value"/> 가 NUL 문자를 담은 경우</exception>
		/// <exception cref="ArgumentNullException"><paramref name="value"/> 가 null 인 경우</exception>
		public bool TryWrite(string key, string value)
		{
			ValidateKey(key);

			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			if (value.IndexOf('\0') >= 0)
			{
				throw new ArgumentException("Secure store value must not contain NUL characters", nameof(value));
			}

			return _store.TryWrite(key, value);
		}

		/// <inheritdoc />
		/// <exception cref="ArgumentException"><paramref name="key"/> 가 null 이거나 빈 문자열이거나 NUL 문자를 담은 경우</exception>
		public bool TryDelete(string key)
		{
			ValidateKey(key);
			return _store.TryDelete(key);
		}

		private static void ValidateKey(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				throw new ArgumentException("Secure store key must not be empty", nameof(key));
			}

			if (key.IndexOf('\0') >= 0)
			{
				throw new ArgumentException("Secure store key must not contain NUL characters", nameof(key));
			}
		}

		private static ISecureStore CreatePlatformStore()
		{
#if UNITY_EDITOR
			return new PlayerPrefsSecureStore(null);
#elif UNITY_IOS
			return new IosKeychainStore();
#elif UNITY_ANDROID
			return AndroidKeystoreStore.IsSupported()
				? (ISecureStore) new AndroidKeystoreStore()
				: new PlayerPrefsSecureStore("Android Keystore AES needs API 23 or later. Secure values are stored in PlayerPrefs without encryption.");
#else
			return new PlayerPrefsSecureStore("This platform has no secure storage. Secure values are stored in PlayerPrefs without encryption.");
#endif
		}
	}
}
