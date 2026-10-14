using System;
using Geuneda.Core;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	[TestFixture]
	public class Pcg32Test
	{
		// 기대값은 PCG 참조 구현(pcg32-demo)과 같은 알고리즘의 Python 구현으로 만들었습니다
		private const ulong Seed = 12345;

		[Test]
		public void FromSequence_MatchesPcgReferenceDemoVector()
		{
			// pcg32-demo: pcg32_srandom_r(&rng, 42u, 54u) 의 처음 여섯 출력
			var generator = Pcg32.FromSequence(42, 54);
			var expected = new uint[] { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };

			foreach (var value in expected)
			{
				Assert.AreEqual(value, generator.NextUInt());
			}
		}

		[Test]
		public void SplitMix64_MatchesReferenceOutputs()
		{
			// SplitMix64 를 상태 0 으로 시작했을 때의 처음 두 출력
			Assert.AreEqual(0xe220a8397b1dcdafUL, Pcg32.SplitMix64(0));
			Assert.AreEqual(0x6e789e6aa1b965f4UL, Pcg32.SplitMix64(0x9E3779B97F4A7C15UL));
		}

		[Test]
		public void Constructor_SeedAndStream_MatchesGoldenSequence()
		{
			AssertSequence(new Pcg32(Seed, 0), 2660500096, 1751544610, 1857258793, 3416782584, 1391937286);
			AssertSequence(new Pcg32(Seed, 7), 1671990731, 4072761750, 3429492346, 2073915029, 3175064787);
			AssertSequence(new Pcg32(0, 0), 2489713799, 3909064955, 2768585077);
		}

		[Test]
		public void Constructor_DifferentStreams_GiveDifferentSequences()
		{
			var first = new Pcg32(Seed, 1);
			var second = new Pcg32(Seed, 2);
			var same = 0;

			for (var i = 0; i < 100; i++)
			{
				if (first.NextUInt() == second.NextUInt())
				{
					same++;
				}
			}

			Assert.Less(same, 3);
		}

		[Test]
		public void Copy_AdvancesIndependently()
		{
			var original = new Pcg32(Seed, 0);
			var copy = original;

			var fromCopy = copy.NextUInt();

			Assert.AreEqual(0UL, original.Position);
			Assert.AreEqual(fromCopy, original.NextUInt());
		}

		[Test]
		public void Position_CountsEveryOutput()
		{
			var generator = new Pcg32(Seed, 0);

			Assert.AreEqual(0UL, generator.Position);

			generator.NextUInt();
			generator.NextInt(0, 10);

			Assert.AreEqual(2UL, generator.Position);
		}

		[Test]
		public void Advance_MatchesRepeatedNextUInt()
		{
			var stepped = new Pcg32(Seed, 0);
			var jumped = new Pcg32(Seed, 0);

			for (var i = 0; i < 1000; i++)
			{
				stepped.NextUInt();
			}

			jumped.Advance(1000);

			Assert.AreEqual(stepped.Position, jumped.Position);
			Assert.AreEqual(3322674188u, stepped.NextUInt());
			Assert.AreEqual(3322674188u, jumped.NextUInt());
		}

		[Test]
		public void Advance_ByTwoToThe64MinusN_GoesBackN()
		{
			var generator = new Pcg32(Seed, 3);
			var first = generator.NextUInt();
			var second = generator.NextUInt();

			generator.Advance(unchecked(0UL - 2));

			Assert.AreEqual(0UL, generator.Position);
			Assert.AreEqual(first, generator.NextUInt());
			Assert.AreEqual(second, generator.NextUInt());
		}

		[Test]
		public void Advance_Zero_KeepsState()
		{
			var generator = new Pcg32(Seed, 0);
			var expected = generator;

			generator.Advance(0);

			Assert.AreEqual(expected.NextUInt(), generator.NextUInt());
			Assert.AreEqual(1UL, generator.Position);
		}

		[Test]
		public void NextUIntBound_MatchesGoldenValuesIncludingRejections()
		{
			// bound = 3 * 2^30 이면 하위 값 2^30 개를 거절해 약 1/4 이 다시 뽑힙니다
			var generator = new Pcg32(Seed, 0);
			var expected = new uint[]
			{
				1313658457, 1392944094, 1043952964, 2566703048, 992221883, 1831694780, 1302212097, 616887919, 2028851697, 2457790498,
				456695419, 1299027970, 2977003177, 2634642323, 311520449, 2816469695, 2446623298, 251231131, 2596635861, 544562460
			};

			foreach (var value in expected)
			{
				Assert.AreEqual(value, generator.NextUInt(3u << 30));
			}

			Assert.AreEqual(30UL, generator.Position);
		}

		[Test]
		public void NextUIntBound_One_AlwaysZeroAndAdvancesOnce()
		{
			var generator = new Pcg32(Seed, 0);

			Assert.AreEqual(0u, generator.NextUInt(1));
			Assert.AreEqual(1UL, generator.Position);
		}

		[Test]
		public void NextUIntBound_Zero_Throws()
		{
			var generator = new Pcg32(Seed, 0);

			Assert.Throws<ArgumentOutOfRangeException>(() => generator.NextUInt(0));
		}

		[Test]
		public void NextInt_NegativeRange_MatchesGoldenValues()
		{
			var generator = new Pcg32(Seed, 0);

			AssertInts(() => generator.NextInt(-50, 50), 11, -10, -7, 29, -18, 29, -20, 6, -10, -31);
			Assert.AreEqual(10UL, generator.Position);
		}

		[Test]
		public void NextInt_NegativeRange_IsUnbiased()
		{
			// 이전 Knuth RngService 는 Range(-50, 50) 에서 0 이 약 2%, -50 이 거의 0% 였습니다(R1)
			var generator = new Pcg32(Seed, 0);
			var counts = new int[100];
			const int draws = 100000;

			for (var i = 0; i < draws; i++)
			{
				counts[generator.NextInt(-50, 50) + 50]++;
			}

			foreach (var count in counts)
			{
				Assert.GreaterOrEqual(count, 850);
				Assert.LessOrEqual(count, 1150);
			}
		}

		[Test]
		public void NextInt_ExtremeBounds_StayInRange()
		{
			var generator = new Pcg32(Seed, 0);

			for (var i = 0; i < 1000; i++)
			{
				var value = generator.NextInt(int.MinValue, int.MaxValue);

				Assert.Less(value, int.MaxValue);
			}

			Assert.AreEqual(int.MaxValue - 1, generator.NextInt(int.MaxValue - 1, int.MaxValue));
			Assert.AreEqual(int.MinValue, generator.NextInt(int.MinValue, int.MinValue + 1));
		}

		[Test]
		public void NextInt_EmptyOrInvertedRange_Throws()
		{
			var generator = new Pcg32(Seed, 0);

			Assert.Throws<ArgumentOutOfRangeException>(() => generator.NextInt(5, 5));
			Assert.Throws<ArgumentOutOfRangeException>(() => generator.NextInt(10, 5));
			Assert.AreEqual(0UL, generator.Position);
		}

		[Test]
		public void NextIntInclusive_MatchesGoldenValuesAndReachesMax()
		{
			var generator = new Pcg32(Seed, 0);

			AssertInts(() => generator.NextIntInclusive(0, 10), 6, 4, 4, 8, 3, 8, 3, 6, 4, 2);

			var sawMax = false;

			for (var i = 0; i < 1000 && !sawMax; i++)
			{
				sawMax = generator.NextIntInclusive(0, 10) == 10;
			}

			// 이전 Knuth RngService 는 maxInclusive 여도 max 를 사실상 돌려주지 않았습니다(R2)
			Assert.IsTrue(sawMax);
		}

		[Test]
		public void NextIntInclusive_FullIntRange_UsesRawOutput()
		{
			var generator = new Pcg32(Seed, 0);

			AssertInts(() => generator.NextIntInclusive(int.MinValue, int.MaxValue), -1634467200, 1751544610, 1857258793);
		}

		[Test]
		public void NextIntInclusive_SingleValue_ReturnsItAndAdvancesOnce()
		{
			var generator = new Pcg32(Seed, 0);

			Assert.AreEqual(7, generator.NextIntInclusive(7, 7));
			Assert.AreEqual(1UL, generator.Position);
		}

		[Test]
		public void NextIntInclusive_InvertedRange_Throws()
		{
			var generator = new Pcg32(Seed, 0);

			Assert.Throws<ArgumentOutOfRangeException>(() => generator.NextIntInclusive(10, 5));
		}

		[Test]
		public void Default_ThrowsInsteadOfReturningZeros()
		{
			var generator = default(Pcg32);

			Assert.Throws<InvalidOperationException>(() => generator.NextUInt());
			Assert.Throws<InvalidOperationException>(() => generator.Advance(1));
		}

		private static void AssertSequence(Pcg32 generator, params uint[] expected)
		{
			foreach (var value in expected)
			{
				Assert.AreEqual(value, generator.NextUInt());
			}
		}

		private static void AssertInts(Func<int> next, params int[] expected)
		{
			foreach (var value in expected)
			{
				Assert.AreEqual(value, next());
			}
		}
	}
}
