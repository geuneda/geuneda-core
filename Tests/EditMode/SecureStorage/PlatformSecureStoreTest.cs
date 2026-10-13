using System;
using Geuneda.Core;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	/// <summary>에디터에서는 PlayerPrefs 대체 저장소를 씁니다. iOS 키체인과 Android 키스토어는 기기에서 확인합니다.</summary>
	[TestFixture]
	public class PlatformSecureStoreTest
	{
		private string _key;
		private PlatformSecureStore _store;

		[SetUp]
		public void SetUp()
		{
			_key = "test." + Guid.NewGuid().ToString("N");
			_store = new PlatformSecureStore();
		}

		[TearDown]
		public void TearDown()
		{
			PlayerPrefs.DeleteKey(PlayerPrefsSecureStore.KeyPrefix + _key);
			PlayerPrefs.DeleteKey(_key);
			PlayerPrefs.Save();
		}

		[Test]
		public void TryRead_Missing_NotFound()
		{
			Assert.AreEqual(SecureReadResult.NotFound, _store.TryRead(_key, out var value));
			Assert.IsNull(value);
		}

		[TestCase("token-value")]
		[TestCase("")]
		[TestCase("토큰 {\"a\":1} é")]
		public void TryWrite_ThenRead_ReturnsValue(string written)
		{
			Assert.IsTrue(_store.TryWrite(_key, written));

			Assert.AreEqual(SecureReadResult.Found, _store.TryRead(_key, out var value));
			Assert.AreEqual(written, value);
		}

		[Test]
		public void TryWrite_Existing_Overwrites()
		{
			_store.TryWrite(_key, "first");

			Assert.IsTrue(_store.TryWrite(_key, "second"));

			_store.TryRead(_key, out var value);
			Assert.AreEqual("second", value);
		}

		[Test]
		public void TryDelete_RemovesValue()
		{
			_store.TryWrite(_key, "value");

			Assert.IsTrue(_store.TryDelete(_key));

			Assert.AreEqual(SecureReadResult.NotFound, _store.TryRead(_key, out _));
		}

		[Test]
		public void TryDelete_Missing_ReturnsTrue()
		{
			Assert.IsTrue(_store.TryDelete(_key));
		}

		[Test]
		public void Values_DoNotCollideWithPlainPlayerPrefsKeys()
		{
			PlayerPrefs.SetString(_key, "plain");

			Assert.AreEqual(SecureReadResult.NotFound, _store.TryRead(_key, out _));

			_store.TryWrite(_key, "secure");
			Assert.AreEqual("plain", PlayerPrefs.GetString(_key));
		}

		[Test]
		public void InvalidArguments_Throw()
		{
			Assert.Throws<ArgumentException>(() => _store.TryRead(null, out _));
			Assert.Throws<ArgumentException>(() => _store.TryRead("", out _));
			Assert.Throws<ArgumentException>(() => _store.TryWrite("", "value"));
			Assert.Throws<ArgumentNullException>(() => _store.TryWrite(_key, null));
			Assert.Throws<ArgumentException>(() => _store.TryDelete(null));
		}

		[Test]
		public void NulCharacters_AreRejected()
		{
			// 네이티브 저장소(C 문자열)에서 잘려 다른 키를 덮어쓰거나 값이 줄어드는 것을 막는다
			Assert.Throws<ArgumentException>(() => _store.TryRead(_key + "\0other", out _));
			Assert.Throws<ArgumentException>(() => _store.TryWrite(_key + "\0other", "value"));
			Assert.Throws<ArgumentException>(() => _store.TryDelete(_key + "\0other"));
			Assert.Throws<ArgumentException>(() => _store.TryWrite(_key, "left\0right"));
			Assert.AreEqual(SecureReadResult.NotFound, _store.TryRead(_key, out _));
		}

		[Test]
		public void ImplementsInterfaceForMainInstallerBinding()
		{
			ISecureStore store = _store;

			Assert.IsTrue(store.TryWrite(_key, "value"));
		}
	}
}
