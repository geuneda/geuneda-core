using System;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 설정 에셋(ScriptableObject)을 경로 규칙으로 읽습니다. 읽는 순서는 에디터에서만 <c>경로.user</c>, 그다음 <c>Resources</c> 의 경로,
	/// 마지막으로 생성자에 넘긴 대체 로더입니다.
	/// </summary>
	/// <remarks>
	/// <para>
	/// 캐시를 두지 않습니다. 부트스트랩에서 한 번 읽어 services 의 <c>MainInstaller</c> 에 바인딩하고, 사용하는 쪽은 바인딩으로 받습니다.
	/// </para>
	/// <para>
	/// <c>.user</c> 는 개인 덮어쓰기(예: 내 PC 에서만 다른 서버 주소)입니다. <c>Editor/Resources</c> 폴더에 두고 git 에 올리지 않습니다.
	/// 에디터에서만 읽습니다. Editor 폴더 밖에 있으면 빌드 전 훅이 플레이어 빌드를 실패시킵니다(<c>Geuneda.Core.Editor</c>).
	/// </para>
	/// </remarks>
	public sealed class ConfigAssetLoader
	{
		/// <summary>개인 덮어쓰기 에셋 이름에 붙는 접미사입니다(<c>GameConfig.user.asset</c>).</summary>
		public const string UserSuffix = ".user";

#if UNITY_EDITOR
		private const bool IsEditor = true;
#else
		private const bool IsEditor = false;
#endif

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Core.ConfigAssets");

		private readonly Func<string, Object> _resourcesLoad;
		private readonly Func<string, Object> _fallbackLoad;
		private readonly bool _loadUserOverride;

		/// <param name="fallbackLoad">Resources 에 없을 때 경로로 에셋을 읽는 대리자(예: Addressables 동기 로드). 없으면 null</param>
		public ConfigAssetLoader(Func<string, Object> fallbackLoad = null)
			: this(Resources.Load, fallbackLoad, IsEditor)
		{
		}

		internal ConfigAssetLoader(Func<string, Object> resourcesLoad, Func<string, Object> fallbackLoad, bool loadUserOverride)
		{
			_resourcesLoad = resourcesLoad ?? throw new ArgumentNullException(nameof(resourcesLoad));
			_fallbackLoad = fallbackLoad;
			_loadUserOverride = loadUserOverride;
		}

		/// <summary>
		/// <paramref name="type"/> 의 Resources 기준 경로입니다. <see cref="ConfigAssetPathAttribute"/> 가 있으면 그 값, 없으면 타입 이름입니다.
		/// </summary>
		public static string GetPath(Type type)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			var attribute = (ConfigAssetPathAttribute)Attribute.GetCustomAttribute(type, typeof(ConfigAssetPathAttribute), false);

			return attribute != null ? attribute.Path : type.Name;
		}

		/// <summary>
		/// <typeparamref name="T"/> 설정 에셋을 읽습니다.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// 어디에서도 찾지 못했거나, 경로의 에셋이 <typeparamref name="T"/> 가 아닌 경우. 둘 다 프로젝트 구성 오류입니다
		/// </exception>
		public T Load<T>() where T : ScriptableObject
		{
			if (TryLoad<T>(out var asset))
			{
				return asset;
			}

			var path = GetPath(typeof(T));
			throw new InvalidOperationException(
				$"Config asset {typeof(T).Name} not found at Resources path '{path}'" + (_fallbackLoad != null ? " or by the fallback loader" : string.Empty));
		}

		/// <summary>
		/// <typeparamref name="T"/> 설정 에셋을 읽습니다. 어디에도 없으면 false 입니다.
		/// </summary>
		/// <exception cref="InvalidOperationException">경로의 에셋이 <typeparamref name="T"/> 가 아닌 경우(프로젝트 구성 오류)</exception>
		public bool TryLoad<T>(out T asset) where T : ScriptableObject
		{
			var path = GetPath(typeof(T));

			if (_loadUserOverride)
			{
				asset = Cast<T>(_resourcesLoad(path + UserSuffix), path + UserSuffix);

				if (asset != null)
				{
					Logger.Info($"Using user override '{path}{UserSuffix}' for {typeof(T).Name}");
					return true;
				}
			}

			asset = Cast<T>(_resourcesLoad(path), path);

			if (asset == null && _fallbackLoad != null)
			{
				asset = Cast<T>(_fallbackLoad(path), path);
			}

			return asset != null;
		}

		private static T Cast<T>(Object loaded, string path) where T : ScriptableObject
		{
			if (loaded == null)
			{
				return null;
			}

			if (loaded is T asset)
			{
				return asset;
			}

			throw new InvalidOperationException($"Config asset at '{path}' is {loaded.GetType().Name}, expected {typeof(T).Name}");
		}
	}
}
