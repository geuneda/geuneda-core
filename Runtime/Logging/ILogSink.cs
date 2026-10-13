// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 로그를 실제로 출력하는 대상입니다. <see cref="Log.Sink"/> 로 교체해 파일, 원격 수집, 테스트 기록 등으로 보낼 수 있습니다.
	/// </summary>
	/// <remarks>
	/// 여러 스레드에서 동시에 불릴 수 있습니다. 여기서 던진 예외는 로그를 남긴 쪽으로 전파되지 않고 Unity 콘솔에 남습니다.
	/// </remarks>
	public interface ILogSink
	{
		void Write(in LogEntry entry);
	}
}
