using System;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 로그 한 건입니다. <see cref="ILogSink"/> 가 받아 출력합니다.
	/// </summary>
	public readonly struct LogEntry
	{
		public readonly LogLevel Level;
		/// <summary>로그를 남긴 모듈 이름(<see cref="ModuleLogger.Module"/>)</summary>
		public readonly string Module;
		public readonly string Message;
		/// <summary>함께 남긴 예외. 없으면 null</summary>
		public readonly Exception Exception;
		/// <summary>Unity 콘솔에서 선택할 오브젝트. 없으면 null</summary>
		public readonly Object Context;
		public readonly string CallerMember;
		public readonly string CallerFile;
		public readonly int CallerLine;

		public LogEntry(LogLevel level, string module, string message, Exception exception, Object context,
			string callerMember, string callerFile, int callerLine)
		{
			Level = level;
			Module = module;
			Message = message;
			Exception = exception;
			Context = context;
			CallerMember = callerMember;
			CallerFile = callerFile;
			CallerLine = callerLine;
		}
	}
}
