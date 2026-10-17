using System;
using System.Linq;
using System.Text;
using Geuneda.Core;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	[TestFixture]
	public class AesEncryptionTest
	{
		// 골든 벡터는 Python cryptography(OpenSSL)의 HKDF·AES-CBC·HMAC 으로 따로 구현해 만들었습니다.
		// 키는 0x00..0x1f, IV 는 0xa0..0xaf 입니다
		private const string Golden1Plaintext = "geuneda aes golden vector";
		private const string Golden1Aad = "save/slot1";
		private const string Golden1 =
			"01a0a1a2a3a4a5a6a7a8a9aaabacadaeaf304691d19b165f819736b46d3003a315b3086fd6501bdad49c016db16c32911b" +
			"d55f77d8bc698b496de8519e936776844b23f03d58cb1cecc8105053c8053914";
		private const string Golden2EmptyNoAad =
			"01a0a1a2a3a4a5a6a7a8a9aaabacadaeaf7679979fc34a8e8ff6ddeb6d2168661833161a986722256f241506437dea49e4" +
			"6189ca407ba6e47e2db354fcb52e5be7";

		// 연관 데이터 0x00..0xff 반복 300바이트: 길이 블록의 두 바이트(0x09, 0x60)가 0 이 아니다
		private const string Golden4LongAad =
			"01a0a1a2a3a4a5a6a7a8a9aaabacadaeafc6caccc09246baec18a882ad443f004f98db283cafd799bb279df271f7361357" +
			"4a885e60120798da751389f34092d6e9";

		private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte) i).ToArray();
		private static readonly byte[] Iv = Enumerable.Range(0xA0, 16).Select(i => (byte) i).ToArray();

		[Test]
		public void Encrypt_FixedIv_MatchesGoldenVector()
		{
			var data = AesEncryption.Encrypt(Key, Utf8(Golden1Plaintext), Utf8(Golden1Aad), Iv);

			Assert.AreEqual(Golden1, ToHex(data));
			Assert.AreEqual(Golden2EmptyNoAad, ToHex(AesEncryption.Encrypt(Key, new byte[0], null, Iv)));
		}

		[Test]
		public void Decrypt_GoldenVector_ReturnsPlaintext()
		{
			var result = AesEncryption.Decrypt(Key, FromHex(Golden1), Utf8(Golden1Aad));

			Assert.IsTrue(result.IsSuccess, result.ToString());
			Assert.AreEqual(Golden1Plaintext, Encoding.UTF8.GetString(result.Value));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(15)]
		[TestCase(16)]
		[TestCase(17)]
		[TestCase(1000)]
		public void Encrypt_ThenDecrypt_RoundTrips(int length)
		{
			var plaintext = Enumerable.Range(0, length).Select(i => (byte) (i * 7)).ToArray();

			var data = AesEncryption.Encrypt(Key, plaintext);
			var result = AesEncryption.Decrypt(Key, data);

			Assert.IsTrue(result.IsSuccess, result.ToString());
			CollectionAssert.AreEqual(plaintext, result.Value);
			// 버전 1 + IV 16 + PKCS7 로 채운 암호문 + 태그 32
			Assert.AreEqual(1 + 16 + (length / 16 + 1) * 16 + 32, data.Length);
			Assert.AreEqual(1, data[0]);
		}

		[Test]
		public void Encrypt_SameInputTwice_UsesNewIv()
		{
			var plaintext = Utf8("same");

			var first = AesEncryption.Encrypt(Key, plaintext);
			var second = AesEncryption.Encrypt(Key, plaintext);

			CollectionAssert.AreNotEqual(first.Skip(1).Take(16), second.Skip(1).Take(16));
			CollectionAssert.AreNotEqual(first, second);
		}

		[Test]
		public void Decrypt_FlippedBitAnywhereAfterVersion_IntegrityCheckFailed()
		{
			var data = FromHex(Golden1);

			// IV(1..16), 암호문(17..48), 태그(49..80)의 모든 바이트
			for (var index = 1; index < data.Length; index++)
			{
				var tampered = (byte[]) data.Clone();
				tampered[index] ^= 0x01;

				var result = AesEncryption.Decrypt(Key, tampered, Utf8(Golden1Aad));

				Assert.AreEqual(ResultCodes.IntegrityCheckFailed, result.Code, $"byte {index}");
			}
		}

		[TestCase((byte) 0)]
		[TestCase((byte) 2)]
		[TestCase((byte) 0x7B)]
		[TestCase((byte) 0xFF)]
		public void Decrypt_OtherVersion_UnsupportedVersion(byte version)
		{
			var data = FromHex(Golden1);
			data[0] = version;

			Assert.AreEqual(ResultCodes.UnsupportedVersion, AesEncryption.Decrypt(Key, data, Utf8(Golden1Aad)).Code);
		}

		[Test]
		public void Decrypt_Empty_InvalidFormat()
		{
			Assert.AreEqual(ResultCodes.InvalidFormat, AesEncryption.Decrypt(Key, new byte[0]).Code);
		}

		[Test]
		public void Decrypt_EveryTruncation_Fails()
		{
			var data = AesEncryption.Encrypt(Key, new byte[40], Utf8(Golden1Aad));

			for (var length = 1; length < data.Length; length++)
			{
				var truncated = data.Take(length).ToArray();
				var result = AesEncryption.Decrypt(Key, truncated, Utf8(Golden1Aad));

				// 블록 경계에서 자른 것은 길이가 맞아 태그 검사에서 걸린다
				var cipherLength = length - 1 - 16 - 32;
				var expected = cipherLength >= 16 && cipherLength % 16 == 0 ? ResultCodes.IntegrityCheckFailed : ResultCodes.InvalidFormat;
				Assert.AreEqual(expected, result.Code, $"length {length}");
			}
		}

		[Test]
		public void Decrypt_AppendedBytes_Fails()
		{
			var data = FromHex(Golden1);

			Assert.AreEqual(ResultCodes.InvalidFormat, AesEncryption.Decrypt(Key, data.Concat(new byte[1]).ToArray(), Utf8(Golden1Aad)).Code);
			Assert.AreEqual(ResultCodes.IntegrityCheckFailed, AesEncryption.Decrypt(Key, data.Concat(new byte[16]).ToArray(), Utf8(Golden1Aad)).Code);
		}

		[Test]
		public void Decrypt_WrongKey_IntegrityCheckFailed()
		{
			var otherKey = (byte[]) Key.Clone();
			otherKey[31] ^= 0x80;

			Assert.AreEqual(ResultCodes.IntegrityCheckFailed, AesEncryption.Decrypt(otherKey, FromHex(Golden1), Utf8(Golden1Aad)).Code);
		}

		[Test]
		public void Decrypt_DifferentAssociatedData_IntegrityCheckFailed()
		{
			var data = AesEncryption.Encrypt(Key, Utf8("save"), Utf8("save/slot1"));

			Assert.AreEqual(ResultCodes.IntegrityCheckFailed, AesEncryption.Decrypt(Key, data, Utf8("save/slot2")).Code);
			Assert.AreEqual(ResultCodes.IntegrityCheckFailed, AesEncryption.Decrypt(Key, data).Code);
			Assert.IsTrue(AesEncryption.Decrypt(Key, data, Utf8("save/slot1")).IsSuccess);
		}

		[Test]
		public void Encrypt_LongAssociatedData_MatchesGoldenVector()
		{
			var aad = Enumerable.Range(0, 300).Select(i => (byte) i).ToArray();

			Assert.AreEqual(Golden4LongAad, ToHex(AesEncryption.Encrypt(Key, Utf8("long aad"), aad, Iv)));
			Assert.AreEqual("long aad", Encoding.UTF8.GetString(AesEncryption.Decrypt(Key, FromHex(Golden4LongAad), aad).Value));
		}

		[TestCase(31)]
		[TestCase(32)]
		[TestCase(33)]
		[TestCase(256)]
		public void Encrypt_AssociatedDataLength_RoundTrips(int length)
		{
			var aad = Enumerable.Range(0, length).Select(i => (byte) (i * 3)).ToArray();

			var result = AesEncryption.Decrypt(Key, AesEncryption.Encrypt(Key, Utf8("payload"), aad), aad);

			Assert.IsTrue(result.IsSuccess, result.ToString());
			Assert.AreEqual("payload", Encoding.UTF8.GetString(result.Value));
		}

		[Test]
		public void Decrypt_NullAndEmptyAssociatedData_AreSame()
		{
			Assert.IsTrue(AesEncryption.Decrypt(Key, AesEncryption.Encrypt(Key, Utf8("a")), new byte[0]).IsSuccess);
			Assert.IsTrue(AesEncryption.Decrypt(Key, AesEncryption.Encrypt(Key, Utf8("a"), new byte[0])).IsSuccess);
		}

		[Test]
		public void Decrypt_AssociatedDataBlockMovedIntoCiphertext_IntegrityCheckFailed()
		{
			// 연관 데이터 앞 16바이트를 암호문 끝으로 옮기면 HMAC 입력 바이트열은 같아진다. 길이 블록이 이것을 막는다
			var prefix = Enumerable.Range(0, 16).Select(i => (byte) (0x30 + i)).ToArray();
			var suffix = Utf8("slot");
			var data = AesEncryption.Encrypt(Key, Utf8("payload"), prefix.Concat(suffix).ToArray());

			var signed = data.Take(data.Length - 32);
			var tag = data.Skip(data.Length - 32);
			var moved = signed.Concat(prefix).Concat(tag).ToArray();

			Assert.AreEqual(ResultCodes.IntegrityCheckFailed, AesEncryption.Decrypt(Key, moved, suffix).Code);
		}

		[TestCase(0)]
		[TestCase(16)]
		[TestCase(31)]
		[TestCase(33)]
		public void WrongKeyLength_Throws(int length)
		{
			var key = new byte[length];

			Assert.Throws<ArgumentException>(() => AesEncryption.Encrypt(key, Utf8("a")));
			Assert.Throws<ArgumentException>(() => AesEncryption.Decrypt(key, FromHex(Golden1)));
		}

		[Test]
		public void NullArguments_Throw()
		{
			Assert.Throws<ArgumentNullException>(() => AesEncryption.Encrypt(null, Utf8("a")));
			Assert.Throws<ArgumentNullException>(() => AesEncryption.Encrypt(Key, null));
			Assert.Throws<ArgumentNullException>(() => AesEncryption.Decrypt(null, FromHex(Golden1)));
			Assert.Throws<ArgumentNullException>(() => AesEncryption.Decrypt(Key, null));
		}

		// RFC 5869 부록 A.1 ~ A.3 (SHA-256)
		[TestCase("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b", "000102030405060708090a0b0c", "f0f1f2f3f4f5f6f7f8f9", 42,
			"077709362c2e32df0ddc3f0dc47bba6390b6c73bb50f9c3122ec844ad7c2b3e5",
			"3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865")]
		[TestCase(
			"000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f404142434445464748494a4b4c4d4e4f",
			"606162636465666768696a6b6c6d6e6f707172737475767778797a7b7c7d7e7f808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9fa0a1a2a3a4a5a6a7a8a9aaabacadaeaf",
			"b0b1b2b3b4b5b6b7b8b9babbbcbdbebfc0c1c2c3c4c5c6c7c8c9cacbcccdcecfd0d1d2d3d4d5d6d7d8d9dadbdcdddedfe0e1e2e3e4e5e6e7e8e9eaebecedeeeff0f1f2f3f4f5f6f7f8f9fafbfcfdfeff",
			82,
			"06a6b88c5853361a06104c9ceb35b45cef760014904671014a193f40c15fc244",
			"b11e398dc80327a1c8e7f78c596a49344f012eda2d4efad8a050cc4c19afa97c59045a99cac7827271cb41c65e590e09da3275600c2f09b8367793a9aca3db71cc30c58179ec3e87c14c01d5c1f3434f1d87")]
		[TestCase("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b", "", "", 42,
			"19ef24a32c717b167f33a91d6f648bdf96596776afdb6377ac434c1c293ccb04",
			"8da4e775a563c18f715f802a063c5a31b8a11f5c5ee1879ec3454e5f3c738d2d9d201395faa4b61a96c8")]
		public void Hkdf_MatchesRfc5869(string ikm, string salt, string info, int length, string expectedPrk, string expectedOkm)
		{
			var prk = AesEncryption.HkdfExtract(FromHex(salt), FromHex(ikm));
			var okm = AesEncryption.HkdfExpand(prk, FromHex(info), length);

			Assert.AreEqual(expectedPrk, ToHex(prk));
			Assert.AreEqual(expectedOkm, ToHex(okm));
		}

		private static byte[] Utf8(string text)
		{
			return Encoding.UTF8.GetBytes(text);
		}

		private static byte[] FromHex(string hex)
		{
			var bytes = new byte[hex.Length / 2];

			for (var i = 0; i < bytes.Length; i++)
			{
				bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
			}

			return bytes;
		}

		private static string ToHex(byte[] bytes)
		{
			return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
		}
	}
}
