#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Editor
{
	/// <summary>
	/// Android Gradle 프로젝트를 만든 직후 <see cref="AndroidProguardRules.Rule"/> 을 unityLibrary 의 consumer 규칙 파일에 넣습니다.
	/// Android 빌드 대상일 때만 컴파일합니다(UnityEditor.Android).
	/// </summary>
	internal sealed class AndroidProguardRulesHook : IPostGenerateGradleAndroidProject
	{
		public int callbackOrder => 0;

		public void OnPostGenerateGradleAndroidProject(string path)
		{
			var file = Path.Combine(path, AndroidProguardRules.FileName);
			var existing = File.Exists(file) ? File.ReadAllText(file) : string.Empty;
			var updated = AndroidProguardRules.Apply(existing);

			if (updated != existing)
			{
				File.WriteAllText(file, updated);
			}
		}
	}
}
#endif
