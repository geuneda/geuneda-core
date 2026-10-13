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

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Core.SecureStore");

		[DllImport("__Internal")]
		private static extern string GeunedaKeychain_Copy(string key, out int status);

		[DllImport("__Internal")]
		private static extern int GeunedaKeychain_Set(string key, string value);

		[DllImport("__Internal")]
		private static extern int GeunedaKeychain_Delete(string key);

		public SecureReadResult TryRead(string key, out string value)
		{
			value = GeunedaKeychain_Copy(key, out var status);

			if (status == ErrSecSuccess)
			{
				value ??= string.Empty;
				return SecureReadResult.Found;
			}

			value = null;

			if (status == ErrSecItemNotFound)
			{
				return SecureReadResult.NotFound;
			}

			Logger.Warning($"Keychain read failed for '{key}' (OSStatus {status})");
			return SecureReadResult.Failed;
		}

		public bool TryWrite(string key, string value)
		{
			var status = GeunedaKeychain_Set(key, value);

			if (status == ErrSecSuccess)
			{
				return true;
			}

			Logger.Warning($"Keychain write failed for '{key}' (OSStatus {status})");
			return false;
		}

		public bool TryDelete(string key)
		{
			var status = GeunedaKeychain_Delete(key);

			if (status == ErrSecSuccess || status == ErrSecItemNotFound)
			{
				return true;
			}

			Logger.Warning($"Keychain delete failed for '{key}' (OSStatus {status})");
			return false;
		}
	}
}
#endif
