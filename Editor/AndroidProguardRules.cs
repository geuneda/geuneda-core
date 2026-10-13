using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Editor
{
	/// <summary>
	/// C# 가 JNI 로 클래스·메서드 이름만 써서 부르는 Java 플러그인(<c>GeunedaSecureStore</c>)을 R8 축소(Minify)가 지우거나 이름을 바꾸지 않게 하는 keep 규칙입니다.
	/// </summary>
	internal static class AndroidProguardRules
	{
		/// <summary>생성된 Gradle 프로젝트의 unityLibrary 에 있고, unityLibrary 의 <c>consumerProguardFiles</c> 라 앱 축소에 적용됩니다.</summary>
		public const string FileName = "proguard-unity.txt";

		public const string Rule = "-keep class com.geuneda.core.GeunedaSecureStore { public static *; }";

		/// <summary>규칙이 없으면 끝에 붙인 내용을, 이미 있으면 그대로 돌려줍니다.</summary>
		public static string Apply(string existing)
		{
			existing ??= string.Empty;

			if (existing.Contains(Rule))
			{
				return existing;
			}

			var separator = existing.Length == 0 || existing.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : "\n";
			return existing + separator + "# Geuneda.Core: C# 에서 JNI 로 부르는 보안 저장소\n" + Rule + "\n";
		}
	}
}
