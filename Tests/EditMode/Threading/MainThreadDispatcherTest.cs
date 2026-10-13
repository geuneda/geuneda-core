using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Geuneda.Core;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	/// <summary>
	/// 큐 처리 규칙을 편집 모드에서 직접 <c>ProcessQueue</c> 를 불러 확인합니다. PlayerLoop 연결은 PlayMode 테스트가 확인합니다.
	/// </summary>
	[TestFixture]
	public class MainThreadDispatcherTest
	{
		private sealed class RecordingSink : ILogSink
		{
			public readonly List<LogEntry> Entries = new List<LogEntry>();

			public void Write(in LogEntry entry)
			{
				Entries.Add(entry);
			}
		}

		private ILogSink _originalSink;
		private RecordingSink _sink;

		[SetUp]
		public void SetUp()
		{
			_originalSink = Log.Sink;
			_sink = new RecordingSink();
			Log.Sink = _sink;
			MainThreadDispatcher.ResetState();
		}

		[TearDown]
		public void TearDown()
		{
			MainThreadDispatcher.ResetState();
			Log.Sink = _originalSink;
		}

		[Test]
		public void Post_Null_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => MainThreadDispatcher.Post(null));
		}

		[Test]
		public void Post_DoesNotRunUntilProcessed()
		{
			var ran = false;

			MainThreadDispatcher.Post(() => ran = true);

			Assert.IsFalse(ran);
			MainThreadDispatcher.ProcessQueue();
			Assert.IsTrue(ran);
		}

		[Test]
		public void ProcessQueue_RunsAllInPostedOrder()
		{
			var order = new List<int>();

			for (var i = 0; i < 5; i++)
			{
				var value = i;
				MainThreadDispatcher.Post(() => order.Add(value));
			}

			MainThreadDispatcher.ProcessQueue();

			CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, order);
		}

		[Test]
		public void ProcessQueue_ActionThrows_LogsAndRunsRest()
		{
			var exception = new InvalidOperationException("callback failed");
			var ran = false;

			MainThreadDispatcher.Post(() => throw exception);
			MainThreadDispatcher.Post(() => ran = true);
			MainThreadDispatcher.ProcessQueue();

			Assert.IsTrue(ran);
			Assert.AreEqual(1, _sink.Entries.Count);
			Assert.AreEqual(LogLevel.Error, _sink.Entries[0].Level);
			Assert.AreSame(exception, _sink.Entries[0].Exception);
		}

		[Test]
		public void PostDuringProcessing_RunsOnNextProcess()
		{
			var order = new List<string>();

			MainThreadDispatcher.Post(() =>
			{
				order.Add("first");
				MainThreadDispatcher.Post(() => order.Add("posted inside"));
			});

			MainThreadDispatcher.ProcessQueue();
			CollectionAssert.AreEqual(new[] { "first" }, order);

			MainThreadDispatcher.ProcessQueue();
			CollectionAssert.AreEqual(new[] { "first", "posted inside" }, order);
		}

		[Test]
		public void ProcessQueueInsideAction_DoesNotRunNestedOrDropWork()
		{
			var order = new List<string>();

			MainThreadDispatcher.Post(() =>
			{
				order.Add("outer");
				MainThreadDispatcher.Post(() => order.Add("next"));
				MainThreadDispatcher.ProcessQueue();
			});
			MainThreadDispatcher.Post(() => order.Add("second"));

			MainThreadDispatcher.ProcessQueue();
			CollectionAssert.AreEqual(new[] { "outer", "second" }, order);

			MainThreadDispatcher.ProcessQueue();
			CollectionAssert.AreEqual(new[] { "outer", "second", "next" }, order);
		}

		[Test]
		public void PostFromManyThreads_RunsEachOnce()
		{
			const int count = 2000;
			var runs = 0;

			Parallel.For(0, count, _ => MainThreadDispatcher.Post(() => runs++));
			MainThreadDispatcher.ProcessQueue();

			Assert.AreEqual(count, runs);
		}

		[Test]
		public void IsMainThread_TrueOnCapturedThreadOnly()
		{
			Assert.IsTrue(MainThreadDispatcher.IsMainThread);
			Assert.IsFalse(Task.Run(() => MainThreadDispatcher.IsMainThread).Result);
		}

		[Test]
		public void ResetState_DropsPendingWork()
		{
			var ran = false;

			MainThreadDispatcher.Post(() => ran = true);
			MainThreadDispatcher.ResetState();
			MainThreadDispatcher.ProcessQueue();

			Assert.IsFalse(ran);
		}

		[Test]
		public void Initialize_RunsOnSubsystemRegistration()
		{
			var method = typeof(MainThreadDispatcher).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.IsNotNull(method);

			var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
			Assert.IsNotNull(attribute);
			Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attribute.loadType);
		}
	}
}
