using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 설정 에셋의 Resources 기준 경로(확장자 없이, 예: <c>Geuneda/GameConfig</c>)를 정합니다.
	/// 없으면 타입 이름을 Resources 루트 경로로 씁니다(<see cref="ConfigAssetLoader.GetPath"/>).
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class ConfigAssetPathAttribute : Attribute
	{
		public string Path { get; }

		/// <exception cref="ArgumentException"><paramref name="path"/> 가 비어 있거나 슬래시만 있는 경우</exception>
		public ConfigAssetPathAttribute(string path)
		{
			var normalized = path?.Replace('\\', '/').Trim().Trim('/');

			if (string.IsNullOrEmpty(normalized))
			{
				throw new ArgumentException("Config asset path must not be empty", nameof(path));
			}

			Path = normalized;
		}
	}
}
