// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 설치 ID 와 기기 ID 입니다. 기본 구현은 <see cref="AppIdentity"/> 이고, services 의 <c>MainInstaller</c> 에 이 인터페이스로 바인딩합니다.
	/// </summary>
	public interface IAppIdentity
	{
		/// <summary>이 설치의 ID(32자리 16진수 GUID). 앱 데이터가 지워지고 백업에서 복원되지 않으면 새로 만듭니다.</summary>
		string InstallId { get; }

		/// <summary>이 기기의 ID. 플랫폼별 유지 범위는 <see cref="AppIdentity"/> 설명을 봅니다.</summary>
		string DeviceId { get; }
	}
}
