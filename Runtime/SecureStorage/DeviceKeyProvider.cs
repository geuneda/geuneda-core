using System;
using System.Security.Cryptography;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// <see cref="PlatformSecureStore"/> 에 이름별 무작위 32바이트 키를 만들어 두고 돌려주는 기기 키 도우미입니다.
	/// 부트스트랩에서 만들어 services 의 <c>MainInstaller</c> 에 <see cref="IDeviceKeyProvider"/> 로 바인딩합니다
	/// (<c>MainInstaller.Bind&lt;IDeviceKeyProvider&gt;(new DeviceKeyProvider())</c>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// 키는 보안 저장소의 <c>geuneda.core.deviceKey.&lt;이름&gt;</c> 항목에 Base64 로 둡니다. 키가 기기 밖으로 나가지 않으므로
	/// 이 키로 암호화한 파일은 다른 기기로 옮기거나(백업 복원 포함) 키가 사라지면(Android 앱 재설치, 키스토어 키 분실) 풀 수 없습니다.
	/// </para>
	/// <para>
	/// 보안 저장소를 쓸 수 없으면 실패를 돌려주고, 고정 키나 메모리 키, 평문 저장으로 대신하지 않습니다.
	/// <list type="bullet">
	/// <item>키체인·키스토어가 없어 <see cref="PlatformSecureStore"/> 가 PlayerPrefs 평문을 쓰는 곳(에디터, Windows, macOS, Linux, WebGL, Android API 22 이하)은 저장소를 건드리지 않고 실패합니다. 에디터에서 시험하려면 개발용 <see cref="IDeviceKeyProvider"/> 를 바인딩합니다.</item>
	/// <item>읽기가 <see cref="SecureReadResult.Failed"/> 면 키가 있는지 모르므로 새로 만들지 않습니다(기존 키를 덮어써 이전 파일을 잃지 않게).</item>
	/// <item>새 키를 쓰지 못하면 그 항목을 지웁니다. 실패한 쓰기가 메모리에 값을 남기는 저장소에서 다음 호출이 다음 실행에 없을 키를 돌려주지 않게 하기 위해서입니다(Android 플러그인은 실패한 commit 의 메모리 값을 스스로 되돌림).</item>
	/// <item>새 키를 쓴 뒤 다시 읽어 같은 값이 나올 때만 돌려줍니다. 쓴 값을 읽을 수 없으면 그 키로 암호화한 파일을 다음 실행에서 풀 수 없기 때문입니다.</item>
	/// <item>저장된 값이 32바이트 키가 아니면 덮어쓰지 않고 실패합니다.</item>
	/// </list>
	/// </para>
	/// <para>
	/// <see cref="ISecureStore"/> 처럼 메인 스레드에서 부릅니다. 같은 저장소에 대한 호출이 메인 스레드에서 차례로 실행되므로
	/// 같은 이름의 키를 두 번 만들지 않습니다. 받은 키로 암호화하는 <see cref="AesEncryption"/> 은 어느 스레드에서 불러도 됩니다.
	/// </para>
	/// </remarks>
	public sealed class DeviceKeyProvider : IDeviceKeyProvider
	{
		internal const string KeyPrefix = "geuneda.core.deviceKey.";

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Core.DeviceKey");

		private readonly ISecureStore _store;
		private readonly bool _isStoreProtected;

		public DeviceKeyProvider() : this(new PlatformSecureStore())
		{
		}

		private DeviceKeyProvider(PlatformSecureStore store) : this(store, store.IsProtected)
		{
		}

		/// <param name="isStoreProtected">false 면 저장소를 건드리지 않고 <see cref="ResultCodes.SecureStoreUnavailable"/> 입니다.</param>
		internal DeviceKeyProvider(ISecureStore store, bool isStoreProtected)
		{
			_store = store ?? throw new ArgumentNullException(nameof(store));
			_isStoreProtected = isStoreProtected;
		}

		/// <inheritdoc />
		/// <exception cref="ArgumentException"><paramref name="name"/> 가 null 이거나 빈 문자열인 경우</exception>
		public Result<byte[]> GetOrCreate(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				throw new ArgumentException("Device key name must not be empty", nameof(name));
			}

			if (!_isStoreProtected)
			{
				return Unavailable(name, "this platform has no protected secure store (keychain or keystore)");
			}

			var storeKey = KeyPrefix + name;

			switch (_store.TryRead(storeKey, out var stored))
			{
				case SecureReadResult.Found:
					return Decode(name, stored);
				case SecureReadResult.Failed:
					return Unavailable(name, "the secure store could not be read");
			}

			// NotFound: 새로 만든다
			var key = new byte[AesEncryption.KeySize];
			RandomNumberGenerator.Fill(key);
			var encoded = Convert.ToBase64String(key);

			if (!_store.TryWrite(storeKey, encoded))
			{
				// 실패한 쓰기가 메모리에 남긴 값을 다음 호출이 영속 키로 돌려주지 않게 지운다
				if (!_store.TryDelete(storeKey))
				{
					Logger.Warning($"Device key '{name}': could not remove the value of a failed write");
				}

				return Unavailable(name, "the new key could not be written");
			}

			if (_store.TryRead(storeKey, out var written) != SecureReadResult.Found || written != encoded)
			{
				return Unavailable(name, "the new key could not be read back");
			}

			return Result<byte[]>.Ok(key);
		}

		private static Result<byte[]> Decode(string name, string stored)
		{
			byte[] key;

			try
			{
				key = Convert.FromBase64String(stored);
			}
			catch (FormatException)
			{
				key = null;
			}

			if (key == null || key.Length != AesEncryption.KeySize)
			{
				var message = $"Device key '{name}' in the secure store is not a {AesEncryption.KeySize}-byte key";
				Logger.Error(message);
				return Result<byte[]>.Fail(ResultCodes.InvalidFormat, message);
			}

			return Result<byte[]>.Ok(key);
		}

		private static Result<byte[]> Unavailable(string name, string reason)
		{
			var message = $"Device key '{name}' is unavailable: {reason}";
			Logger.Warning(message);
			return Result<byte[]>.Fail(ResultCodes.SecureStoreUnavailable, message);
		}
	}
}
