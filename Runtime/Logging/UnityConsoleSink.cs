using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 기본 출력 대상입니다. <c>[모듈] 메시지</c> 형식으로 Unity 콘솔에 씁니다.
	/// </summary>
	/// <remarks>
	/// Verbose, Debug, Info 는 Log, Warning 은 Warning, Error 와 Fatal 은 Error 로 씁니다.
	/// 예외가 있으면 메시지를 쓴 뒤 <see cref="Debug.LogException(System.Exception, Object)"/> 로 예외를 따로 씁니다.
	/// </remarks>
	public sealed class UnityConsoleSink : ILogSink
	{
		public void Write(in LogEntry entry)
		{
			var message = Format(entry);

			// 메시지 안의 중괄호가 서식으로 해석되지 않도록 그대로 넘긴다
			Debug.LogFormat(ToLogType(entry.Level), LogOption.None, entry.Context, "{0}", message);

			if (entry.Exception != null)
			{
				Debug.LogException(entry.Exception, entry.Context);
			}
		}

		/// <summary>
		/// <c>[모듈] 메시지</c> 형식으로 만듭니다.
		/// </summary>
		public static string Format(in LogEntry entry)
		{
			return $"[{entry.Module}] {entry.Message}";
		}

		private static LogType ToLogType(LogLevel level)
		{
			switch (level)
			{
				case LogLevel.Warning:
					return LogType.Warning;
				case LogLevel.Error:
				case LogLevel.Fatal:
					return LogType.Error;
				default:
					return LogType.Log;
			}
		}
	}
}
