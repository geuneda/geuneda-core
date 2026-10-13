using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Geuneda.Core;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	/// <summary>
	/// README "결과 규약" 예제와 같은 모양의 클라이언트로 규약(정상 실패는 코드, 오용은 예외)을 확인합니다.
	/// </summary>
	[TestFixture]
	public class ResultConventionTest
	{
		private sealed class RecordingSink : ILogSink
		{
			public readonly List<LogEntry> Entries = new List<LogEntry>();

			public void Write(in LogEntry entry)
			{
				Entries.Add(entry);
			}
		}

		private sealed class ProfileClient
		{
			private readonly ModuleLogger _logger = new ModuleLogger("Test.Profile");
			private readonly Func<string, CancellationToken, Task<string>> _fetch;

			private bool _isLoading;

			public ProfileClient(Func<string, CancellationToken, Task<string>> fetch)
			{
				_fetch = fetch;
			}

			public async Task<Result<string>> LoadAsync(string userId, CancellationToken token)
			{
				if (string.IsNullOrEmpty(userId))
				{
					throw new ArgumentException("User id is empty", nameof(userId));
				}

				if (_isLoading)
				{
					return Result<string>.Fail(ResultCodes.InProgress);
				}

				_isLoading = true;
				try
				{
					return Result<string>.Ok(await _fetch(userId, token));
				}
				catch (OperationCanceledException)
				{
					return Result<string>.Fail(ResultCodes.Cancelled);
				}
				catch (TimeoutException exception)
				{
					_logger.Warning($"Profile request timed out: {exception.Message}");
					return Result<string>.FromException(exception);
				}
				catch (HttpRequestException exception)
				{
					_logger.Warning($"Profile request failed: {exception.Message}");
					return Result<string>.Fail(ResultCodes.NetworkError, exception.Message);
				}
				finally
				{
					_isLoading = false;
				}
			}
		}

		private ILogSink _originalSink;
		private RecordingSink _sink;

		[SetUp]
		public void SetUp()
		{
			_originalSink = Log.Sink;
			_sink = new RecordingSink();
			Log.Sink = _sink;
		}

		[TearDown]
		public void TearDown()
		{
			Log.Sink = _originalSink;
		}

		// 예제의 비동기 메서드는 이미 끝난 Task 만 기다리므로 동기적으로 완료된다
		private static Result<string> Load(ProfileClient client, string userId, CancellationToken token = default)
		{
			var task = client.LoadAsync(userId, token);
			Assert.IsTrue(task.IsCompleted);
			return task.GetAwaiter().GetResult();
		}

		private static Task<string> Throw(Exception exception)
		{
			return Task.FromException<string>(exception);
		}

		[Test]
		public void Success_ReturnsValue()
		{
			var client = new ProfileClient((id, token) => Task.FromResult($"profile-{id}"));

			Assert.AreEqual("profile-u1", Load(client, "u1").Value);
		}

		[Test]
		public void NetworkFailure_ReturnsNetworkErrorWithWarning()
		{
			var client = new ProfileClient((id, token) => Throw(new HttpRequestException("offline")));

			var result = Load(client, "u1");

			Assert.AreEqual(ResultCodes.NetworkError, result.Code);
			Assert.AreEqual("offline", result.Message);
			Assert.AreEqual(1, _sink.Entries.Count);
			Assert.AreEqual(LogLevel.Warning, _sink.Entries[0].Level);
		}

		[Test]
		public void Timeout_ReturnsTimeout()
		{
			var client = new ProfileClient((id, token) => Throw(new TimeoutException("slow")));

			Assert.AreEqual(ResultCodes.Timeout, Load(client, "u1").Code);
		}

		[Test]
		public void CallerCancellation_ReturnsCancelledWithoutLog()
		{
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();
			var client = new ProfileClient((id, token) => Task.FromCanceled<string>(token));

			var result = Load(client, "u1", cancellation.Token);

			Assert.AreEqual(ResultCodes.Cancelled, result.Code);
			CollectionAssert.IsEmpty(_sink.Entries);
		}

		[Test]
		public void ConcurrentCall_ReturnsInProgress()
		{
			var pending = new TaskCompletionSource<string>();
			var client = new ProfileClient((id, token) => pending.Task);

			var first = client.LoadAsync("u1", default);
			var second = Load(client, "u1");

			Assert.IsFalse(first.IsCompleted);
			Assert.AreEqual(ResultCodes.InProgress, second.Code);
		}

		[Test]
		public void Misuse_Propagates()
		{
			var client = new ProfileClient((id, token) => Task.FromResult(id));

			Assert.Throws<ArgumentException>(() => client.LoadAsync("", default).GetAwaiter().GetResult());
		}

		[Test]
		public void BugInLowerLayer_PropagatesAndReleasesInProgressFlag()
		{
			var disposed = true;
			var client = new ProfileClient((id, token) =>
				disposed ? Throw(new ObjectDisposedException("connection")) : Task.FromResult(id));

			Assert.Throws<ObjectDisposedException>(() => client.LoadAsync("u1", default).GetAwaiter().GetResult());
			CollectionAssert.IsEmpty(_sink.Entries);

			disposed = false;
			Assert.IsTrue(Load(client, "u1").IsSuccess);
		}
	}
}
