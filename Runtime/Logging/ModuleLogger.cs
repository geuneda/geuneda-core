using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 모듈 하나의 로거입니다. 모듈 이름을 붙여 <see cref="Log.Sink"/> 로 보내고, 레벨은 로거마다 켜고 끕니다.
	/// </summary>
	/// <remarks>
	/// <para>
	/// 보통 모듈마다 <c>private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Services.Pool");</c> 처럼 하나를 둡니다.
	/// 기본으로 Verbose 와 Debug 는 꺼져 있습니다(<see cref="DefaultLevels"/>).
	/// </para>
	/// <para>
	/// <see cref="Verbose"/> 와 <see cref="Debug"/> 는 에디터, 개발 빌드(<c>DEVELOPMENT_BUILD</c>), 또는 <c>GEUNEDA_LOG_DEBUG</c> 심볼이 있을 때만
	/// 컴파일됩니다. 릴리스 빌드에서는 인자 계산(문자열 보간 포함)까지 호출이 통째로 사라집니다.
	/// </para>
	/// <para>
	/// 호출자 정보(멤버, 파일, 줄)는 <c>[Caller*]</c> 특성으로 자동으로 채워지므로 직접 넘기지 않습니다.
	/// </para>
	/// </remarks>
	public sealed class ModuleLogger
	{
		/// <summary>
		/// 새 로거가 켜 두는 레벨: Info, Warning, Error, Fatal
		/// </summary>
		public const LogLevel DefaultLevels = LogLevel.Info | LogLevel.Warning | LogLevel.Error | LogLevel.Fatal;

		/// <summary>
		/// 로그에 붙는 모듈 이름
		/// </summary>
		public string Module { get; }

		// 여러 스레드에서 읽고 바꿀 수 있도록 int 로 두고 Volatile/Interlocked 로 접근한다
		private int _enabledLevels = (int)DefaultLevels;

		/// <summary>
		/// 출력할 레벨입니다. 여러 스레드에서 읽고 바꿔도 됩니다.
		/// </summary>
		/// <remarks>
		/// 플레이 세션이 시작될 때 <see cref="DefaultLevels"/> 로 되돌아갑니다(<see cref="Log"/> 참고).
		/// </remarks>
		public LogLevel EnabledLevels
		{
			get => (LogLevel)Volatile.Read(ref _enabledLevels);
			set => Volatile.Write(ref _enabledLevels, (int)value);
		}

		/// <exception cref="ArgumentException"><paramref name="module"/> 이 null 이거나 비어 있는 경우</exception>
		public ModuleLogger(string module)
		{
			if (string.IsNullOrEmpty(module))
			{
				throw new ArgumentException("Module name must not be empty", nameof(module));
			}

			Module = module;
			Log.Register(this);
		}

		/// <summary>
		/// <paramref name="level"/> 중 하나라도 켜져 있으면 true 입니다. 비싼 메시지를 만들기 전에 확인할 때 씁니다.
		/// </summary>
		public bool IsEnabled(LogLevel level)
		{
			return (EnabledLevels & level) != 0;
		}

		/// <summary>
		/// <paramref name="levels"/> 를 켭니다. 다른 스레드의 동시 변경을 잃지 않습니다.
		/// </summary>
		public void Enable(LogLevel levels)
		{
			Update(current => current | (int)levels);
		}

		/// <summary>
		/// <paramref name="levels"/> 를 끕니다. 다른 스레드의 동시 변경을 잃지 않습니다.
		/// </summary>
		public void Disable(LogLevel levels)
		{
			Update(current => current & ~(int)levels);
		}

		[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("GEUNEDA_LOG_DEBUG")]
		public void Verbose(string message, Object context = null,
			[CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
		{
			Write(LogLevel.Verbose, message, null, context, member, file, line);
		}

		[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("GEUNEDA_LOG_DEBUG")]
		public void Debug(string message, Object context = null,
			[CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
		{
			Write(LogLevel.Debug, message, null, context, member, file, line);
		}

		public void Info(string message, Object context = null,
			[CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
		{
			Write(LogLevel.Info, message, null, context, member, file, line);
		}

		public void Warning(string message, Object context = null,
			[CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
		{
			Write(LogLevel.Warning, message, null, context, member, file, line);
		}

		public void Error(string message, Object context = null,
			[CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
		{
			Write(LogLevel.Error, message, null, context, member, file, line);
		}

		/// <summary>
		/// 예외와 함께 Error 로그를 남깁니다. <paramref name="message"/> 가 없으면 예외 메시지를 씁니다.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="exception"/> 이 null 인 경우</exception>
		public void Error(Exception exception, string message = null, Object context = null,
			[CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
		{
			if (exception == null)
			{
				throw new ArgumentNullException(nameof(exception));
			}

			Write(LogLevel.Error, message ?? exception.Message, exception, context, member, file, line);
		}

		/// <summary>
		/// 예외와 함께 Fatal 로그를 남기고 같은 예외를 돌려줍니다. <c>throw logger.Fatal(new InvalidOperationException(...))</c> 처럼 씁니다.
		/// </summary>
		/// <remarks>
		/// 던진 예외를 아무도 잡지 않으면 Unity 가 예외를 한 번 더 콘솔에 씁니다.
		/// </remarks>
		/// <exception cref="ArgumentNullException"><paramref name="exception"/> 이 null 인 경우</exception>
		public T Fatal<T>(T exception, string message = null, Object context = null,
			[CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
			where T : Exception
		{
			if (exception == null)
			{
				throw new ArgumentNullException(nameof(exception));
			}

			Write(LogLevel.Fatal, message ?? exception.Message, exception, context, member, file, line);

			return exception;
		}

		private void Update(Func<int, int> change)
		{
			int current;

			do
			{
				current = Volatile.Read(ref _enabledLevels);
			}
			while (Interlocked.CompareExchange(ref _enabledLevels, change(current), current) != current);
		}

		private void Write(LogLevel level, string message, Exception exception, Object context,
			string member, string file, int line)
		{
			if (!IsEnabled(level))
			{
				return;
			}

			var entry = new LogEntry(level, Module, message, exception, context, member, file, line);

			try
			{
				Log.Sink.Write(entry);
			}
			catch (Exception sinkException)
			{
				// 출력 대상의 오류가 로그를 남긴 쪽의 흐름을 끊지 않게 한다
				UnityEngine.Debug.LogException(sinkException);
			}
		}
	}
}
