// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 앱이 붙는 서버·설정 환경입니다. 현재 값은 <see cref="AppEnv.Current"/> 입니다.
	/// </summary>
	public enum AppEnvironment
	{
		Dev = 1,
		QA = 2,
		Prod = 3
	}
}
