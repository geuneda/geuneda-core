#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_ANDROID && !UNITY_EDITOR)
#define GEUNEDA_MONOTONIC_LIBC
#elif UNITY_IOS && !UNITY_EDITOR
#define GEUNEDA_MONOTONIC_IOS
#endif

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 플랫폼 기본 단조 시계입니다. 할 수 있는 플랫폼에서는 기기가 잠든 동안에도 흐르는 시계를 씁니다.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Android 는 <c>clock_gettime(CLOCK_BOOTTIME)</c>, macOS(에디터·플레이어)는 <c>clock_gettime(CLOCK_MONOTONIC)</c> 를 libc 에서 직접 부르고,
	/// iOS 는 <c>Plugins/iOS/GeunedaMonotonicClock.mm</c> 의 <c>clock_gettime_nsec_np(CLOCK_MONOTONIC)</c> 를 부릅니다.
	/// 셋 다 기기가 잠든 동안에도 흐릅니다(Darwin 의 <c>CLOCK_MONOTONIC</c> 은 잠자기를 포함하고, 포함하지 않는 쪽은 <c>CLOCK_UPTIME_RAW</c>).
	/// iOS 만 플러그인을 쓰는 것은 IL2CPP 가 <c>__Internal</c> 선언을 시스템 헤더 선언과 함께 컴파일하면 형식이 달라 충돌할 수 있어서입니다.
	/// </para>
	/// <para>
	/// 그 밖의 플랫폼(Windows, Linux, WebGL)과 네이티브 호출이 실패한 경우(생성할 때 한 번 확인)는 <see cref="Stopwatch"/> 를 씁니다.
	/// 이때 잠든 동안 흐르는지는 플랫폼에 따릅니다(<see cref="IncludesSleep"/> 가 false).
	/// 같은 기기에서는 인스턴스가 달라도 같은 시계를 읽으므로 값끼리 비교할 수 있습니다.
	/// </para>
	/// </remarks>
	public sealed class MonotonicClock : IMonotonicClock
	{
#if GEUNEDA_MONOTONIC_LIBC
#if UNITY_ANDROID && !UNITY_EDITOR
		private const int ClockId = 7; // Linux CLOCK_BOOTTIME
#else
		private const int ClockId = 6; // Darwin CLOCK_MONOTONIC
#endif

		// time_t 와 long 은 포인터 크기입니다(32비트 Android 에서는 32비트)
		[StructLayout(LayoutKind.Sequential)]
		private struct Timespec
		{
			public IntPtr Seconds;
			public IntPtr Nanoseconds;
		}

		[DllImport("libc", EntryPoint = "clock_gettime")]
		private static extern int ClockGetTime(int clockId, out Timespec time);
#elif GEUNEDA_MONOTONIC_IOS
		[DllImport("__Internal")]
		private static extern long GeunedaMonotonicClock_Nanoseconds();
#endif

		private readonly bool _native;

		/// <summary>기기가 잠든 동안에도 흐르는 네이티브 시계를 쓰는지입니다. false 면 <see cref="Stopwatch"/> 입니다.</summary>
		public bool IncludesSleep => _native;

		/// <inheritdoc />
		public TimeSpan Elapsed => _native ? ReadNative() : ReadStopwatch();

		public MonotonicClock()
		{
			_native = ProbeNative();
		}

		private static TimeSpan ReadStopwatch()
		{
			var timestamp = Stopwatch.GetTimestamp();
			var frequency = Stopwatch.Frequency;
			var seconds = timestamp / frequency;
			var remainder = timestamp % frequency;

			return new TimeSpan(seconds * TimeSpan.TicksPerSecond + remainder * TimeSpan.TicksPerSecond / frequency);
		}

#if GEUNEDA_MONOTONIC_LIBC
		private static bool ProbeNative()
		{
			try
			{
				return ClockGetTime(ClockId, out _) == 0;
			}
			catch (DllNotFoundException)
			{
				return false;
			}
			catch (EntryPointNotFoundException)
			{
				return false;
			}
		}

		private static TimeSpan ReadNative()
		{
			ClockGetTime(ClockId, out var time);

			return new TimeSpan(time.Seconds.ToInt64() * TimeSpan.TicksPerSecond + time.Nanoseconds.ToInt64() / 100);
		}
#elif GEUNEDA_MONOTONIC_IOS
		private static bool ProbeNative()
		{
			return true;
		}

		private static TimeSpan ReadNative()
		{
			return new TimeSpan(GeunedaMonotonicClock_Nanoseconds() / 100);
		}
#else
		private static bool ProbeNative()
		{
			return false;
		}

		private static TimeSpan ReadNative()
		{
			return ReadStopwatch();
		}
#endif
	}
}
