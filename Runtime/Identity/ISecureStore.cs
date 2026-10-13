// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	internal enum SecureReadResult
	{
		Found,
		NotFound,
		/// <summary>있는지 알 수 없는 실패(예: 기기 잠금). 덮어쓰면 기존 값을 잃을 수 있다</summary>
		Failed
	}

	/// <summary>
	/// 앱을 지워도 남는 기기 저장소입니다(iOS 키체인). 지원하지 않는 플랫폼에는 없습니다.
	/// </summary>
	internal interface ISecureStore
	{
		SecureReadResult TryRead(string key, out string value);

		/// <summary>새 항목을 추가합니다. 이미 있거나 실패하면 false 입니다.</summary>
		bool TryAdd(string key, string value);
	}
}
