using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 보안 저장소가 없는 곳(에디터 등)의 대체 저장소입니다. PlayerPrefs 에 암호화 없이 둡니다.
	/// </summary>
	internal sealed class PlayerPrefsSecureStore : ISecureStore
	{
		internal const string KeyPrefix = "geuneda.secure.";

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Core.SecureStore");

		/// <param name="unprotectedWarning">null 이 아니면 암호화하지 않는다는 Warning 으로 남깁니다</param>
		public PlayerPrefsSecureStore(string unprotectedWarning)
		{
			if (unprotectedWarning != null)
			{
				Logger.Warning(unprotectedWarning);
			}
		}

		public SecureReadResult TryRead(string key, out string value)
		{
			var prefsKey = KeyPrefix + key;

			if (!PlayerPrefs.HasKey(prefsKey))
			{
				value = null;
				return SecureReadResult.NotFound;
			}

			value = PlayerPrefs.GetString(prefsKey, string.Empty);
			return SecureReadResult.Found;
		}

		public bool TryWrite(string key, string value)
		{
			try
			{
				PlayerPrefs.SetString(KeyPrefix + key, value);
				PlayerPrefs.Save();
				return true;
			}
			catch (PlayerPrefsException exception)
			{
				// WebGL 의 저장 한도(1MB)를 넘은 경우
				Logger.Warning($"Secure store write failed for '{key}': {exception.Message}");
				return false;
			}
		}

		public bool TryDelete(string key)
		{
			PlayerPrefs.DeleteKey(KeyPrefix + key);
			PlayerPrefs.Save();
			return true;
		}
	}
}
