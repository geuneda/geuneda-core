using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 모든 Geuneda 패키지가 함께 쓰는 결과 코드입니다(1~999).
	/// </summary>
	/// <remarks>
	/// 패키지 고유 코드는 패키지마다 받은 1000 단위 블록 안에서, 게임 코드는 101000 이상에서 정의합니다(100000~100999 는 실시간 RPC 서버 프레임워크).
	/// 범위 표는 패키지 README 의 "결과 코드 범위" 절에 있습니다.
	/// </remarks>
	public static class ResultCodes
	{
		/// <summary>성공. 실패 코드로는 쓸 수 없습니다.</summary>
		public const int Success = 0;
		/// <summary>분류하지 않은 실패입니다. 예외를 코드로 바꿀 때의 기본값이고, <c>default(Result)</c> 의 코드입니다.</summary>
		public const int Unknown = 1;
		/// <summary>호출자나 사용자가 작업을 취소했습니다.</summary>
		public const int Cancelled = 2;
		/// <summary>정해진 시간 안에 끝나지 않았습니다.</summary>
		public const int Timeout = 3;
		/// <summary>네트워크에 연결할 수 없거나 연결이 끊겼습니다.</summary>
		public const int NetworkError = 4;
		/// <summary>같은 작업이 이미 진행 중이라 이번 요청을 거절했습니다.</summary>
		public const int InProgress = 5;
		/// <summary>데이터 형식이 맞지 않습니다(잘림, 길이 오류, 잘못된 인코딩 등).</summary>
		public const int InvalidFormat = 6;
		/// <summary>데이터의 형식 버전을 지원하지 않습니다(더 새 버전이 쓴 데이터 등).</summary>
		public const int UnsupportedVersion = 7;
		/// <summary>무결성 검사에 실패했습니다(변조, 다른 키, 다른 연관 데이터).</summary>
		public const int IntegrityCheckFailed = 8;
		/// <summary>보안 저장소(키체인, 키스토어)를 읽거나 쓸 수 없습니다. 나중에 다시 시도할 수 있습니다.</summary>
		public const int SecureStoreUnavailable = 9;

		/// <summary>
		/// 예외를 공용 코드로 바꿉니다. <see cref="OperationCanceledException"/> 는 <see cref="Cancelled"/>,
		/// <see cref="TimeoutException"/> 은 <see cref="Timeout"/>, 그 밖은 <see cref="Unknown"/> 입니다.
		/// </summary>
		/// <remarks>
		/// <para>
		/// 코드로 바꾸기만 하고, 그 예외를 결과로 돌려줘도 되는지는 판단하지 않습니다. 정상 실패에 해당하는 예외만 타입별로 잡아 넘기세요.
		/// <c>catch (Exception)</c> 으로 모두 넘기면 오용과 버그가 <see cref="Unknown"/> 으로 숨겨집니다.
		/// </para>
		/// <para>
		/// 네트워크 예외는 종류가 SDK 마다 달라 여기서 분류하지 않습니다. 통신 패키지가 자기 예외를 <see cref="NetworkError"/> 로 바꿉니다.
		/// </para>
		/// </remarks>
		/// <exception cref="ArgumentNullException"><paramref name="exception"/> 이 null 인 경우</exception>
		public static int FromException(Exception exception)
		{
			switch (exception)
			{
				case null:
					throw new ArgumentNullException(nameof(exception));
				case OperationCanceledException _:
					return Cancelled;
				case TimeoutException _:
					return Timeout;
				default:
					return Unknown;
			}
		}
	}
}
