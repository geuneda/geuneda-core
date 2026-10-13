using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 로그 레벨입니다. 비트 플래그라 여러 레벨을 한 번에 켜고 끌 수 있습니다.
	/// </summary>
	[Flags]
	public enum LogLevel : byte
	{
		None = 0,
		/// <summary>아주 자세한 추적. 기본으로 꺼져 있고 릴리스 빌드에서는 호출 자체가 제거됩니다.</summary>
		Verbose = 1 << 0,
		/// <summary>개발 중 디버깅용. 기본으로 꺼져 있고 릴리스 빌드에서는 호출 자체가 제거됩니다.</summary>
		Debug = 1 << 1,
		/// <summary>정상 흐름의 주요 사건</summary>
		Info = 1 << 2,
		/// <summary>문제가 될 수 있지만 계속 진행할 수 있는 상황</summary>
		Warning = 1 << 3,
		/// <summary>기능이 실패했지만 앱은 계속 동작하는 상황</summary>
		Error = 1 << 4,
		/// <summary>더 진행할 수 없는 오류. 보통 <c>throw logger.Fatal(exception)</c> 으로 씁니다.</summary>
		Fatal = 1 << 5,
		All = Verbose | Debug | Info | Warning | Error | Fatal
	}
}
