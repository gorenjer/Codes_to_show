namespace Game.Model
{
	public enum GameSubtype
	{
		Common,
		Today,
		Tutorial
	}

	public static class GameSubtypeHelper
	{
		public static string ToAnalyticsString(this GameSubtype subtype)
		{
			switch (subtype)
			{
				case GameSubtype.Common:
				{
					return "common_game";
				}
				break;

				case GameSubtype.Today:
				{
					return "today_training";
				}
				break;

				default:
				{
					return string.Empty;
				}
			}
		}
	}
}
