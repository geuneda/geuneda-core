// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary><see cref="ISecureStore.TryRead"/> 의 결과입니다.</summary>
	public enum SecureReadResult
	{
		Found,
		NotFound,
		/// <summary>있는지 알 수 없는 실패(예: 기기 잠금). 덮어쓰면 기존 값을 잃을 수 있습니다</summary>
		Failed
	}
}
