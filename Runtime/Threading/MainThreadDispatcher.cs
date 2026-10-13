using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 다른 스레드(SDK 콜백, 스레드 풀)에서 받은 작업을 Unity 메인 스레드에서 실행합니다.
	/// </summary>
	/// <remarks>
	/// <para>
	/// 플레이 세션이 시작될 때(<c>SubsystemRegistration</c>) PlayerLoop 의 Update 단계에서 MonoBehaviour <c>Update</c> 를 실행하는
	/// <c>ScriptRunBehaviourUpdate</c> 바로 앞에 처리 단계를 끼워 넣습니다. GameObject 가 필요 없고, 같은 프레임의 <c>Update</c> 보다 먼저 실행됩니다.
	/// 다른 라이브러리(UniTask 등)가 나중에 Update 맨 앞에 단계를 넣어도 이 위치는 유지됩니다.
	/// </para>
	/// <para>
	/// 한 번 처리할 때 그때까지 쌓인 작업을 모두 순서대로 실행합니다(프레임당 개수 제한 없음). 처리 중에 들어온 작업은 다음 프레임에 실행합니다.
	/// 작업 하나가 예외를 던져도 로그만 남기고 나머지를 계속 실행합니다.
	/// </para>
	/// <para>
	/// 플레이 중에 쓰도록 만든 것입니다. 에디터 편집 모드에서의 실행은 보장하지 않습니다.
	/// static 가변 상태를 허용하는 두 곳 중 하나입니다(D2, 패키지 AGENTS.md).
	/// </para>
	/// </remarks>
	public static class MainThreadDispatcher
	{
		// PlayerLoop 에서 이 처리 단계를 찾기 위한 표식 타입
		private struct MainThreadDispatcherUpdate
		{
		}

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Core.MainThreadDispatcher");
		private static readonly object _lock = new object();

		// 받는 목록과 처리하는 목록을 바꿔 가며 써서 프레임마다 할당하지 않는다
		private static List<Action> _pending = new List<Action>();
		private static List<Action> _processing = new List<Action>();
		private static bool _isProcessing;
		private static int _mainThreadId;

		/// <summary>
		/// 지금 스레드가 Unity 메인 스레드이면 true 입니다.
		/// </summary>
		/// <remarks>
		/// 메인 스레드는 플레이 세션이 시작될 때 기록합니다. 그 전(에디터를 연 뒤 한 번도 플레이하지 않은 편집 모드)에는 어느 스레드에서든 false 입니다.
		/// 한 번 기록한 뒤에는 플레이를 멈춘 편집 모드에서도 메인 스레드면 true 입니다.
		/// </remarks>
		public static bool IsMainThread
		{
			get
			{
				var mainThreadId = Volatile.Read(ref _mainThreadId);
				return mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == mainThreadId;
			}
		}

		/// <summary>
		/// <paramref name="action"/> 을 메인 스레드의 다음 처리 때 실행하도록 넣습니다. 어느 스레드에서 불러도 됩니다.
		/// </summary>
		/// <remarks>
		/// 메인 스레드에서 불러도 바로 실행하지 않고 넣은 순서대로 다음 처리 때 실행합니다.
		/// </remarks>
		/// <exception cref="ArgumentNullException"><paramref name="action"/> 이 null 인 경우</exception>
		public static void Post(Action action)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			lock (_lock)
			{
				_pending.Add(action);
			}
		}

		/// <summary>
		/// 쌓인 작업을 모두 실행합니다. PlayerLoop 가 메인 스레드에서 매 프레임 부릅니다.
		/// </summary>
		internal static void ProcessQueue()
		{
			// 작업 안에서 다시 불려도 처리 중인 목록을 바꾸지 않는다
			if (_isProcessing)
			{
				return;
			}

			lock (_lock)
			{
				if (_pending.Count == 0)
				{
					return;
				}

				var received = _pending;
				_pending = _processing;
				_processing = received;
			}

			_isProcessing = true;
			try
			{
				for (var i = 0; i < _processing.Count; i++)
				{
					try
					{
						_processing[i]();
					}
					catch (Exception exception)
					{
						Logger.Error(exception);
					}
				}
			}
			finally
			{
				_processing.Clear();
				_isProcessing = false;
			}
		}

		/// <summary>
		/// 이전 플레이 세션의 작업을 버리고 지금 스레드를 메인 스레드로 기록합니다.
		/// </summary>
		internal static void ResetState()
		{
			lock (_lock)
			{
				_pending.Clear();
			}

			_processing.Clear();
			_isProcessing = false;
			Volatile.Write(ref _mainThreadId, Thread.CurrentThread.ManagedThreadId);
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Initialize()
		{
			ResetState();
			InstallPlayerLoopSystem();
		}

		private static void InstallPlayerLoopSystem()
		{
			var root = PlayerLoop.GetCurrentPlayerLoop();
			var phases = root.subSystemList;

			for (var i = 0; i < phases.Length; i++)
			{
				if (phases[i].type != typeof(UnityEngine.PlayerLoop.Update))
				{
					continue;
				}

				// 도메인 리로드를 끈 Enter Play Mode 에서는 이전 세션에 넣은 단계가 남아 있을 수 있어 지우고 다시 넣는다
				var systems = new List<PlayerLoopSystem>();

				foreach (var system in phases[i].subSystemList ?? Array.Empty<PlayerLoopSystem>())
				{
					if (system.type != typeof(MainThreadDispatcherUpdate))
					{
						systems.Add(system);
					}
				}

				// MonoBehaviour.Update 바로 앞. 없으면(비표준 PlayerLoop) Update 단계 맨 앞
				var behaviourUpdateIndex = systems.FindIndex(system => system.type == typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate));
				systems.Insert(Math.Max(behaviourUpdateIndex, 0),
					new PlayerLoopSystem { type = typeof(MainThreadDispatcherUpdate), updateDelegate = ProcessQueue });

				var newPhases = (PlayerLoopSystem[])phases.Clone();
				newPhases[i].subSystemList = systems.ToArray();
				root.subSystemList = newPhases;
				PlayerLoop.SetPlayerLoop(root);
				return;
			}

			Logger.Error("PlayerLoop has no Update phase. MainThreadDispatcher will not run.");
		}
	}
}
