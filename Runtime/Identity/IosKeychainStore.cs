#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// iOS 키체인(<c>Plugins/iOS/GeunedaKeychain.mm</c>)입니다. 항목은 앱을 지워도 남고, 다른 기기로 옮겨지지 않습니다(ThisDeviceOnly).
	/// </summary>
	internal sealed class IosKeychainStore : ISecureStore
	{
		private const int ErrSecSuccess = 0;
		private const int ErrSecItemNotFound = -25300;

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Core.AppIdentity");

		[DllImport("__Internal")]
		private static extern string GeunedaKeychain_Copy(string key, out int status);

		[DllImport("__Internal")]
		private static extern int GeunedaKeychain_Add(string key, string value);

		public SecureReadResult TryRead(string key, out string value)
		{
			value = GeunedaKeychain_Copy(key, out var status);

			if (status == ErrSecSuccess && !string.IsNullOrEmpty(value))
			{
				return SecureReadResult.Found;
			}

			if (status == ErrSecItemNotFound)
			{
				return SecureReadResult.NotFound;
			}

			Logger.Warning($"Keychain read failed for '{key}' (OSStatus {status})");
			value = null;
			return SecureReadResult.Failed;
		}

		public bool TryAdd(string key, string value)
		{
			var status = GeunedaKeychain_Add(key, value);

			if (status == ErrSecSuccess)
			{
				return true;
			}

			Logger.Warning($"Keychain add failed for '{key}' (OSStatus {status})");
			return false;
		}
	}
}
#endif
