#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// Android 키스토어 저장소(<c>Plugins/Android/GeunedaSecureStore.java</c>)입니다. 값은 키스토어의 AES-256-GCM 키로 암호화해
	/// 앱 전용 SharedPreferences 에 둡니다. 복호화할 수 없는 값(백업 복원 등)은 플러그인이 지우고 없는 것으로 돌려줍니다.
	/// </summary>
	internal sealed class AndroidKeystoreStore : ISecureStore
	{
		private const string PluginClass = "com.geuneda.core.GeunedaSecureStore";
		private const int MinApiLevel = 23;

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Core.SecureStore");

		/// <summary>키스토어에 AES 키를 만들 수 있는 API 23 이상인지</summary>
		public static bool IsSupported()
		{
			using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
			{
				return version.GetStatic<int>("SDK_INT") >= MinApiLevel;
			}
		}

		public SecureReadResult TryRead(string key, out string value)
		{
			try
			{
				using (var plugin = new AndroidJavaClass(PluginClass))
				using (var activity = CurrentActivity())
				{
					value = plugin.CallStatic<string>("read", activity, key);
				}

				return value != null ? SecureReadResult.Found : SecureReadResult.NotFound;
			}
			catch (AndroidJavaException exception)
			{
				Logger.Warning($"Secure store read failed for '{key}': {exception.Message}");
				value = null;
				return SecureReadResult.Failed;
			}
		}

		public bool TryWrite(string key, string value)
		{
			try
			{
				using (var plugin = new AndroidJavaClass(PluginClass))
				using (var activity = CurrentActivity())
				{
					plugin.CallStatic("write", activity, key, value);
				}

				return true;
			}
			catch (AndroidJavaException exception)
			{
				Logger.Warning($"Secure store write failed for '{key}': {exception.Message}");
				return false;
			}
		}

		public bool TryDelete(string key)
		{
			try
			{
				using (var plugin = new AndroidJavaClass(PluginClass))
				using (var activity = CurrentActivity())
				{
					plugin.CallStatic("delete", activity, key);
				}

				return true;
			}
			catch (AndroidJavaException exception)
			{
				Logger.Warning($"Secure store delete failed for '{key}': {exception.Message}");
				return false;
			}
		}

		private static AndroidJavaObject CurrentActivity()
		{
			using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
			{
				return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
			}
		}
	}
}
#endif
