using System;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 설치 ID 와 기기 ID 입니다. 생성할 때 한 번 정해지고, 부트스트랩에서 만들어 services 의 <c>MainInstaller</c> 에
	/// <see cref="IAppIdentity"/> 로 바인딩합니다(<c>MainInstaller.Bind&lt;IAppIdentity&gt;(new AppIdentity())</c>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="InstallId"/> 는 첫 실행에 만든 GUID 를 PlayerPrefs 에 저장합니다. 앱 데이터가 지워지면(앱 삭제 후 재설치 등) 새로 만듭니다.
	/// 단, Android 자동 백업이나 iOS 백업 복원으로 PlayerPrefs 가 되살아나면 이전 값을 그대로 씁니다.
	/// </para>
	/// <para>
	/// <see cref="DeviceId"/> 는 iOS 에서 키체인에 저장한 GUID 라 다시 설치해도 유지됩니다(다른 기기로는 옮겨지지 않음).
	/// 키체인을 읽거나 쓰지 못하면 그 실행 동안 플랫폼 ID(iOS 는 identifierForVendor)를 씁니다. 읽기 실패 때는 기존 값을 잃지 않도록 새로 쓰지 않습니다.
	/// 그 밖의 플랫폼은 <see cref="SystemInfo.deviceUniqueIdentifier"/>(Android 는 ANDROID_ID 기반)이고, 지원하지 않으면 PlayerPrefs 에 저장한 GUID 입니다.
	/// </para>
	/// <para>
	/// PlayerPrefs 와 <see cref="SystemInfo"/> 를 쓰므로 메인 스레드에서 만듭니다.
	/// </para>
	/// </remarks>
	public sealed class AppIdentity : IAppIdentity
	{
		internal const string InstallIdKey = "geuneda.core.installId";
		internal const string DeviceIdKey = "geuneda.core.deviceId";

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Core.AppIdentity");

		/// <inheritdoc />
		public string InstallId { get; }

		/// <inheritdoc />
		public string DeviceId { get; }

		public AppIdentity() : this(new PlayerPrefsStore(), CreatePlatformSecureStore(), () => SystemInfo.deviceUniqueIdentifier)
		{
		}

		internal AppIdentity(IKeyValueStore prefs, ISecureStore secureStore, Func<string> platformDeviceId)
		{
			if (prefs == null)
			{
				throw new ArgumentNullException(nameof(prefs));
			}

			if (platformDeviceId == null)
			{
				throw new ArgumentNullException(nameof(platformDeviceId));
			}

			InstallId = GetOrCreate(prefs, InstallIdKey);
			DeviceId = ResolveDeviceId(prefs, secureStore, platformDeviceId);
		}

		private static string ResolveDeviceId(IKeyValueStore prefs, ISecureStore secureStore, Func<string> platformDeviceId)
		{
			if (secureStore != null)
			{
				switch (secureStore.TryRead(DeviceIdKey, out var stored))
				{
					case SecureReadResult.Found:
						return stored;
					case SecureReadResult.NotFound:
						var created = NewId();

						if (secureStore.TryWrite(DeviceIdKey, created))
						{
							return created;
						}

						Logger.Warning("Could not save the device id to secure storage. Using the platform id for this run.");
						break;
					default:
						// 값이 있는지 모르므로 새로 쓰지 않는다. 덮어쓰면 기존 기기 ID 를 잃는다
						Logger.Warning("Could not read the device id from secure storage. Using the platform id for this run.");
						break;
				}
			}

			var platformId = platformDeviceId();

			if (!string.IsNullOrEmpty(platformId) && platformId != SystemInfo.unsupportedIdentifier)
			{
				return platformId;
			}

			return GetOrCreate(prefs, DeviceIdKey);
		}

		private static string GetOrCreate(IKeyValueStore prefs, string key)
		{
			var stored = prefs.Get(key);

			if (!string.IsNullOrEmpty(stored))
			{
				return stored;
			}

			var created = NewId();
			prefs.Set(key, created);
			return created;
		}

		private static string NewId()
		{
			return Guid.NewGuid().ToString("N");
		}

		// 다시 설치해도 남는 저장소만 쓴다. Android 키스토어 값은 앱을 지우면 사라지므로 ANDROID_ID 를 쓴다
		private static ISecureStore CreatePlatformSecureStore()
		{
#if UNITY_IOS && !UNITY_EDITOR
			return new IosKeychainStore();
#else
			return null;
#endif
		}
	}
}
