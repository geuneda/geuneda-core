using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Geuneda.Core;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	[TestFixture]
	public class AppIdentityTest
	{
		private sealed class MemoryStore : IKeyValueStore
		{
			public readonly Dictionary<string, string> Values = new Dictionary<string, string>();

			public string Get(string key)
			{
				return Values.TryGetValue(key, out var value) ? value : null;
			}

			public void Set(string key, string value)
			{
				Values[key] = value;
			}
		}

		private sealed class FakeSecureStore : ISecureStore
		{
			public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
			public bool FailRead;
			public bool FailAdd;
			public int AddCalls;

			public SecureReadResult TryRead(string key, out string value)
			{
				value = null;

				if (FailRead)
				{
					return SecureReadResult.Failed;
				}

				return Values.TryGetValue(key, out value) ? SecureReadResult.Found : SecureReadResult.NotFound;
			}

			public bool TryAdd(string key, string value)
			{
				AddCalls++;

				if (FailAdd || Values.ContainsKey(key))
				{
					return false;
				}

				Values[key] = value;
				return true;
			}
		}

		private static readonly Regex IdFormat = new Regex("^[0-9a-f]{32}$");

		private ILogSink _originalSink;

		[SetUp]
		public void SetUp()
		{
			// 경고 로그가 콘솔에 남아 테스트를 실패시키지 않도록 출력을 버린다
			_originalSink = Log.Sink;
			Log.Sink = new DiscardSink();
		}

		[TearDown]
		public void TearDown()
		{
			Log.Sink = _originalSink;
		}

		private sealed class DiscardSink : ILogSink
		{
			public void Write(in LogEntry entry)
			{
			}
		}

		[Test]
		public void InstallId_CreatedOnceAndReused()
		{
			var prefs = new MemoryStore();

			var first = new AppIdentity(prefs, null, () => "platform");
			var second = new AppIdentity(prefs, null, () => "platform");

			StringAssert.IsMatch(IdFormat.ToString(), first.InstallId);
			Assert.AreEqual(first.InstallId, second.InstallId);
			Assert.AreEqual(first.InstallId, prefs.Values[AppIdentity.InstallIdKey]);
		}

		[Test]
		public void InstallId_NewAfterAppDataIsCleared()
		{
			var first = new AppIdentity(new MemoryStore(), null, () => "platform");
			var reinstalled = new AppIdentity(new MemoryStore(), null, () => "platform");

			Assert.AreNotEqual(first.InstallId, reinstalled.InstallId);
		}

		[Test]
		public void DeviceId_WithoutSecureStore_UsesPlatformId()
		{
			Assert.AreEqual("platform-id", new AppIdentity(new MemoryStore(), null, () => "platform-id").DeviceId);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("n/a")]
		public void DeviceId_UnsupportedPlatformId_UsesStoredGuid(string platformId)
		{
			Assume.That(SystemInfo.unsupportedIdentifier, Is.EqualTo("n/a"));
			var prefs = new MemoryStore();

			var first = new AppIdentity(prefs, null, () => platformId);
			var second = new AppIdentity(prefs, null, () => platformId);

			StringAssert.IsMatch(IdFormat.ToString(), first.DeviceId);
			Assert.AreEqual(first.DeviceId, second.DeviceId);
			Assert.AreNotEqual(first.InstallId, first.DeviceId);
		}

		[Test]
		public void DeviceId_SecureStoreValue_SurvivesReinstall()
		{
			var keychain = new FakeSecureStore();

			var first = new AppIdentity(new MemoryStore(), keychain, () => "platform");
			var reinstalled = new AppIdentity(new MemoryStore(), keychain, () => "platform");

			StringAssert.IsMatch(IdFormat.ToString(), first.DeviceId);
			Assert.AreEqual(first.DeviceId, reinstalled.DeviceId);
			Assert.AreNotEqual(first.InstallId, reinstalled.InstallId);
			Assert.AreEqual(1, keychain.AddCalls);
		}

		[Test]
		public void DeviceId_SecureReadFails_UsesPlatformIdAndDoesNotOverwrite()
		{
			var keychain = new FakeSecureStore { FailRead = true };
			keychain.Values[AppIdentity.DeviceIdKey] = "existing";

			var identity = new AppIdentity(new MemoryStore(), keychain, () => "platform");

			Assert.AreEqual("platform", identity.DeviceId);
			Assert.AreEqual(0, keychain.AddCalls);
			Assert.AreEqual("existing", keychain.Values[AppIdentity.DeviceIdKey]);
		}

		[Test]
		public void DeviceId_SecureAddFails_UsesPlatformId()
		{
			var keychain = new FakeSecureStore { FailAdd = true };

			Assert.AreEqual("platform", new AppIdentity(new MemoryStore(), keychain, () => "platform").DeviceId);
		}

		[Test]
		public void ImplementsInterfaceForMainInstallerBinding()
		{
			// services 의 MainInstaller 는 인터페이스 타입으로만 바인딩한다
			IAppIdentity identity = new AppIdentity(new MemoryStore(), null, () => "platform");

			Assert.IsTrue(typeof(IAppIdentity).IsInterface);
			Assert.AreEqual("platform", identity.DeviceId);
		}

		[Test]
		public void NullArguments_Throw()
		{
			Assert.Throws<ArgumentNullException>(() => new AppIdentity(null, null, () => "platform"));
			Assert.Throws<ArgumentNullException>(() => new AppIdentity(new MemoryStore(), null, null));
		}

		[Test]
		public void PublicConstructor_InEditor_UsesPlayerPrefsAndSystemInfo()
		{
			var hadInstallId = PlayerPrefs.HasKey(AppIdentity.InstallIdKey);

			try
			{
				var identity = new AppIdentity();

				Assert.AreEqual(PlayerPrefs.GetString(AppIdentity.InstallIdKey), identity.InstallId);
				Assert.AreEqual(SystemInfo.deviceUniqueIdentifier, identity.DeviceId);
			}
			finally
			{
				if (!hadInstallId)
				{
					PlayerPrefs.DeleteKey(AppIdentity.InstallIdKey);
					PlayerPrefs.Save();
				}
			}
		}
	}
}
