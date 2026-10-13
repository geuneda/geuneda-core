using Geuneda.Core;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	public class TestConfigAsset : ScriptableObject
	{
		public string Label;
		public EnvValue<string> ServerUrl;
	}
}
