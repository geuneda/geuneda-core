using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Editor
{
	/// <summary>
	/// 개인 덮어쓰기 설정 에셋(<c>*.user.*</c>)이 빌드에 들어갈 수 있는 곳(<c>Editor</c> 폴더 밖, 또는 StreamingAssets 안)에 있으면 플레이어 빌드를 실패시킵니다.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Editor 폴더 밖의 에셋은 Resources 에 있거나, 빌드 씬·다른 에셋이 참조하거나, Addressables 그룹에 들어 있으면 빌드 파일에 들어갑니다.
	/// 어떤 경로로 들어갈지 따로 추적하지 않고 Editor 폴더 밖의 <c>.user</c> 에셋을 모두 거부합니다.
	/// 개인 덮어쓰기는 <c>Editor/Resources</c> 폴더에 둡니다. 에디터에서는 <c>Resources.Load</c> 로 읽히고 빌드에서는 빠집니다.
	/// 단, StreamingAssets 는 안의 파일을 그대로 복사하므로 그 아래의 <c>Editor</c> 폴더도 빌드에 들어갑니다. 그래서 StreamingAssets 안은 Editor 예외 없이 거부합니다.
	/// </para>
	/// <para>
	/// 플레이어 빌드에서만 검사합니다. Addressables 콘텐츠 빌드만 따로 할 때는 검사하지 않습니다.
	/// </para>
	/// </remarks>
	internal sealed class UserConfigAssetBuildGuard : IPreprocessBuildWithReport
	{
		public int callbackOrder => 0;

		public void OnPreprocessBuild(BuildReport report)
		{
			var userAssets = FindUserAssetsThatCanShip(AssetDatabase.GetAllAssetPaths());

			if (userAssets.Count > 0)
			{
				throw new BuildFailedException(
					"Personal config overrides (*.user) outside an Editor folder (or inside StreamingAssets) can ship in the build " +
					"(Resources, scene references, Addressables, StreamingAssets). Move them under an Editor/Resources folder outside StreamingAssets:\n" +
					string.Join("\n", userAssets));
			}
		}

		/// <summary>
		/// <paramref name="assetPaths"/> 중 확장자를 뺀 이름이 <c>.user</c> 로 끝나고(대소문자 무시), 경로에 <c>Editor</c> 폴더가 없거나
		/// <c>Assets/StreamingAssets/</c> 아래에 있는 에셋 경로입니다. StreamingAssets 밖의 스크립트(<c>.cs</c>)는 빌드에 원본이 들어가지 않으므로 뺍니다.
		/// </summary>
		/// <remarks>
		/// <c>.user</c> 는 Unity 의 Resources 경로처럼 대소문자를 가리지 않습니다. <c>Editor</c> 는 정확히 일치할 때만 제외해, 모호하면 거부하는 쪽으로 둡니다.
		/// </remarks>
		internal static IReadOnlyList<string> FindUserAssetsThatCanShip(IEnumerable<string> assetPaths)
		{
			var found = new List<string>();

			foreach (var assetPath in assetPaths)
			{
				var path = assetPath.Replace('\\', '/');

				if (!Path.GetFileNameWithoutExtension(path).EndsWith(ConfigAssetLoader.UserSuffix, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				// StreamingAssets 는 파일을 그대로 복사하므로 Editor 폴더 예외를 적용하지 않는다
				var isStreamingAsset = path.StartsWith("Assets/StreamingAssets/", StringComparison.OrdinalIgnoreCase);

				// 스크립트는 어셈블리로 컴파일되고 원본은 빌드에 들어가지 않는다(예: Quantum 의 partial 파일 Frame.User.cs)
				if (!isStreamingAsset && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (isStreamingAsset || Array.IndexOf(path.Split('/'), "Editor") < 0)
				{
					found.Add(path);
				}
			}

			return found;
		}
	}
}
