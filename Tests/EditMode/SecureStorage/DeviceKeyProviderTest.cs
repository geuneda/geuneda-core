using System;
using System.Collections.Generic;
using System.Text;
using Geuneda.Core;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	[TestFixture]
	public class DeviceKeyProviderTest
	{
		/// <summary>
		/// 메모리 값(<see cref="Values"/>)과 다음 실행에 남는 값(<see cref="Persisted"/>)을 나눈 가짜 저장소.
		/// Android SharedPreferences 처럼 디스크 쓰기가 실패해도 메모리는 먼저 바뀐다(<see cref="FailPersist"/>).
		/// </summary>
		private sealed class FakeSecureStore : ISecureStore
		{
			public Dictionary<string, string> Values = new Dictionary<string, string>();
			public readonly Dictionary<string, string> Persisted = new Dictionary<string, string>();
			public bool FailRead;
			// 아무것도 바꾸지 않고 쓰기 실패
			public bool FailWrite;
			// 메모리는 바뀌지만 디스크 쓰기(와 삭제)가 실패해 false
			public bool FailPersist;
			// 쓰기는 성공했다고 답하지만 값을 남기지 않는 저장소(쓴 값을 다시 읽지 못하는 경우)
			public bool DropWrites;
			// 성공한 쓰기 뒤의 읽기를 Failed 로 답한다(쓴 직후 기기가 잠긴 경우)
			public bool FailReadAfterWrite;
			public int ReadCalls;
			public int WriteCalls;
			private bool _written;

			public SecureReadResult TryRead(string key, out string value)
			{
				ReadCalls++;
				value = null;

				if (FailRead || (FailReadAfterWrite && _written))
				{
					return SecureReadResult.Failed;
				}

				return Values.TryGetValue(key, out value) ? SecureReadResult.Found : SecureReadResult.NotFound;
			}

			public bool TryWrite(string key, string value)
			{
				WriteCalls++;

				if (FailWrite)
				{
					return false;
				}

				if (!DropWrites)
				{
					Values[key] = value;

					if (FailPersist)
					{
						return false;
					}

					Persisted[key] = value;
				}

				_written = true;
				return true;
			}

			public bool TryDelete(string key)
			{
				Values.Remove(key);

				if (FailPersist)
				{
					return false;
				}

				Persisted.Remove(key);
				return true;
			}

			/// <summary>앱을 다시 실행한 것처럼 메모리 값을 디스크 값으로 되돌린다.</summary>
			public void Restart()
			{
				Values = new Dictionary<string, string>(Persisted);
			}
		}

		private sealed class RecordingSink : ILogSink
		{
			public readonly List<LogEntry> Entries = new List<LogEntry>();

			public void Write(in LogEntry entry)
			{
				Entries.Add(entry);
			}
		}

		private const string StoreKey = DeviceKeyProvider.KeyPrefix + "save";

		private FakeSecureStore _store;
		private DeviceKeyProvider _provider;
		private ILogSink _originalSink;
		private RecordingSink _sink;

		[SetUp]
		public void SetUp()
		{
			// 경고·오류 로그가 콘솔에 남아 테스트를 실패시키지 않도록 기록용 출력으로 바꾼다
			_originalSink = Log.Sink;
			_sink = new RecordingSink();
			Log.Sink = _sink;
			_store = new FakeSecureStore();
			_provider = new DeviceKeyProvider(_store, true);
		}

		[TearDown]
		public void TearDown()
		{
			Log.Sink = _originalSink;
		}

		[Test]
		public void GetOrCreate_Missing_CreatesAndStoresKey()
		{
			var result = _provider.GetOrCreate("save");

			Assert.IsTrue(result.IsSuccess, result.ToString());
			Assert.AreEqual(AesEncryption.KeySize, result.Value.Length);
			Assert.AreEqual(Convert.ToBase64String(result.Value), _store.Values[StoreKey]);
			Assert.AreEqual(1, _store.WriteCalls);
		}

		[Test]
		public void GetOrCreate_Again_ReturnsSameKeyWithoutWriting()
		{
			var first = _provider.GetOrCreate("save").Value;

			var second = _provider.GetOrCreate("save").Value;
			// 다시 실행한 것처럼 새 인스턴스로 읽어도 같다
			_store.Restart();
			var restarted = new DeviceKeyProvider(_store, true).GetOrCreate("save").Value;

			CollectionAssert.AreEqual(first, second);
			CollectionAssert.AreEqual(first, restarted);
			Assert.AreEqual(1, _store.WriteCalls);
		}

		[Test]
		public void GetOrCreate_DifferentNames_DifferentKeys()
		{
			var save = _provider.GetOrCreate("save").Value;
			var log = _provider.GetOrCreate("log").Value;

			CollectionAssert.AreNotEqual(save, log);
			Assert.AreEqual(2, _store.Values.Count);
		}

		[Test]
		public void GetOrCreate_ReturnsNewArrayEachCall()
		{
			var first = _provider.GetOrCreate("save").Value;
			var copy = (byte[]) first.Clone();
			Array.Clear(first, 0, first.Length);

			CollectionAssert.AreEqual(copy, _provider.GetOrCreate("save").Value);
		}

		[Test]
		public void GetOrCreate_ReadFailed_FailsWithoutWriting()
		{
			_store.Values[StoreKey] = Convert.ToBase64String(new byte[32]);
			_store.FailRead = true;

			var result = _provider.GetOrCreate("save");

			Assert.AreEqual(ResultCodes.SecureStoreUnavailable, result.Code);
			Assert.AreEqual(0, _store.WriteCalls);
			Assert.AreEqual(LogLevel.Warning, _sink.Entries[0].Level);
		}

		[Test]
		public void GetOrCreate_WriteFailed_Fails()
		{
			_store.FailWrite = true;

			Assert.AreEqual(ResultCodes.SecureStoreUnavailable, _provider.GetOrCreate("save").Code);
			Assert.IsFalse(_store.Values.ContainsKey(StoreKey));
		}

		[Test]
		public void GetOrCreate_WrittenKeyNotReadable_Fails()
		{
			_store.DropWrites = true;

			Assert.AreEqual(ResultCodes.SecureStoreUnavailable, _provider.GetOrCreate("save").Code);
		}

		[Test]
		public void GetOrCreate_ReadBackFailed_FailsThenUsesStoredKeyLater()
		{
			_store.FailReadAfterWrite = true;

			Assert.AreEqual(ResultCodes.SecureStoreUnavailable, _provider.GetOrCreate("save").Code);

			// 쓴 값은 남아 있으므로 저장소가 다시 읽히면 그 키를 돌려주고 새로 만들지 않는다
			_store.FailReadAfterWrite = false;
			var result = _provider.GetOrCreate("save");

			Assert.IsTrue(result.IsSuccess, result.ToString());
			Assert.AreEqual(_store.Values[StoreKey], Convert.ToBase64String(result.Value));
			Assert.AreEqual(1, _store.WriteCalls);
		}

		[TestCase("not base64 !")]
		[TestCase("AAAA")]
		[TestCase("")]
		public void GetOrCreate_StoredValueNotKey_InvalidFormatWithoutOverwriting(string stored)
		{
			_store.Values[StoreKey] = stored;

			var result = _provider.GetOrCreate("save");

			Assert.AreEqual(ResultCodes.InvalidFormat, result.Code);
			Assert.AreEqual(stored, _store.Values[StoreKey]);
			Assert.AreEqual(0, _store.WriteCalls);
			Assert.AreEqual(LogLevel.Error, _sink.Entries[0].Level);
		}

		[Test]
		public void GetOrCreate_KeyEncryptsAndDecrypts()
		{
			var key = _provider.GetOrCreate("save").Value;
			var data = AesEncryption.Encrypt(key, Encoding.UTF8.GetBytes("save data"));

			var sameKey = _provider.GetOrCreate("save").Value;

			Assert.AreEqual("save data", Encoding.UTF8.GetString(AesEncryption.Decrypt(sameKey, data).Value));
		}

		[Test]
		public void InvalidArguments_Throw()
		{
			Assert.Throws<ArgumentNullException>(() => new DeviceKeyProvider(null, true));
			Assert.Throws<ArgumentException>(() => _provider.GetOrCreate(null));
			Assert.Throws<ArgumentException>(() => _provider.GetOrCreate(string.Empty));
		}

		[Test]
		public void GetOrCreate_FailedWriteLeftValueInMemory_NotReturnedAsKey()
		{
			_store.FailPersist = true;

			Assert.AreEqual(ResultCodes.SecureStoreUnavailable, _provider.GetOrCreate("save").Code);
			// 실패한 쓰기가 메모리에 남긴 값을 지웠으므로 같은 실행의 재시도도 그 키를 돌려주지 않는다
			Assert.IsFalse(_store.Values.ContainsKey(StoreKey));
			Assert.AreEqual(ResultCodes.SecureStoreUnavailable, _provider.GetOrCreate("save").Code);
			Assert.AreEqual(ResultCodes.SecureStoreUnavailable, new DeviceKeyProvider(_store, true).GetOrCreate("save").Code);

			_store.FailPersist = false;
			var created = _provider.GetOrCreate("save").Value;
			_store.Restart();

			CollectionAssert.AreEqual(created, new DeviceKeyProvider(_store, true).GetOrCreate("save").Value);
		}

		[Test]
		public void GetOrCreate_UnprotectedStore_FailsWithoutTouchingStore()
		{
			var provider = new DeviceKeyProvider(_store, false);

			Assert.AreEqual(ResultCodes.SecureStoreUnavailable, provider.GetOrCreate("save").Code);
			Assert.AreEqual(0, _store.ReadCalls);
			Assert.AreEqual(0, _store.WriteCalls);
		}

		[Test]
		public void GetOrCreate_DefaultConstructorInEditor_StoresKeyInPlayerPrefs()
		{
			// 에디터의 PlatformSecureStore 는 PlayerPrefs 평문이지만, 에디터에서만 개발용으로 기기 키를 둔다
			var name = "test." + Guid.NewGuid().ToString("N");
			var prefsKey = PlayerPrefsSecureStore.KeyPrefix + DeviceKeyProvider.KeyPrefix + name;
			Assert.IsFalse(new PlatformSecureStore().IsProtected);

			try
			{
				var first = new DeviceKeyProvider().GetOrCreate(name);
				var second = new DeviceKeyProvider().GetOrCreate(name);

				Assert.IsTrue(first.IsSuccess, first.ToString());
				Assert.AreEqual(AesEncryption.KeySize, first.Value.Length);
				Assert.IsTrue(PlayerPrefs.HasKey(prefsKey));
				Assert.IsTrue(second.IsSuccess, second.ToString());
				CollectionAssert.AreEqual(first.Value, second.Value);
			}
			finally
			{
				PlayerPrefs.DeleteKey(prefsKey);
				PlayerPrefs.Save();
			}
		}
	}
}
