using Geuneda.Core.Editor;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	[TestFixture]
	public class AndroidProguardRulesTest
	{
		[TestCase("")]
		[TestCase(null)]
		[TestCase("-keep class com.unity3d.player.* { *; }")]
		[TestCase("-keep class com.unity3d.player.* { *; }\n")]
		public void Apply_AddsRuleOnItsOwnLine(string existing)
		{
			var updated = AndroidProguardRules.Apply(existing);

			StringAssert.StartsWith(existing ?? string.Empty, updated);
			StringAssert.Contains("\n" + AndroidProguardRules.Rule + "\n", "\n" + updated);
		}

		[Test]
		public void Apply_IsIdempotent()
		{
			var once = AndroidProguardRules.Apply("-dontwarn x\n");

			Assert.AreEqual(once, AndroidProguardRules.Apply(once));
		}
	}
}
