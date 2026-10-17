// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 이 기기에만 있는 암호 키를 이름별로 만들어 두고 돌려줍니다. 기본 구현은 <see cref="DeviceKeyProvider"/> 이고,
	/// services 의 <c>MainInstaller</c> 에 이 인터페이스로 바인딩합니다.
	/// </summary>
	public interface IDeviceKeyProvider
	{
		/// <summary>
		/// <paramref name="name"/> 의 키(<see cref="AesEncryption.KeySize"/> 바이트)를 돌려줍니다. 없으면 무작위로 만들어 저장한 뒤 돌려줍니다.
		/// </summary>
		/// <returns>
		/// 성공하면 호출마다 새로 만든 키 배열입니다. 키체인·키스토어가 없는 플레이어이거나(기본 구현은 에디터에서는 예외로 PlayerPrefs 평문에 둠) 보안 저장소를 읽거나 쓰지 못하면
		/// <see cref="ResultCodes.SecureStoreUnavailable"/>, 저장된 값이 키 형식이 아니면 <see cref="ResultCodes.InvalidFormat"/> 입니다.
		/// </returns>
		Result<byte[]> GetOrCreate(string name);
	}
}
