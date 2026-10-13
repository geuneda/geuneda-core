using Geuneda.Core;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	/// <summary>
	/// define 과 빌드 종류에 따른 환경 결정(<see cref="AppEnv.Resolve"/>)과 에디터 컴파일 값을 확인합니다.
	/// define 별 컴파일 결과는 <see cref="AppEnvPlayerCompileTest"/> 로 확인합니다.
	/// </summary>
	[TestFixture]
	public class AppEnvTest
	{
		[TestCase(true, false, false, false, AppEnvironment.Prod)]
		[TestCase(true, false, false, true, AppEnvironment.Prod)]
		[TestCase(false, true, false, false, AppEnvironment.QA)]
		[TestCase(false, true, false, true, AppEnvironment.QA)]
		[TestCase(false, false, true, false, AppEnvironment.Dev)]
		[TestCase(false, false, true, true, AppEnvironment.Dev)]
		[TestCase(false, false, false, true, AppEnvironment.Dev)]
		[TestCase(false, false, false, false, AppEnvironment.Prod)]
		public void Resolve_MapsDefinesAndBuildKind(bool prod, bool qa, bool dev, bool editorOrDevelopmentBuild, AppEnvironment expected)
		{
			Assert.AreEqual(expected, AppEnv.Resolve(prod, qa, dev, editorOrDevelopmentBuild));
		}

		[Test]
		public void StripCondition_MatchesNonProd()
		{
			// README 의 운영 제외 조건 #if !GEUNEDA_ENV_PROD && (UNITY_EDITOR || DEVELOPMENT_BUILD || GEUNEDA_ENV_DEV || GEUNEDA_ENV_QA)
			// 이 Resolve 가 Prod 가 아닌 경우와 같은지, 허용되는 define 조합(많아야 하나)과 빌드 종류 전부에서 확인한다
			var defineSets = new[]
			{
				(prod: false, qa: false, dev: false),
				(prod: true, qa: false, dev: false),
				(prod: false, qa: true, dev: false),
				(prod: false, qa: false, dev: true)
			};

			foreach (var (prod, qa, dev) in defineSets)
			{
				foreach (var editorOrDevelopmentBuild in new[] { false, true })
				{
					var included = !prod && (editorOrDevelopmentBuild || dev || qa);
					var nonProd = AppEnv.Resolve(prod, qa, dev, editorOrDevelopmentBuild) != AppEnvironment.Prod;

					Assert.AreEqual(nonProd, included, $"prod={prod} qa={qa} dev={dev} editorOrDevelopmentBuild={editorOrDevelopmentBuild}");
				}
			}
		}

		[Test]
		public void FlagsAndName_MatchCurrent()
		{
			var current = AppEnv.Current;

			Assert.AreEqual(current == AppEnvironment.Dev, AppEnv.IsDev);
			Assert.AreEqual(current == AppEnvironment.QA, AppEnv.IsQA);
			Assert.AreEqual(current == AppEnvironment.Prod, AppEnv.IsProd);
			Assert.AreEqual(current.ToString().ToLowerInvariant(), AppEnv.Name);
		}

#if !GEUNEDA_ENV_DEV && !GEUNEDA_ENV_QA && !GEUNEDA_ENV_PROD
		[Test]
		public void EditorWithoutEnvDefine_IsDev()
		{
			Assert.AreEqual(AppEnvironment.Dev, AppEnv.Current);
			Assert.AreEqual("dev", AppEnv.Name);
		}
#endif
	}
}
