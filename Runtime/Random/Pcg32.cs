using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// PCG32(XSH-RR) 결정론적 난수 생성기입니다. 시드와 스트림이 같으면 어느 플랫폼에서나 같은 수열을 냅니다.
	/// </summary>
	/// <remarks>
	/// <para>
	/// 상태는 64비트 LCG 상태, 스트림에서 정한 홀수 증가값, 지금까지 낸 32비트 출력 수(<see cref="Position"/>)입니다.
	/// 스트림 번호는 SplitMix64 로 섞어 증가값을 만들므로, 이웃한 스트림 번호끼리도 관련 없는 수열이 됩니다.
	/// 증가값은 섞은 값의 하위 63비트(<c>&lt;&lt; 1 | 1</c>)라, 섞은 값이 최상위 비트만 다른 번호 한 쌍은 같은 수열입니다.
	/// </para>
	/// <para>
	/// 가변 struct 입니다. 복사하면 상태가 따로 가므로 복사본으로 다음 값을 미리 볼 수 있습니다.
	/// 반대로 readonly 필드에 두거나 프로퍼티로 꺼내 부르면 복사본이 진행해 원래 생성기는 그대로입니다.
	/// 진행해야 하는 생성기는 readonly 가 아닌 필드나 지역 변수에 둡니다.
	/// </para>
	/// <para>
	/// 정수 연산만 쓰고 UnityEngine 을 참조하지 않아 서버(.NET)가 이 파일을 링크해 같은 수열을 재현할 수 있습니다.
	/// 오버플로 검사를 켠 프로젝트에서도 결과가 같도록 연산은 모두 unchecked 입니다. 스레드 안전하지 않습니다.
	/// </para>
	/// </remarks>
	public struct Pcg32
	{
		private const ulong Multiplier = 0x5851F42D4C957F2DUL;

		private ulong _state;
		private ulong _increment;
		private ulong _position;

		/// <summary>
		/// <paramref name="seed"/> 와 <paramref name="stream"/> 으로 생성기를 만듭니다. <see cref="Position"/> 은 0 입니다.
		/// </summary>
		/// <param name="seed">시작 상태를 정하는 시드입니다.</param>
		/// <param name="stream">수열을 고르는 스트림 번호입니다. 시드가 같아도 스트림이 다르면 다른 수열입니다(스트림 공간은 2^63, 클래스 설명 참고).</param>
		public Pcg32(ulong seed, ulong stream)
		{
			this = FromSequence(seed, SplitMix64(stream));
		}

		/// <summary>
		/// 만든 뒤 지금까지 낸 32비트 출력의 수입니다. 범위 함수는 거절 샘플링으로 한 번에 여러 개를 쓸 수 있습니다.
		/// <see cref="Advance"/> 와 같이 2^64 를 법으로 셉니다.
		/// </summary>
		public ulong Position => _position;

		/// <summary>
		/// 다음 32비트 값을 돌려주고 한 단계 진행합니다.
		/// </summary>
		/// <exception cref="InvalidOperationException">생성자로 만들지 않은 <c>default</c> 값이면 발생합니다.</exception>
		public uint NextUInt()
		{
			EnsureInitialized();

			unchecked
			{
				var old = _state;

				_state = old * Multiplier + _increment;
				_position++;

				var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
				var rotation = (int)(old >> 59);

				return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
			}
		}

		/// <summary>
		/// [0, <paramref name="bound"/>) 의 균등한 값을 돌려줍니다. Lemire 방식이라 편향이 없고,
		/// 드물게 거절된 출력만큼 더 진행합니다.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="bound"/> 가 0 이면 발생합니다.</exception>
		public uint NextUInt(uint bound)
		{
			if (bound == 0)
			{
				throw new ArgumentOutOfRangeException(nameof(bound), bound, "bound 는 1 이상이어야 합니다");
			}

			unchecked
			{
				var product = (ulong)NextUInt() * bound;
				var low = (uint)product;

				if (low < bound)
				{
					// 2^32 를 bound 로 나눈 나머지만큼의 하위 값을 거절하면 모든 결과가 같은 수의 출력에 대응합니다
					var threshold = (0u - bound) % bound;

					while (low < threshold)
					{
						product = (ulong)NextUInt() * bound;
						low = (uint)product;
					}
				}

				return (uint)(product >> 32);
			}
		}

		/// <summary>
		/// [<paramref name="min"/>, <paramref name="max"/>) 의 균등한 값을 돌려줍니다.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> 가 <paramref name="min"/> 보다 크지 않으면 발생합니다.</exception>
		public int NextInt(int min, int max)
		{
			if (min >= max)
			{
				throw new ArgumentOutOfRangeException(nameof(max), max, $"max({max})는 min({min})보다 커야 합니다");
			}

			unchecked
			{
				return (int)(min + NextUInt((uint)((long)max - min)));
			}
		}

		/// <summary>
		/// [<paramref name="min"/>, <paramref name="max"/>] 의 균등한 값을 돌려줍니다. 두 값이 같아도 한 단계 진행합니다.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> 가 <paramref name="min"/> 보다 작으면 발생합니다.</exception>
		public int NextIntInclusive(int min, int max)
		{
			if (min > max)
			{
				throw new ArgumentOutOfRangeException(nameof(max), max, $"max({max})는 min({min}) 이상이어야 합니다");
			}

			unchecked
			{
				var size = (long)max - min + 1;

				// int 전체 범위는 개수(2^32)가 uint 에 들어가지 않으므로 32비트 출력을 그대로 씁니다
				if (size > uint.MaxValue)
				{
					return (int)NextUInt();
				}

				return (int)(min + NextUInt((uint)size));
			}
		}

		/// <summary>
		/// <paramref name="delta"/> 단계를 O(log n) 으로 건너뜁니다. <see cref="NextUInt()"/> 를 그만큼 부른 것과 같습니다.
		/// </summary>
		/// <remarks>
		/// 주기가 2^64 라 뒤로 n 단계 가려면 <c>Advance(unchecked(0UL - n))</c> 처럼 2^64 - n 을 넘깁니다.
		/// 위치 p 로 옮기려면 <c>Advance(unchecked(p - Position))</c> 입니다.
		/// </remarks>
		/// <exception cref="InvalidOperationException">생성자로 만들지 않은 <c>default</c> 값이면 발생합니다.</exception>
		public void Advance(ulong delta)
		{
			EnsureInitialized();

			unchecked
			{
				var currentMultiplier = Multiplier;
				var currentIncrement = _increment;
				var accumulatedMultiplier = 1UL;
				var accumulatedIncrement = 0UL;
				var remaining = delta;

				while (remaining > 0)
				{
					if ((remaining & 1UL) != 0)
					{
						accumulatedMultiplier *= currentMultiplier;
						accumulatedIncrement = accumulatedIncrement * currentMultiplier + currentIncrement;
					}

					currentIncrement = (currentMultiplier + 1) * currentIncrement;
					currentMultiplier *= currentMultiplier;
					remaining >>= 1;
				}

				_state = accumulatedMultiplier * _state + accumulatedIncrement;
				_position += delta;
			}
		}

		/// <summary>
		/// PCG 참조 구현의 <c>pcg32_srandom_r(initState, initSequence)</c> 와 같은 생성기를 만듭니다.
		/// 공식 테스트 벡터를 확인하려고 스트림 번호를 섞지 않는 경로를 따로 둡니다.
		/// </summary>
		internal static Pcg32 FromSequence(ulong initState, ulong initSequence)
		{
			var generator = new Pcg32 { _increment = unchecked((initSequence << 1) | 1UL) };

			generator.Step();
			generator._state = unchecked(generator._state + initState);
			generator.Step();

			return generator;
		}

		/// <summary>
		/// SplitMix64 의 한 단계 출력입니다(Steele, Lea, Flood 2014).
		/// </summary>
		internal static ulong SplitMix64(ulong value)
		{
			unchecked
			{
				var z = value + 0x9E3779B97F4A7C15UL;

				z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
				z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;

				return z ^ (z >> 31);
			}
		}

		private void Step()
		{
			unchecked
			{
				_state = _state * Multiplier + _increment;
			}
		}

		private void EnsureInitialized()
		{
			// 생성자는 언제나 홀수 증가값을 넣으므로 0 은 default 값뿐입니다
			if (_increment == 0)
			{
				throw new InvalidOperationException("Pcg32 는 new Pcg32(seed, stream) 으로 만들어야 합니다. default 값은 쓸 수 없습니다");
			}
		}
	}
}
