using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	/// <summary>
	/// 활성 빌드 타깃의 플레이어 스크립트를 define 조합별로 컴파일해 <c>AppEnv.cs</c> 의 컴파일 규칙을 확인합니다.
	/// </summary>
	/// <remarks>
	/// 프로젝트 전체를 여러 번 컴파일해 느리므로 기본 실행에서 빠집니다. 환경 define 규칙을 바꿀 때
	/// <c>--filter Geuneda.Core.Tests.AppEnvPlayerCompileTest</c> 로 직접 실행합니다. 프로젝트 설정에 환경 define 이 없어야 합니다.
	/// </remarks>
	[TestFixture, Explicit("플레이어 스크립트를 여러 번 컴파일해 느립니다")]
	public class AppEnvPlayerCompileTest
	{
		private const string OutputFolder = "Temp/GeunedaAppEnvCompileTest";

		private static bool CompilePlayerScripts(ScriptCompilationOptions options, string[] defines, List<string> errors)
		{
			var target = EditorUserBuildSettings.activeBuildTarget;
			var settings = new ScriptCompilationSettings
			{
				target = target,
				group = BuildPipeline.GetBuildTargetGroup(target),
				options = options,
				extraScriptingDefines = defines
			};

			void OnLog(string condition, string stackTrace, LogType type)
			{
				if (type == LogType.Error)
				{
					errors.Add(condition);
				}
			}

			Application.logMessageReceived += OnLog;
			try
			{
				var result = PlayerBuildInterface.CompilePlayerScripts(settings, OutputFolder);
				return result.assemblies != null && result.assemblies.Contains("Geuneda.Core.dll");
			}
			finally
			{
				Application.logMessageReceived -= OnLog;
			}
		}

#if !GEUNEDA_ENV_DEV && !GEUNEDA_ENV_QA && !GEUNEDA_ENV_PROD
		[Test]
		public void ReleaseWithoutEnvDefine_Compiles()
		{
			// Addressables 콘텐츠 빌드가 쓰는 조합(개발 빌드 옵션도 추가 define 도 없음)
			var errors = new List<string>();

			Assert.IsTrue(CompilePlayerScripts(ScriptCompilationOptions.None, new string[0], errors), string.Join("\n", errors));
		}

		[TestCase("GEUNEDA_ENV_PROD")]
		[TestCase("GEUNEDA_ENV_QA")]
		[TestCase("GEUNEDA_ENV_DEV")]
		public void ReleaseWithOneEnvDefine_Compiles(string define)
		{
			var errors = new List<string>();

			Assert.IsTrue(CompilePlayerScripts(ScriptCompilationOptions.None, new[] { define }, errors), string.Join("\n", errors));
		}

		[Test]
		public void DevelopmentWithoutEnvDefine_Compiles()
		{
			var errors = new List<string>();

			Assert.IsTrue(CompilePlayerScripts(ScriptCompilationOptions.DevelopmentBuild, new string[0], errors), string.Join("\n", errors));
		}

		[Test]
		public void TwoEnvDefines_FailWithDefineOnlyOneError()
		{
			LogAssert.ignoreFailingMessages = true;
			var errors = new List<string>();

			var compiled = CompilePlayerScripts(ScriptCompilationOptions.None, new[] { "GEUNEDA_ENV_PROD", "GEUNEDA_ENV_QA" }, errors);

			Assert.IsFalse(compiled);
			Assert.IsTrue(errors.Any(error => error.Contains("Define only one of GEUNEDA_ENV_DEV, GEUNEDA_ENV_QA, GEUNEDA_ENV_PROD")),
				string.Join("\n", errors));
		}
#endif
	}
}
