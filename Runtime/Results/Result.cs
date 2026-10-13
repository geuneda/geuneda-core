using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 값이 없는 작업의 결과입니다. 정상적인 실패(네트워크, 타임아웃, 취소, 서버 거절 등)를 예외 대신 코드로 돌려줍니다.
	/// </summary>
	/// <remarks>
	/// <para>
	/// 성공이면 <see cref="Code"/> 가 <see cref="ResultCodes.Success"/>(0)이고, 실패 코드는 1 이상입니다(<see cref="ResultCodes"/>).
	/// </para>
	/// <para>
	/// <c>default(Result)</c> 는 성공이 아니라 <see cref="ResultCodes.Unknown"/> 실패입니다. 채우지 않은 결과가 성공으로 보이지 않게 하기 위해서입니다.
	/// </para>
	/// <para>
	/// 호출자가 코드를 고쳐야 하는 오용(잘못된 인자, 잘못된 상태에서 호출)은 결과로 돌려주지 않고 즉시 예외를 던집니다.
	/// </para>
	/// </remarks>
	public readonly struct Result
	{
		private readonly bool _isSuccess;
		private readonly int _code;

		public bool IsSuccess => _isSuccess;

		/// <summary>
		/// 성공이면 <see cref="ResultCodes.Success"/>, 실패면 1 이상의 코드입니다.
		/// </summary>
		public int Code => _isSuccess ? ResultCodes.Success : _code == ResultCodes.Success ? ResultCodes.Unknown : _code;

		/// <summary>
		/// 로그와 디버깅용 설명입니다. 없으면 null 입니다. 사용자에게 보여줄 문구는 <see cref="Code"/> 로 찾습니다.
		/// </summary>
		public string Message { get; }

		private Result(bool isSuccess, int code, string message)
		{
			_isSuccess = isSuccess;
			_code = code;
			Message = message;
		}

		public static Result Ok()
		{
			return new Result(true, ResultCodes.Success, null);
		}

		/// <exception cref="ArgumentOutOfRangeException"><paramref name="code"/> 가 1 미만인 경우</exception>
		public static Result Fail(int code, string message = null)
		{
			if (code <= ResultCodes.Success)
			{
				throw new ArgumentOutOfRangeException(nameof(code), code, "Failure code must be positive");
			}

			return new Result(false, code, message);
		}

		/// <summary>
		/// 예외를 <see cref="ResultCodes.FromException"/> 의 코드와 예외 메시지로 바꿉니다. 로그는 호출자가 남깁니다.
		/// </summary>
		/// <remarks>
		/// 정상 실패에 해당하는 예외(취소, 타임아웃 등)만 타입별로 잡아 넘기세요. 오용·버그 예외까지 넘기면 <see cref="ResultCodes.Unknown"/> 으로 숨겨집니다.
		/// </remarks>
		/// <exception cref="ArgumentNullException"><paramref name="exception"/> 이 null 인 경우</exception>
		public static Result FromException(Exception exception)
		{
			return Fail(ResultCodes.FromException(exception), exception.Message);
		}

		public override string ToString()
		{
			if (_isSuccess)
			{
				return "Success";
			}

			return string.IsNullOrEmpty(Message) ? $"Failure({Code})" : $"Failure({Code}): {Message}";
		}
	}

	/// <summary>
	/// 값을 돌려주는 작업의 결과입니다. 성공이면 <see cref="Value"/> 를, 실패면 코드와 메시지를 담습니다.
	/// </summary>
	/// <remarks>
	/// <see cref="Result"/> 와 같은 규칙을 따릅니다. <c>default(Result&lt;T&gt;)</c> 는 <see cref="ResultCodes.Unknown"/> 실패입니다.
	/// </remarks>
	public readonly struct Result<T>
	{
		private readonly Result _result;
		private readonly T _value;

		public bool IsSuccess => _result.IsSuccess;

		/// <inheritdoc cref="Result.Code"/>
		public int Code => _result.Code;

		/// <inheritdoc cref="Result.Message"/>
		public string Message => _result.Message;

		/// <summary>
		/// 성공한 값입니다.
		/// </summary>
		/// <exception cref="InvalidOperationException">실패한 결과인 경우. 먼저 <see cref="IsSuccess"/> 를 확인하거나 <see cref="TryGetValue"/> 를 씁니다.</exception>
		public T Value
		{
			get
			{
				if (!_result.IsSuccess)
				{
					throw new InvalidOperationException($"Result has no value: {_result}");
				}

				return _value;
			}
		}

		private Result(Result result, T value)
		{
			_result = result;
			_value = value;
		}

		/// <summary>
		/// 성공이면 값을 넘기고 true 를, 실패면 <c>default</c> 를 넘기고 false 를 돌려줍니다.
		/// </summary>
		public bool TryGetValue(out T value)
		{
			value = _value;
			return _result.IsSuccess;
		}

		public static Result<T> Ok(T value)
		{
			return new Result<T>(Result.Ok(), value);
		}

		/// <inheritdoc cref="Result.Fail"/>
		public static Result<T> Fail(int code, string message = null)
		{
			return new Result<T>(Result.Fail(code, message), default);
		}

		/// <inheritdoc cref="Result.FromException"/>
		public static Result<T> FromException(Exception exception)
		{
			return new Result<T>(Result.FromException(exception), default);
		}

		/// <summary>
		/// 값을 버리고 성공 여부, 코드, 메시지만 남깁니다. 실패를 값이 없는 결과로 그대로 넘길 때 씁니다.
		/// </summary>
		public static implicit operator Result(Result<T> result)
		{
			return result._result;
		}

		public override string ToString()
		{
			return _result.IsSuccess ? $"Success({_value})" : _result.ToString();
		}
	}
}
