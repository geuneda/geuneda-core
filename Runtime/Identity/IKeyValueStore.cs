// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 앱 데이터와 함께 지워지는 문자열 저장소입니다(기본 PlayerPrefs). 테스트에서 바꿔 끼웁니다.
	/// </summary>
	internal interface IKeyValueStore
	{
		/// <summary>없으면 null 이나 빈 문자열</summary>
		string Get(string key);

		/// <summary>저장하고 바로 디스크에 씁니다.</summary>
		void Set(string key, string value);
	}
}
