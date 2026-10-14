using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 기기 시계(<see cref="DateTime.UtcNow"/>)와 따로 흐르는 단조 시계입니다. 사용자가 기기 시계를 바꿔도 영향받지 않고, 값이 줄어들지 않습니다.
	/// </summary>
	/// <remarks>
	/// 기준점은 정해져 있지 않으므로(보통 기기 부팅) 값 자체가 아니라 두 값의 차이로 경과 시간을 잽니다.
	/// 기본 구현은 <see cref="MonotonicClock"/> 입니다.
	/// </remarks>
	public interface IMonotonicClock
	{
		/// <summary>기준점 이후 흐른 시간입니다. 어느 스레드에서 읽어도 됩니다.</summary>
		TimeSpan Elapsed { get; }
	}
}
