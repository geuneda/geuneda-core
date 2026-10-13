using System;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 환경(Dev, QA, Prod)마다 다른 값입니다. 설정 에셋의 필드로 두고 <see cref="Value"/> 로 이 빌드의 환경 값을 읽습니다.
	/// </summary>
	/// <remarks>
	/// 세 값이 모두 빌드에 들어갑니다. 운영 빌드에 넣으면 안 되는 비밀 값(키, 토큰)은 여기에 두지 않습니다.
	/// </remarks>
	[Serializable]
	public struct EnvValue<T>
	{
		[SerializeField] private T _dev;
		[SerializeField] private T _qa;
		[SerializeField] private T _prod;

		public EnvValue(T dev, T qa, T prod)
		{
			_dev = dev;
			_qa = qa;
			_prod = prod;
		}

		/// <summary>
		/// 이 빌드의 환경(<see cref="AppEnv.Current"/>) 값입니다.
		/// </summary>
		public T Value => Get(AppEnv.Current);

		/// <exception cref="ArgumentOutOfRangeException"><paramref name="environment"/> 가 정의되지 않은 값인 경우</exception>
		public T Get(AppEnvironment environment)
		{
			switch (environment)
			{
				case AppEnvironment.Dev:
					return _dev;
				case AppEnvironment.QA:
					return _qa;
				case AppEnvironment.Prod:
					return _prod;
				default:
					throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unknown environment");
			}
		}
	}
}
