using System;
using System.Security.Cryptography;
using System.Text;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 세이브 파일이나 로그 캐시처럼 기기에 두는 데이터를 암호화하고 변조를 검출합니다(AES-256-CBC + HMAC-SHA256, encrypt-then-MAC).
	/// </summary>
	/// <remarks>
	/// <para>
	/// 출력(버전 1)은 <c>버전 1바이트(1) || IV 16바이트 || 암호문(PKCS7, 16 의 배수) || 태그 32바이트</c> 입니다.
	/// 태그는 <c>HMAC-SHA256(MAC 키, 버전 || IV || 암호문 || 연관 데이터 || 연관 데이터 비트 길이 8바이트 빅엔디언)</c> 이고,
	/// 암호 키와 MAC 키는 32바이트 키에서 HKDF-SHA256(salt 없음, info <c>geuneda.core.aes-256-cbc-hmac-sha256.v1</c>)으로 나눕니다.
	/// 복호화는 태그를 상수 시간으로 비교해 맞을 때만 합니다.
	/// </para>
	/// <para>
	/// 연관 데이터는 암호화하지 않고 태그에만 넣는 값입니다(용도, 파일 이름 등). 같은 키로 여러 파일을 암호화할 때 넣으면
	/// 한 파일의 암호문을 다른 파일 자리에 옮겨 넣은 것을 검출합니다. 같은 파일의 이전 암호문으로 되돌린 것은 검출하지 못합니다.
	/// null 과 빈 배열은 같습니다.
	/// </para>
	/// <para>
	/// 상태가 없어 어느 스레드에서 불러도 됩니다. UnityEngine 을 참조하지 않으므로 서버(.NET)가 이 파일과
	/// <c>Runtime/Results/</c> 를 링크해 같은 형식을 읽고 쓸 수 있습니다.
	/// Unity Mono 의 <c>AesGcm</c> 은 <see cref="PlatformNotSupportedException"/> 이라 쓰지 않습니다(6000.3.11f1 확인).
	/// </para>
	/// </remarks>
	public static class AesEncryption
	{
		/// <summary>키 길이(바이트)입니다.</summary>
		public const int KeySize = 32;

		internal const byte Version = 1;
		internal const int IvSize = 16;
		internal const int TagSize = 32;
		internal const int HeaderSize = 1 + IvSize;

		private const int BlockSize = 16;
		private const int HashSize = 32;
		private const string KeyInfo = "geuneda.core.aes-256-cbc-hmac-sha256.v1";

		/// <summary>
		/// <paramref name="plaintext"/> 를 암호화합니다. IV 는 호출마다 새로 만들므로 같은 입력이어도 결과가 다릅니다.
		/// </summary>
		/// <param name="key"><see cref="KeySize"/> 바이트 키입니다.</param>
		/// <param name="plaintext">암호화할 데이터입니다. 빈 배열도 됩니다.</param>
		/// <param name="associatedData">태그에만 넣을 연관 데이터입니다. 복호화할 때 같은 값을 넘겨야 합니다.</param>
		/// <returns>버전, IV, 암호문, 태그를 이은 새 배열입니다.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="key"/> 나 <paramref name="plaintext"/> 가 null 인 경우</exception>
		/// <exception cref="ArgumentException"><paramref name="key"/> 길이가 <see cref="KeySize"/> 가 아닌 경우</exception>
		public static byte[] Encrypt(byte[] key, byte[] plaintext, byte[] associatedData = null)
		{
			var iv = new byte[IvSize];
			RandomNumberGenerator.Fill(iv);
			return Encrypt(key, plaintext, associatedData, iv);
		}

		/// <summary>
		/// <see cref="Encrypt(byte[], byte[], byte[])"/> 의 결과를 풉니다.
		/// </summary>
		/// <returns>
		/// 성공하면 평문입니다. 실패 코드는 다음과 같습니다.
		/// <list type="bullet">
		/// <item><see cref="ResultCodes.UnsupportedVersion"/>: 첫 바이트가 아는 버전이 아님(더 새 버전이 쓴 데이터, 암호화하지 않은 데이터 등)</item>
		/// <item><see cref="ResultCodes.InvalidFormat"/>: 길이가 형식에 맞지 않음(잘림, 덧붙임)</item>
		/// <item><see cref="ResultCodes.IntegrityCheckFailed"/>: 태그가 맞지 않음(변조, 다른 키, 다른 연관 데이터)</item>
		/// </list>
		/// </returns>
		/// <exception cref="ArgumentNullException"><paramref name="key"/> 나 <paramref name="data"/> 가 null 인 경우</exception>
		/// <exception cref="ArgumentException"><paramref name="key"/> 길이가 <see cref="KeySize"/> 가 아닌 경우</exception>
		public static Result<byte[]> Decrypt(byte[] key, byte[] data, byte[] associatedData = null)
		{
			ValidateKey(key);

			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}

			if (data.Length == 0)
			{
				return Result<byte[]>.Fail(ResultCodes.InvalidFormat, "Encrypted data is empty");
			}

			if (data[0] != Version)
			{
				return Result<byte[]>.Fail(ResultCodes.UnsupportedVersion, $"Unsupported encrypted data version {data[0]}");
			}

			var cipherLength = data.Length - HeaderSize - TagSize;

			if (cipherLength < BlockSize || cipherLength % BlockSize != 0)
			{
				return Result<byte[]>.Fail(ResultCodes.InvalidFormat, $"Invalid encrypted data length {data.Length}");
			}

			DeriveKeys(key, out var encryptionKey, out var macKey);

			try
			{
				var expectedTag = ComputeTag(macKey, data, HeaderSize + cipherLength, associatedData);
				var actualTag = new ReadOnlySpan<byte>(data, HeaderSize + cipherLength, TagSize);

				if (!CryptographicOperations.FixedTimeEquals(expectedTag, actualTag))
				{
					return Result<byte[]>.Fail(ResultCodes.IntegrityCheckFailed, "Encrypted data failed the integrity check");
				}

				var iv = new byte[IvSize];
				Buffer.BlockCopy(data, 1, iv, 0, IvSize);

				using (var aes = CreateAes())
				using (var decryptor = aes.CreateDecryptor(encryptionKey, iv))
				{
					byte[] plaintext;

					try
					{
						plaintext = decryptor.TransformFinalBlock(data, HeaderSize, cipherLength);
					}
					catch (CryptographicException exception)
					{
						// 태그가 맞았으므로 키를 가진 쪽이 잘못된 패딩을 만든 경우뿐이다
						return Result<byte[]>.Fail(ResultCodes.InvalidFormat, $"Invalid padding: {exception.Message}");
					}

					return Result<byte[]>.Ok(plaintext);
				}
			}
			finally
			{
				Array.Clear(encryptionKey, 0, encryptionKey.Length);
				Array.Clear(macKey, 0, macKey.Length);
			}
		}

		/// <summary>IV 를 정해 암호화합니다. 골든 벡터 테스트용이며 IV 를 다시 쓰면 안전하지 않습니다.</summary>
		internal static byte[] Encrypt(byte[] key, byte[] plaintext, byte[] associatedData, byte[] iv)
		{
			ValidateKey(key);

			if (plaintext == null)
			{
				throw new ArgumentNullException(nameof(plaintext));
			}

			DeriveKeys(key, out var encryptionKey, out var macKey);

			try
			{
				byte[] cipher;

				using (var aes = CreateAes())
				using (var encryptor = aes.CreateEncryptor(encryptionKey, iv))
				{
					cipher = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
				}

				var output = new byte[HeaderSize + cipher.Length + TagSize];
				output[0] = Version;
				Buffer.BlockCopy(iv, 0, output, 1, IvSize);
				Buffer.BlockCopy(cipher, 0, output, HeaderSize, cipher.Length);

				var tag = ComputeTag(macKey, output, HeaderSize + cipher.Length, associatedData);
				Buffer.BlockCopy(tag, 0, output, HeaderSize + cipher.Length, TagSize);
				return output;
			}
			finally
			{
				Array.Clear(encryptionKey, 0, encryptionKey.Length);
				Array.Clear(macKey, 0, macKey.Length);
			}
		}

		/// <summary>RFC 5869 HKDF-Extract(SHA-256)입니다. salt 가 null 이거나 비면 0 으로 채운 32바이트입니다.</summary>
		internal static byte[] HkdfExtract(byte[] salt, byte[] inputKeyMaterial)
		{
			using (var hmac = new HMACSHA256(salt == null || salt.Length == 0 ? new byte[HashSize] : salt))
			{
				return hmac.ComputeHash(inputKeyMaterial);
			}
		}

		/// <summary>RFC 5869 HKDF-Expand(SHA-256)입니다. <paramref name="length"/> 는 1 ~ 255*32 입니다.</summary>
		internal static byte[] HkdfExpand(byte[] pseudoRandomKey, byte[] info, int length)
		{
			if (length <= 0 || length > 255 * HashSize)
			{
				throw new ArgumentOutOfRangeException(nameof(length), length, "HKDF output length must be between 1 and 8160");
			}

			var output = new byte[length];
			var previous = Array.Empty<byte>();

			using (var hmac = new HMACSHA256(pseudoRandomKey))
			{
				try
				{
					for (var counter = 1; (counter - 1) * HashSize < length; counter++)
					{
						// T(n) = HMAC(PRK, T(n-1) || info || n). T(n) 는 출력(파생 키)의 일부라 쓰고 나면 지운다
						var input = new byte[previous.Length + info.Length + 1];
						Buffer.BlockCopy(previous, 0, input, 0, previous.Length);
						Buffer.BlockCopy(info, 0, input, previous.Length, info.Length);
						input[input.Length - 1] = (byte) counter;

						var current = hmac.ComputeHash(input);
						Array.Clear(input, 0, input.Length);
						Array.Clear(previous, 0, previous.Length);
						previous = current;

						var offset = (counter - 1) * HashSize;
						Buffer.BlockCopy(previous, 0, output, offset, Math.Min(HashSize, length - offset));
					}
				}
				finally
				{
					Array.Clear(previous, 0, previous.Length);
				}
			}

			return output;
		}

		private static void ValidateKey(byte[] key)
		{
			if (key == null)
			{
				throw new ArgumentNullException(nameof(key));
			}

			if (key.Length != KeySize)
			{
				throw new ArgumentException($"Key must be {KeySize} bytes but was {key.Length}", nameof(key));
			}
		}

		private static void DeriveKeys(byte[] key, out byte[] encryptionKey, out byte[] macKey)
		{
			byte[] pseudoRandomKey = null;
			byte[] material = null;

			try
			{
				pseudoRandomKey = HkdfExtract(null, key);
				material = HkdfExpand(pseudoRandomKey, Encoding.ASCII.GetBytes(KeyInfo), KeySize * 2);

				encryptionKey = new byte[KeySize];
				macKey = new byte[KeySize];
				Buffer.BlockCopy(material, 0, encryptionKey, 0, KeySize);
				Buffer.BlockCopy(material, KeySize, macKey, 0, KeySize);
			}
			finally
			{
				if (pseudoRandomKey != null)
				{
					Array.Clear(pseudoRandomKey, 0, pseudoRandomKey.Length);
				}

				if (material != null)
				{
					Array.Clear(material, 0, material.Length);
				}
			}
		}

		private static byte[] ComputeTag(byte[] macKey, byte[] data, int signedLength, byte[] associatedData)
		{
			var associatedLength = associatedData?.Length ?? 0;
			var associatedBits = (ulong) associatedLength * 8;
			var lengthBlock = new byte[8];

			for (var i = 0; i < 8; i++)
			{
				// 마스크로 자른다. 오버플로 검사를 켠 서버 프로젝트에서 (byte) 변환이 OverflowException 을 내지 않게
				lengthBlock[i] = (byte) ((associatedBits >> (56 - 8 * i)) & 0xFFUL);
			}

			using (var hmac = new HMACSHA256(macKey))
			{
				hmac.TransformBlock(data, 0, signedLength, null, 0);

				if (associatedLength > 0)
				{
					hmac.TransformBlock(associatedData, 0, associatedLength, null, 0);
				}

				// 연관 데이터 길이를 넣어 암호문과 연관 데이터의 경계를 옮겨도 같은 태그가 되지 않게 한다
				hmac.TransformFinalBlock(lengthBlock, 0, lengthBlock.Length);
				return hmac.Hash;
			}
		}

		private static Aes CreateAes()
		{
			var aes = Aes.Create();
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			return aes;
		}
	}
}
