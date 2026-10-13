using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Geuneda.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests.PlayMode
{
	/// <summary>
	/// 플레이 중 PlayerLoop 가 큐를 처리하는지 확인합니다.
	/// </summary>
	public class MainThreadDispatcherPlayModeTest
	{
		private const string DispatcherSystemName = "MainThreadDispatcherUpdate";

		[Test]
		public void PlayerLoop_HasDispatcherOnceJustBeforeBehaviourUpdate()
		{
			var root = PlayerLoop.GetCurrentPlayerLoop();
			var found = 0;
			var isJustBeforeBehaviourUpdate = false;

			foreach (var phase in root.subSystemList)
			{
				var systems = phase.subSystemList ?? Array.Empty<PlayerLoopSystem>();

				for (var i = 0; i < systems.Length; i++)
				{
					if (systems[i].type != null && systems[i].type.Name == DispatcherSystemName)
					{
						found++;
						isJustBeforeBehaviourUpdate = phase.type == typeof(UnityEngine.PlayerLoop.Update) && i + 1 < systems.Length &&
							systems[i + 1].type == typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate);
					}
				}
			}

			Assert.AreEqual(1, found);
			Assert.IsTrue(isJustBeforeBehaviourUpdate);
		}

		[UnityTest]
		public IEnumerator PostFromBackgroundThread_RunsOnMainThread()
		{
			var ran = false;
			var ranOnMainThread = false;

			var post = Task.Run(() => MainThreadDispatcher.Post(() =>
			{
				ran = true;
				ranOnMainThread = MainThreadDispatcher.IsMainThread;
			}));

			while (!post.IsCompleted)
			{
				yield return null;
			}

			Assert.IsNull(post.Exception);

			for (var frame = 0; frame < 5 && !ran; frame++)
			{
				yield return null;
			}

			Assert.IsTrue(ran);
			Assert.IsTrue(ranOnMainThread);
		}

		[UnityTest]
		public IEnumerator ThrowingAction_DoesNotStopLaterActions()
		{
			var ran = false;

			LogAssert.Expect(LogType.Error, new Regex("dispatcher test failure"));
			LogAssert.Expect(LogType.Exception, new Regex("dispatcher test failure"));
			MainThreadDispatcher.Post(() => throw new InvalidOperationException("dispatcher test failure"));
			MainThreadDispatcher.Post(() => ran = true);

			for (var frame = 0; frame < 5 && !ran; frame++)
			{
				yield return null;
			}

			Assert.IsTrue(ran);
		}
	}
}
