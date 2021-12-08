using Game.Model;
using Game.Preference;

namespace Game.Configuration.Model
{
	public interface IGameConfig
	{
		string LevelId { get; set; }
		GameType Type { get; }
		GameSubtype Subtype { get; }
		GameDifficulty Difficulty { get; }

		ScoreConfig ScoreConfig { get; }
		TimeConfig TimeConfig { get; }

		int FreeLiveCount { get; }
		int FreeHintCount { get; }
		bool IsNoMistakeMode { get; set; }
		string Serialize();
	}

	public class GameConfig : IGameConfig
	{
		public string LevelId { get; set; }
		public GameType Type { get; private set; }
		public GameSubtype Subtype { get; private set; }
		public GameDifficulty Difficulty { get; private set; }

		public ScoreConfig ScoreConfig { get; private set; }
		public TimeConfig TimeConfig { get; private set; }

		public bool IsNoMistakeMode { get; set; }
		public int FreeLiveCount { get; private set; }
		public int FreeHintCount { get; private set; }
		
		public GameConfig(GameType type, GameSubtype subtype, GameDifficulty difficulty, ScoreConfig scoreConfig, TimeConfig timeConfig, int freeHintCount, int freeLiveCount)
		{
			Type = type;
			Subtype = subtype;
			Difficulty = difficulty;
			ScoreConfig = scoreConfig;
			TimeConfig = timeConfig;
			FreeLiveCount = freeLiveCount;
			FreeHintCount = freeHintCount;
		}

		public GameConfig(GameType type, GameSubtype subtype, GameDifficulty difficulty, int size, ScoreConfig scoreConfig, TimeConfig timeConfig, int freeLiveCount, int freeHintCount)
		{
			Type = type;
			Subtype = subtype;
			Difficulty = difficulty;
			ScoreConfig = scoreConfig;
			TimeConfig = timeConfig;
			FreeLiveCount = freeLiveCount;
			FreeHintCount = freeHintCount;
		}

		public GameConfig(string data)
		{
			var obj = new Record(data);

			Type = (GameType)(int)obj.GetField("type").n;
			Subtype = (GameSubtype)(int)obj.GetField("subtype").n;
			Difficulty = (GameDifficulty)(int)obj.GetField("difficulty").n;

			var scoreConfigObj = obj.GetField("scoreConfig");
			ScoreConfig = new ScoreConfig(scoreConfigObj.ToString());

			var timeConfigObj = obj.GetField("timeConfig");
			TimeConfig = new TimeConfig(timeConfigObj.ToString());

			FreeLiveCount = (int)obj.GetField("health").n;
			FreeHintCount = (int)obj.GetField("hintCount").n;

			LevelId = obj.GetField("levelId").str;
		}

		public string Serialize()
		{
			var obj = new Record();

			obj.AddField("type", (int)Type);
			obj.AddField("subtype", (int)Subtype);
			obj.AddField("difficulty", (int)Difficulty);

			var serializedScoreConfig = ScoreConfig.Serialize();
			obj.AddField("scoreConfig", new Record(serializedScoreConfig));

			var serializedTimeConfig = TimeConfig.Serialize();
			obj.AddField("timeConfig", new Record(serializedTimeConfig));

			obj.AddField("health", FreeLiveCount);
			obj.AddField("hintCount", FreeHintCount);

			obj.AddField("levelId", LevelId);

			return obj.ToString();
		}
	}
}