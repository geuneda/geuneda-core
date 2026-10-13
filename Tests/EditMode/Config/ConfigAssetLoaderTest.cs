using System;
using System.Collections.Generic;
using Geuneda.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	[TestFixture]
	public class ConfigAssetLoaderTest
	{
		private sealed class RecordingSink : ILogSink
		{
			public readonly List<LogEntry> Entries = new List<LogEntry>();

			public void Write(in LogEntry entry)
			{
				Entries.Add(entry);
			}
		}

		private readonly List<Object> _created = new List<Object>();
		// 이 테스트가 만든 임시 폴더. 만든 경우에만 TearDown 에서 지운다
		private string _tempFolder;
		private Dictionary<string, Object> _resources;
		private List<string> _fallbackRequests;
		private ILogSink _originalSink;
		private RecordingSink _sink;

		[SetUp]
		public void SetUp()
		{
			_resources = new Dictionary<string, Object>();
			_fallbackRequests = new List<string>();
			_originalSink = Log.Sink;
			_sink = new RecordingSink();
			Log.Sink = _sink;
		}

		[TearDown]
		public void TearDown()
		{
			Log.Sink = _originalSink;

			foreach (var created in _created)
			{
				if (created != null && !AssetDatabase.Contains(created))
				{
					Object.DestroyImmediate(created);
				}
			}

			_created.Clear();

			if (_tempFolder != null)
			{
				AssetDatabase.DeleteAsset(_tempFolder);
				_tempFolder = null;
			}
		}

		private string CreateTempFolder()
		{
			var path = AssetDatabase.GenerateUniqueAssetPath("Assets/GeunedaCoreConfigTestTemp");
			var guid = AssetDatabase.CreateFolder("Assets", path.Substring("Assets/".Length));

			Assert.IsFalse(string.IsNullOrEmpty(guid), $"Could not create {path}");
			_tempFolder = AssetDatabase.GUIDToAssetPath(guid);
			return _tempFolder;
		}

		private static string CreateFolders(string parent, params string[] names)
		{
			foreach (var name in names)
			{
				AssetDatabase.CreateFolder(parent, name);
				parent = parent + "/" + name;
			}

			return parent;
		}

		private T Create<T>(string label) where T : ScriptableObject
		{
			var asset = ScriptableObject.CreateInstance<T>();
			asset.name = label;
			_created.Add(asset);
			return asset;
		}

		private ConfigAssetLoader CreateLoader(bool loadUserOverride, Dictionary<string, Object> fallback = null)
		{
			Func<string, Object> fallbackLoad = null;

			if (fallback != null)
			{
				fallbackLoad = path =>
				{
					_fallbackRequests.Add(path);
					return fallback.TryGetValue(path, out var found) ? found : null;
				};
			}

			return new ConfigAssetLoader(path => _resources.TryGetValue(path, out var found) ? found : null, fallbackLoad, loadUserOverride);
		}

		[Test]
		public void GetPath_UsesAttributeOrTypeName()
		{
			Assert.AreEqual("GeunedaCoreTest/PathConfig", ConfigAssetLoader.GetPath(typeof(PathConfigAsset)));
			Assert.AreEqual("TestConfigAsset", ConfigAssetLoader.GetPath(typeof(TestConfigAsset)));
			Assert.Throws<ArgumentNullException>(() => ConfigAssetLoader.GetPath(null));
		}

		[TestCase("\\Geuneda\\Game/", "Geuneda/Game")]
		[TestCase(" Configs/Game ", "Configs/Game")]
		public void PathAttribute_Normalizes(string path, string expected)
		{
			Assert.AreEqual(expected, new ConfigAssetPathAttribute(path).Path);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("  ")]
		[TestCase("/")]
		public void PathAttribute_Empty_Throws(string path)
		{
			Assert.Throws<ArgumentException>(() => new ConfigAssetPathAttribute(path));
		}

		[Test]
		public void Load_ReadsResourcesPath()
		{
			var asset = Create<PathConfigAsset>("base");
			_resources["GeunedaCoreTest/PathConfig"] = asset;

			Assert.AreSame(asset, CreateLoader(false).Load<PathConfigAsset>());
		}

		[Test]
		public void Load_InEditor_PrefersUserOverrideAndLogsIt()
		{
			var user = Create<PathConfigAsset>("user");
			_resources["GeunedaCoreTest/PathConfig"] = Create<PathConfigAsset>("base");
			_resources["GeunedaCoreTest/PathConfig.user"] = user;

			Assert.AreSame(user, CreateLoader(true).Load<PathConfigAsset>());
			Assert.AreEqual(1, _sink.Entries.Count);
			Assert.AreEqual(LogLevel.Info, _sink.Entries[0].Level);
			StringAssert.Contains("GeunedaCoreTest/PathConfig.user", _sink.Entries[0].Message);
		}

		[Test]
		public void Load_InPlayer_IgnoresUserOverride()
		{
			var asset = Create<PathConfigAsset>("base");
			_resources["GeunedaCoreTest/PathConfig"] = asset;
			_resources["GeunedaCoreTest/PathConfig.user"] = Create<PathConfigAsset>("user");

			Assert.AreSame(asset, CreateLoader(false).Load<PathConfigAsset>());
		}

		[Test]
		public void Load_NotInResources_UsesFallbackWithPath()
		{
			var asset = Create<PathConfigAsset>("fallback");
			var fallback = new Dictionary<string, Object> { ["GeunedaCoreTest/PathConfig"] = asset };

			Assert.AreSame(asset, CreateLoader(false, fallback).Load<PathConfigAsset>());
			CollectionAssert.AreEqual(new[] { "GeunedaCoreTest/PathConfig" }, _fallbackRequests);
		}

		[Test]
		public void Load_InResources_DoesNotCallFallback()
		{
			_resources["GeunedaCoreTest/PathConfig"] = Create<PathConfigAsset>("base");

			CreateLoader(false, new Dictionary<string, Object>()).Load<PathConfigAsset>();

			CollectionAssert.IsEmpty(_fallbackRequests);
		}

		[Test]
		public void Load_Missing_ThrowsWithPathAndTryLoadReturnsFalse()
		{
			var loader = CreateLoader(true, new Dictionary<string, Object>());

			var exception = Assert.Throws<InvalidOperationException>(() => loader.Load<PathConfigAsset>());
			StringAssert.Contains("GeunedaCoreTest/PathConfig", exception.Message);
			Assert.IsFalse(loader.TryLoad<PathConfigAsset>(out var asset));
			Assert.IsNull(asset);
		}

		[Test]
		public void Load_WrongTypeAtPath_Throws()
		{
			_resources["GeunedaCoreTest/PathConfig"] = Create<TestConfigAsset>("wrong");

			var exception = Assert.Throws<InvalidOperationException>(() => CreateLoader(false).Load<PathConfigAsset>());
			StringAssert.Contains("TestConfigAsset", exception.Message);
		}

		[Test]
		public void PublicLoader_ReadsResourcesAndUserOverrideFromEditorResources()
		{
			var root = CreateTempFolder();
			var resourcesFolder = CreateFolders(root, "Resources", "GeunedaCoreTest");
			var editorResourcesFolder = CreateFolders(root, "Editor", "Resources", "GeunedaCoreTest");

			var baseAsset = Create<PathConfigAsset>("PathConfig");
			baseAsset.Label = "base";
			AssetDatabase.CreateAsset(baseAsset, resourcesFolder + "/PathConfig.asset");

			Assert.AreEqual("base", new ConfigAssetLoader().Load<PathConfigAsset>().Label);

			// 개인 덮어쓰기는 빌드에서 빠지는 Editor/Resources 에 둔다. 에디터에서는 Resources.Load 로 읽힌다
			var userAsset = Create<PathConfigAsset>("PathConfig.user");
			userAsset.Label = "user";
			AssetDatabase.CreateAsset(userAsset, editorResourcesFolder + "/PathConfig.user.asset");

			Assert.AreEqual("user", new ConfigAssetLoader().Load<PathConfigAsset>().Label);
		}
	}
}
