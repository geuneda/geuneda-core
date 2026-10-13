using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	internal sealed class PlayerPrefsStore : IKeyValueStore
	{
		public string Get(string key)
		{
			return PlayerPrefs.GetString(key, null);
		}

		public void Set(string key, string value)
		{
			PlayerPrefs.SetString(key, value);
			// 첫 실행에 만든 ID 가 비정상 종료로 사라지지 않게 바로 쓴다
			PlayerPrefs.Save();
		}
	}
}
