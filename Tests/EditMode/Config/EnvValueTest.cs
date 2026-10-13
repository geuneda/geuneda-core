using System;
using Geuneda.Core;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	[TestFixture]
	public class EnvValueTest
	{
		[Test]
		public void Get_ReturnsValueOfEachEnvironment()
		{
			var value = new EnvValue<string>("dev-url", "qa-url", "prod-url");

			Assert.AreEqual("dev-url", value.Get(AppEnvironment.Dev));
			Assert.AreEqual("qa-url", value.Get(AppEnvironment.QA));
			Assert.AreEqual("prod-url", value.Get(AppEnvironment.Prod));
		}

		[Test]
		public void Value_UsesCurrentEnvironment()
		{
			var value = new EnvValue<int>(1, 2, 3);

			Assert.AreEqual(value.Get(AppEnv.Current), value.Value);
		}

		[Test]
		public void Get_UndefinedEnvironment_Throws()
		{
			var value = new EnvValue<int>(1, 2, 3);

			Assert.Throws<ArgumentOutOfRangeException>(() => value.Get(default));
		}

		[Test]
		public void SerializesAsAssetField()
		{
			var source = ScriptableObject.CreateInstance<TestConfigAsset>();
			var copy = ScriptableObject.CreateInstance<TestConfigAsset>();

			try
			{
				source.ServerUrl = new EnvValue<string>("dev-url", "qa-url", "prod-url");

				JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);

				Assert.AreEqual("dev-url", copy.ServerUrl.Get(AppEnvironment.Dev));
				Assert.AreEqual("qa-url", copy.ServerUrl.Get(AppEnvironment.QA));
				Assert.AreEqual("prod-url", copy.ServerUrl.Get(AppEnvironment.Prod));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(source);
				UnityEngine.Object.DestroyImmediate(copy);
			}
		}
	}
}
