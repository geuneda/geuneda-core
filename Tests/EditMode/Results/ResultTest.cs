using System;
using System.Threading.Tasks;
using Geuneda.Core;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	[TestFixture]
	public class ResultTest
	{
		[Test]
		public void Ok_IsSuccessWithSuccessCode()
		{
			var result = Result.Ok();

			Assert.IsTrue(result.IsSuccess);
			Assert.AreEqual(ResultCodes.Success, result.Code);
			Assert.IsNull(result.Message);
			Assert.AreEqual("Success", result.ToString());
		}

		[Test]
		public void Fail_KeepsCodeAndMessage()
		{
			var result = Result.Fail(ResultCodes.Timeout, "no response");

			Assert.IsFalse(result.IsSuccess);
			Assert.AreEqual(ResultCodes.Timeout, result.Code);
			Assert.AreEqual("no response", result.Message);
			Assert.AreEqual("Failure(3): no response", result.ToString());
			Assert.AreEqual("Failure(3)", Result.Fail(ResultCodes.Timeout).ToString());
		}

		[TestCase(0)]
		[TestCase(-1)]
		public void Fail_WithNonPositiveCode_Throws(int code)
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => Result.Fail(code));
			Assert.Throws<ArgumentOutOfRangeException>(() => Result<int>.Fail(code));
		}

		[Test]
		public void Default_IsUnknownFailure()
		{
			var result = default(Result);
			var typed = default(Result<int>);

			Assert.IsFalse(result.IsSuccess);
			Assert.AreEqual(ResultCodes.Unknown, result.Code);
			Assert.IsFalse(typed.IsSuccess);
			Assert.AreEqual(ResultCodes.Unknown, typed.Code);
			Assert.Throws<InvalidOperationException>(() => _ = typed.Value);
		}

		[Test]
		public void TypedOk_ExposesValue()
		{
			var result = Result<string>.Ok("gold");

			Assert.IsTrue(result.IsSuccess);
			Assert.AreEqual(ResultCodes.Success, result.Code);
			Assert.AreEqual("gold", result.Value);
			Assert.IsTrue(result.TryGetValue(out var value));
			Assert.AreEqual("gold", value);
			Assert.AreEqual("Success(gold)", result.ToString());
		}

		[Test]
		public void TypedOk_AllowsDefaultValue()
		{
			var result = Result<string>.Ok(null);

			Assert.IsTrue(result.IsSuccess);
			Assert.IsNull(result.Value);
		}

		[Test]
		public void TypedFail_ValueThrowsAndTryGetValueReturnsFalse()
		{
			var result = Result<int>.Fail(ResultCodes.InProgress, "busy");

			var exception = Assert.Throws<InvalidOperationException>(() => _ = result.Value);
			StringAssert.Contains("Failure(5): busy", exception.Message);
			Assert.IsFalse(result.TryGetValue(out var value));
			Assert.AreEqual(0, value);
			Assert.AreEqual("Failure(5): busy", result.ToString());
		}

		[Test]
		public void TypedResult_ConvertsToResultWithoutValue()
		{
			Result success = Result<int>.Ok(7);
			Result failure = Result<int>.Fail(ResultCodes.NetworkError, "offline");
			Result empty = default(Result<int>);

			Assert.IsTrue(success.IsSuccess);
			Assert.AreEqual(ResultCodes.NetworkError, failure.Code);
			Assert.AreEqual("offline", failure.Message);
			Assert.AreEqual(ResultCodes.Unknown, empty.Code);
		}

		[Test]
		public void FromException_MapsCommonExceptions()
		{
			Assert.AreEqual(ResultCodes.Cancelled, ResultCodes.FromException(new OperationCanceledException()));
			Assert.AreEqual(ResultCodes.Cancelled, ResultCodes.FromException(new TaskCanceledException()));
			Assert.AreEqual(ResultCodes.Timeout, ResultCodes.FromException(new TimeoutException()));
			Assert.AreEqual(ResultCodes.Unknown, ResultCodes.FromException(new InvalidOperationException()));
		}

		[Test]
		public void FromException_KeepsExceptionMessage()
		{
			var result = Result.FromException(new TimeoutException("server slow"));
			var typed = Result<int>.FromException(new OperationCanceledException("closed"));

			Assert.AreEqual(ResultCodes.Timeout, result.Code);
			Assert.AreEqual("server slow", result.Message);
			Assert.AreEqual(ResultCodes.Cancelled, typed.Code);
			Assert.AreEqual("closed", typed.Message);
		}

		[Test]
		public void FromException_WithNull_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => ResultCodes.FromException(null));
			Assert.Throws<ArgumentNullException>(() => Result.FromException(null));
			Assert.Throws<ArgumentNullException>(() => Result<int>.FromException(null));
		}
	}
}
