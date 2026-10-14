using System;
using System.Diagnostics;
using System.Threading;
using Geuneda.Core;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	[TestFixture]
	public class MonotonicClockTest
	{
		[Test]
		public void Elapsed_NeverDecreases()
		{
			var clock = new MonotonicClock();
			var previous = clock.Elapsed;

			for (var i = 0; i < 10000; i++)
			{
				var current = clock.Elapsed;

				Assert.GreaterOrEqual(current, previous);
				previous = current;
			}
		}

		[Test]
		public void Elapsed_AdvancesWithRealTime()
		{
			var clock = new MonotonicClock();
			var stopwatch = Stopwatch.StartNew();
			var start = clock.Elapsed;

			Thread.Sleep(200);

			var elapsed = clock.Elapsed - start;
			stopwatch.Stop();

			Assert.AreEqual(stopwatch.Elapsed.TotalMilliseconds, elapsed.TotalMilliseconds, 50);
		}

		[Test]
		public void Elapsed_SameAcrossInstances()
		{
			var first = new MonotonicClock();
			var second = new MonotonicClock();

			var a = first.Elapsed;
			var b = second.Elapsed;
			var c = first.Elapsed;

			Assert.GreaterOrEqual(b, a);
			Assert.GreaterOrEqual(c, b);
		}

		[Test]
		public void IncludesSleep_MatchesEditorPlatform()
		{
			var clock = new MonotonicClock();

#if UNITY_EDITOR_OSX
			Assert.IsTrue(clock.IncludesSleep, "macOS 에디터는 libc CLOCK_MONOTONIC 을 써야 합니다");
#else
			Assert.IsFalse(clock.IncludesSleep, "macOS 밖의 에디터는 Stopwatch 를 씁니다");
#endif
		}
	}
}
