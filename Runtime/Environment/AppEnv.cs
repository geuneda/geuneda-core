// 환경은 컴파일 상수다. 빌드마다 Scripting Define Symbols(또는 Build Profile)에 많아야 하나를 정의한다
#if (GEUNEDA_ENV_DEV && GEUNEDA_ENV_QA) || (GEUNEDA_ENV_DEV && GEUNEDA_ENV_PROD) || (GEUNEDA_ENV_QA && GEUNEDA_ENV_PROD)
#error Define only one of GEUNEDA_ENV_DEV, GEUNEDA_ENV_QA, GEUNEDA_ENV_PROD.
#endif

// ReSharper disable once CheckNamespace

namespace Geuneda.Core
{
	/// <summary>
	/// 이 빌드의 환경(<see cref="AppEnvironment"/>)입니다. 값은 컴파일할 때 define 으로 정해지고 실행 중에는 바뀌지 않습니다.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>GEUNEDA_ENV_PROD</c>, <c>GEUNEDA_ENV_QA</c>, <c>GEUNEDA_ENV_DEV</c> 중 정의된 것을 씁니다. 둘 이상 정의하면 컴파일 오류입니다.
	/// 하나도 없으면 에디터와 개발 빌드는 <see cref="AppEnvironment.Dev"/>, 릴리스 빌드는 스토어 빌드로 보고 <see cref="AppEnvironment.Prod"/> 입니다.
	/// </para>
	/// <para>
	/// define 이 없는 릴리스 빌드를 컴파일 오류로 막지 않는 이유: Addressables 콘텐츠 빌드는 개발 빌드 옵션과 추가 define 없이
	/// 플레이어 스크립트를 컴파일하므로, 막으면 환경 define 이 없는 프로젝트의 콘텐츠 빌드가 실패합니다.
	/// </para>
	/// <para>
	/// 운영 빌드에서 코드를 확실히 빼야 하면(치트 메뉴 등) 이 프로퍼티로 분기하지 말고
	/// <c>#if !GEUNEDA_ENV_PROD &amp;&amp; (UNITY_EDITOR || DEVELOPMENT_BUILD || GEUNEDA_ENV_DEV || GEUNEDA_ENV_QA)</c> 로 감쌉니다.
	/// 이 조건은 <see cref="Current"/> 가 Prod 가 아닌 경우와 같습니다. define 없는 릴리스 빌드도 Prod 이므로 <c>#if !GEUNEDA_ENV_PROD</c> 만으로는 빠지지 않고,
	/// PROD 를 넣은 개발 빌드·에디터도 Prod 이므로 <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c> 만으로도 안 됩니다.
	/// </para>
	/// </remarks>
	public static class AppEnv
	{
#if GEUNEDA_ENV_PROD
		private const bool ProdDefined = true;
#else
		private const bool ProdDefined = false;
#endif
#if GEUNEDA_ENV_QA
		private const bool QADefined = true;
#else
		private const bool QADefined = false;
#endif
#if GEUNEDA_ENV_DEV
		private const bool DevDefined = true;
#else
		private const bool DevDefined = false;
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
		private const bool EditorOrDevelopmentBuild = true;
#else
		private const bool EditorOrDevelopmentBuild = false;
#endif

		public static AppEnvironment Current => Resolve(ProdDefined, QADefined, DevDefined, EditorOrDevelopmentBuild);

		public static bool IsDev => Current == AppEnvironment.Dev;

		public static bool IsQA => Current == AppEnvironment.QA;

		public static bool IsProd => Current == AppEnvironment.Prod;

		/// <summary>
		/// 소문자 이름(<c>dev</c>, <c>qa</c>, <c>prod</c>)입니다. 버전 문자열, 로그, 분석 이벤트, 서버 요청에 씁니다.
		/// </summary>
		public static string Name
		{
			get
			{
				switch (Current)
				{
					case AppEnvironment.Prod:
						return "prod";
					case AppEnvironment.QA:
						return "qa";
					default:
						return "dev";
				}
			}
		}

		/// <summary>
		/// define 과 빌드 종류로 환경을 정합니다. 둘 이상의 define 은 컴파일 오류라 여기서는 PROD, QA, DEV 순으로 봅니다.
		/// </summary>
		internal static AppEnvironment Resolve(bool prodDefined, bool qaDefined, bool devDefined, bool editorOrDevelopmentBuild)
		{
			if (prodDefined)
			{
				return AppEnvironment.Prod;
			}

			if (qaDefined)
			{
				return AppEnvironment.QA;
			}

			if (devDefined)
			{
				return AppEnvironment.Dev;
			}

			return editorOrDevelopmentBuild ? AppEnvironment.Dev : AppEnvironment.Prod;
		}
	}
}
