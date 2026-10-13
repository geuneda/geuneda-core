using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 모든 <see cref="ModuleLogger"/> 가 쓰는 출력 대상을 보관합니다.
	/// </summary>
	/// <remarks>
	/// geuneda 프레임워크에서 static 상태를 허용하는 두 곳(로거, 메인 스레드 디스패처) 중 하나입니다.
	/// 플레이 세션이 시작될 때(<see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>) 출력 대상을 기본값(<see cref="UnityConsoleSink"/>)으로,
	/// 살아 있는 모든 <see cref="ModuleLogger"/> 의 레벨을 <see cref="ModuleLogger.DefaultLevels"/> 로 되돌립니다. 그래서 도메인 리로드를 끈 에디터에서도
	/// 이전 세션의 설정이 남지 않습니다. 출력 대상과 레벨은 그 이후(<see cref="RuntimeInitializeLoadType.AfterAssembliesLoaded"/> 이후)에 바꾸세요.
	/// </remarks>
	public static class Log
	{
		private static volatile ILogSink _sink = new UnityConsoleSink();

		// 세션 시작 때 레벨을 되돌릴 로거들. 로거 수명을 붙잡지 않도록 약한 참조로 둔다
		private static readonly List<WeakReference<ModuleLogger>> _loggers = new List<WeakReference<ModuleLogger>>();
		private static int _pruneThreshold = MinPruneThreshold;
		private const int MinPruneThreshold = 64;

		/// <summary>
		/// 로그를 출력할 대상입니다. 기본값은 <see cref="UnityConsoleSink"/> 입니다.
		/// </summary>
		/// <exception cref="ArgumentNullException">null 을 넣은 경우</exception>
		public static ILogSink Sink
		{
			get => _sink;
			set => _sink = value ?? throw new ArgumentNullException(nameof(value));
		}

		/// <summary>
		/// 출력 대상을 기본값(<see cref="UnityConsoleSink"/>)으로 되돌립니다.
		/// </summary>
		public static void ResetSink()
		{
			_sink = new UnityConsoleSink();
		}

		internal static void Register(ModuleLogger logger)
		{
			lock (_loggers)
			{
				// 버려진 로거가 쌓이지 않도록 목록이 커질 때마다 정리한다
				if (_loggers.Count >= _pruneThreshold)
				{
					_loggers.RemoveAll(reference => !reference.TryGetTarget(out _));
					_pruneThreshold = Math.Max(MinPruneThreshold, _loggers.Count * 2);
				}

				_loggers.Add(new WeakReference<ModuleLogger>(logger));
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetOnSubsystemRegistration()
		{
			ResetSink();

			lock (_loggers)
			{
				for (var i = _loggers.Count - 1; i >= 0; i--)
				{
					if (_loggers[i].TryGetTarget(out var logger))
					{
						logger.EnabledLevels = ModuleLogger.DefaultLevels;
					}
					else
					{
						_loggers.RemoveAt(i);
					}
				}
			}
		}
	}
}
