// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 토큰이나 비밀 값처럼 민감한 문자열을 기기에 보관하는 저장소입니다. 기본 구현은 <see cref="PlatformSecureStore"/> 이고,
	/// services 의 <c>MainInstaller</c> 에 이 인터페이스로 바인딩합니다.
	/// </summary>
	/// <remarks>
	/// 실패는 예외 대신 반환값으로 알립니다. 빈 키, null 값, NUL 문자를 담은 키·값 같은 오용은 예외입니다. 메인 스레드에서 부릅니다.
	/// </remarks>
	public interface ISecureStore
	{
		/// <summary>값을 읽습니다.</summary>
		/// <returns>
		/// <see cref="SecureReadResult.Failed"/> 면 값이 있는지 알 수 없습니다. 이때 새 값을 쓰면 기존 값을 잃을 수 있습니다.
		/// </returns>
		SecureReadResult TryRead(string key, out string value);

		/// <summary>값을 추가하거나 덮어씁니다. 실패하면 false 입니다.</summary>
		bool TryWrite(string key, string value);

		/// <summary>값을 지웁니다. 원래 없었어도 true 이고, 실패하면 false 입니다.</summary>
		bool TryDelete(string key);
	}
}
