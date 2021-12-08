using Game.Diagnostic;

namespace Game.Model
{
	public enum GameType
	{
		Undefined,
		Classic,
		Busted
	}

	public static class GameTypeHelper
	{
		public static GameType FromString(string value)
		{
			switch (value)
			{
				case "Classic_Game":
				{
					return GameType.Classic;
				}

				case "Busted_Game":
				{
					return GameType.Busted;
				}

				default:
				{
					GameDebug.Log(nameof(GameTypeHelper), $"Couldn't convert string ['{value}']");
					return GameType.Undefined;
				}
			}
		}

		public static string ToString(GameType type)
		{
			switch (type)
			{				
				case GameType.Classic:
				{
					return "Classic_Game";
				}

				case GameType.Busted:
				{
					return "Busted_Game";
				}

				default:
				{
					GameDebug.Log(nameof(GameTypeHelper), $"Couldn't convert string ['{type}']");
					return string.Empty;
				}
			}
		}
	}
}