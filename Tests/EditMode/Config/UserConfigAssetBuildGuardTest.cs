using Geuneda.Core.Editor;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	[TestFixture]
	public class UserConfigAssetBuildGuardTest
	{
		[Test]
		public void FindsUserAssetsThatCanShip()
		{
			var paths = new[]
			{
				"Assets/Resources/Game.user.asset",
				"Assets/Configs/Resources/Sub/Game.user.asset",
				"Assets/Local/Game.user.asset",
				"Packages/com.example/Runtime/Resources/Ui.user.prefab",
				"Assets\\Win\\Resources\\Game.user.asset",
				"Assets/resources/Game.USER.asset",
				"Assets/RESOURCES/Game.User.asset",
				"Assets/Editor/Resources/Game.user.asset",
				"Assets/Tools/Editor/Configs/Resources/Game.user.asset",
				"Packages/com.example/Editor/Resources/Game.user.asset",
				"Assets/Resources/Game.asset",
				"Assets/Resources/Game.users.asset",
				"Assets/Resources/Game.user",
				"Assets/StreamingAssets/Editor/Resources/GameConfig.user.asset",
				"Assets/StreamingAssets/Editor/Game.USER.json",
				"Assets/StreamingAssets/Game.json",
				"Assets/QuantumUser/Simulation/Frame.User.cs",
				"Assets/Scripts/Game.user.CS",
				"Assets/StreamingAssets/Frame.User.cs"
			};

			var found = UserConfigAssetBuildGuard.FindUserAssetsThatCanShip(paths);

			CollectionAssert.AreEqual(new[]
			{
				"Assets/Resources/Game.user.asset",
				"Assets/Configs/Resources/Sub/Game.user.asset",
				"Assets/Local/Game.user.asset",
				"Packages/com.example/Runtime/Resources/Ui.user.prefab",
				"Assets/Win/Resources/Game.user.asset",
				"Assets/resources/Game.USER.asset",
				"Assets/RESOURCES/Game.User.asset",
				"Assets/StreamingAssets/Editor/Resources/GameConfig.user.asset",
				"Assets/StreamingAssets/Editor/Game.USER.json",
				"Assets/StreamingAssets/Frame.User.cs"
			}, found);
		}
	}
}
