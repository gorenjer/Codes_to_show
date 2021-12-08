using UnityEngine;

using System.Linq;
using System.Collections.Generic;

using Game.User;
using Game.Model;
using Game.Network;
using Game.Diagnostic;
using Game.Preference;
using Game.Network.Request;
using Game.Stats;
using Game.Network.Response;
using Game.Achievements;
using Game.Global;

namespace Game
{
	public interface IGameManager
	{
		bool IsPlaying();
		bool IsSaved(GameType type, GameSubtype subtype);
		float GetProgress(GameType type, GameSubtype subtype);

		GameCost GetNotSyncedCost();
		GameViewConfig GetViewConfig();

		IGame GetMatch();
		Level GetRandomLevel(GameType type, GameDifficulty diffuculty);

		void Begin(GameType type, GameSubtype subtype, GameDifficulty difficulty, string levelId);
		void Begin(IGameConfig config);

		void Continue(GameType type, GameSubtype subtype);
		void Discard(GameType type, GameSubtype subtype);

		Report SendReport();

		void Leave();
		void End();

#if REF_DEBUG_CONSOLE
		void CheatWin();
		void CheatLive();
		void CheatHints();
#endif
	}

	public class GameManager : Manager, IGameManager
	{
		private LevelDescription[] bustedLevels;
		private LevelDescription[] classicLevels;
		private LevelDescription tutorialLevel;

		private int defaultFreeLives;
		private int defaultFreeHints;
		private ScoreConfig defaultScoreConfig;
		private DifficultyTimeConfig[] defaultTimeConfigs;

		private GameViewConfig viewConfig;

		private INetworkManager networkManager;
		private ISaveManager saveManager;
		private IProfileManager profileManager;
		private IStatisticsManager statisticsManager;
		private IAchievementManager achievementManager;

		private Game current;
		private readonly List<Game> saved = new List<Game>();
		private readonly List<GameResult> failedReportQueue = new List<GameResult>(); // reports that failed to sent

		public bool IsPlaying()
		{
			return current != null;
		}

		public bool IsSaved(GameType type, GameSubtype subtype)
		{
			if (subtype != GameSubtype.Today)
			{
				var similarGame = FindSavedGame(type, subtype);
				return similarGame != null;
			}
			else
			{
				var similarGame = FindSavedGame(subtype);

				if (similarGame != null)
				{
					var config = similarGame.Config;
					return similarGame.StartTimestamp.Date == Time.LocalDate && (config.Type == type || type == GameType.Undefined);
				}

				return false;
			}
		}

		public float GetProgress(GameType type, GameSubtype subtype)
		{
			var similarGame = FindSavedGame(type, subtype);
			
			if (similarGame == null)
			{
				return 0f;
			}

			return similarGame.Progress;
		}

		public IGame GetMatch()
		{
			return current;
		}

		public Level GetRandomLevel(GameType type, GameDifficulty difficulty)
		{
			return GetLevel(type, difficulty);
		}

		public void Begin(GameType type, GameSubtype subtype, GameDifficulty difficulty, string levelId)
		{
			var defaultTimeConfig = FindDefaultTimeConfig(difficulty);

			if (defaultTimeConfig == null)
			{
				GameDebug.Error(nameof(GameManager), $"Couldn't start game, because default time config for difficulty: {difficulty} doesn't exists!");
				return;
			}

			var config = new GameConfig(type, subtype, difficulty, defaultScoreConfig, defaultTimeConfig, defaultFreeLives, defaultFreeHints);
			config.LevelId = levelId;

			Begin(config);
		}

		public void Begin(IGameConfig config)
		{
			if (IsPlaying())
			{
				GameDebug.Error(nameof(GameManager), "Couldn't start game, because is already started!");
				return;
			}

			DiscardOutDatedDailies();
			Discard(config.Type, config.Subtype, config.Subtype != GameSubtype.Today);

			var profile = profileManager.Profile;
			var inventory = profile.Inventory;
			var settings = profile.Settings;
			var gameplay = settings.GameplaySettings;

			config.IsNoMistakeMode = gameplay.UnlimitedLives;

			if (string.IsNullOrEmpty(config.LevelId))
			{
				var level = GetLevel(config.Type, config.Difficulty);
				config.LevelId = level.Id;

				current = new Game(inventory, config, level);
			}
			else
			{
				if (config.Subtype == GameSubtype.Tutorial)
				{
					var level = GetTutorialLevel();
					current = new Game(inventory, config, level);
				}
				else
				{
					var level = GetLevel(config.Type, config.LevelId);
					current = new Game(inventory, config, level);
				}
			}

			var player = current.Player;
			
			var nonSyncedCost = CalculateNonSyncedCost();
			current.SetNonSyncCost(nonSyncedCost);
			current.Pause();

			if (config.Subtype != GameSubtype.Tutorial)
			{
				Analytics.GameStart(config.Type, config.Difficulty, config.Subtype, new System.DateTimeOffset(current.StartTimestamp).ToUnixTimeSeconds(), config.LevelId);
			}
		}

		public void Continue(GameType type, GameSubtype subtype)
		{
			if (IsPlaying())
			{
				GameDebug.Error(nameof(GameManager), "Couldn't resume game, because is already started!");
				return;
			}

			Game savedGame;

			if (subtype != GameSubtype.Today)
			{
				savedGame = FindSavedGame(type, subtype);
			}
			else
			{
				savedGame = FindSavedGame(subtype);
			}

			if (savedGame == null)
			{
				GameDebug.Error(nameof(GameManager), "Couldn't resume game, because it doesn't exists!");
				return;
			}

			RemoveSave(type, subtype, false);
			var nonSyncedCost = CalculateNonSyncedCost();

			current = savedGame;
			current.SetNonSyncCost(nonSyncedCost);
			current.Resume();

			var config = current.Config;
			if (config.Subtype != GameSubtype.Tutorial)
			{
				Analytics.GameStart(config.Type, config.Difficulty, config.Subtype, new System.DateTimeOffset(current.StartTimestamp).ToUnixTimeSeconds(), config.LevelId);
			}
		}

		public void Discard(GameType type, GameSubtype subtype)
		{
			Discard(type, subtype, subtype != GameSubtype.Today);
		}

		public Report SendReport()
		{
			if (!IsPlaying())
			{
				GameDebug.Error(nameof(GameManager), "There is no active game to send report!");
				return null;
			}

			var matchResult = new GameResult(current);
			var reportStatus = new ReportStatusImpl(matchResult);

			var config = current.Config;

			if (config.Subtype != GameSubtype.Tutorial)
			{
				SendGameReport(new GameResult(current), reportStatus);
			}
			{
				var profile = profileManager.Profile;
				var inventoryDetail = profile.Inventory;

				reportStatus.OnReportSent(new string[0], new Inventory(inventoryDetail.ExtraHintCount, inventoryDetail.ExtraLiveCount, inventoryDetail.ExtraTimeSeconds, inventoryDetail.IsNoAds(), inventoryDetail.IsUnlimitedTime()));
			}

			return reportStatus;
		}

		public void Leave()
		{
			if (!IsPlaying())
			{
				GameDebug.Log(nameof(GameManager), "There is no active game to leave!");
				return;
			}

			current.Pause();
			SaveGame(current);
			current = null;
		}

		public void End()
		{
			if (!IsPlaying())
			{
				GameDebug.Log(nameof(GameManager), "There is no active game to end!");
				return;
			}

			var config = current.Config;
			Discard(config.Type, config.Subtype, false);

			current = null;
		}

		public GameCost GetNotSyncedCost()
		{
			return CalculateNonSyncedCost();
		}

		public GameViewConfig GetViewConfig()
		{
			return viewConfig;
		}

		public void Init(MatchManagerConfig config, INetworkManager networkManager, IProfileManager profileManager, IAchievementManager achievementManager, ISaveManager saveManager, IStatisticsManager statisticsManager)
		{
			classicLevels = config.classicSudokuLevelDescriptions;
			bustedLevels = config.colorSudokuLevelDescriptions;
			tutorialLevel = config.colorSudokuTutorialLevel;

			defaultFreeHints = config.defaultFreeHints;
			defaultFreeLives = config.defaultFreeLives;
			defaultTimeConfigs = config.defaultTimeConfigs;
			defaultScoreConfig = config.defaultScoreConfigInstaller.ToConfig();

			this.viewConfig = config.viewConfig;
			this.networkManager = networkManager;
			this.profileManager = profileManager;
			this.statisticsManager = statisticsManager;
			this.achievementManager = achievementManager;
			this.saveManager = saveManager;

			Load();

			SetInitialized(true);
		}

		public void Suspend()
		{
			if (IsPlaying())
			{
				if (!current.IsEnd())
				{
					var config = current.Config;

					if (config.Subtype != GameSubtype.Tutorial)
					{
						current.Pause();
						SaveGame(current);
					}
				}
			}

			Save();
		}

		public void Resume()
		{
			if (IsPlaying())
			{
				RemoveSave(current, false);
			}
		}

		public void Update(float deltaTime)
		{
			if (IsPlaying())
			{
				current.Update(deltaTime);
			}
		}

		public void Release()
		{
			bustedLevels = null;
			classicLevels = null;

			defaultTimeConfigs = null;
			defaultScoreConfig = null;

			viewConfig = null;

			profileManager = null;
			saveManager = null;
			networkManager = null;
			statisticsManager = null;

			current = null;
			
			saved.Clear();

			SetInitialized(false);
		}

		private void DiscardOutDatedDailies()
		{
			var similarSavedGames = saved.FindAll((match) => { return match.Config.Subtype == GameSubtype.Today && Time.LocalDate != match.StartTimestamp.Date; });
			
			foreach (var game in similarSavedGames)
			{
				var result = new GameResult(game);
				SendGameReport(result, new ReportStatusImpl(result));
				saved.Remove(game);
			}
		}

		private void Discard(GameType type, GameSubtype subtype, bool sendReport)
		{
			if (IsSaved(type, subtype))
			{
				var game = FindSavedGame(type, subtype);
				RemoveSave(game, sendReport);
			}
		}

		private void OnGameFinalize(GameResult result)
		{
			statisticsManager.AddResult(result);
			achievementManager.AddResult(result);
		}

		private void SendGameReport(GameResult report, ReportStatusImpl reportStatus)
		{
			void OnGameReportSentResponseHandler(IResponse r)
			{
				var gameResponse = (GameResponse)r;

				string[] achievements = new string[0];
				Inventory inventory = null;

				if (gameResponse.IsCompleted)
				{
					failedReportQueue.Clear();

					var response = (SendGameReportResponse)gameResponse;

					OnGameFinalize(report);

					inventory = response.Inventory;
					achievements = response.NewAchievements;

					profileManager.Sync(inventory);
				}
				else
				{
					bool clear = true;
					var errors = gameResponse.Errors;
					
					foreach (Error error in errors)
					{
						if (error.Code >= 500 || error.Code == ErrorCodes.NoInternet || error.Code == ErrorCodes.InvalidToken || error.Code == ErrorCodes.NotInitialized)
						{
							clear = false;
						}
					}

					if (clear)
					{
						failedReportQueue.Clear();
					}
				}
				
				if (IsPlaying())
				{
					var cost = CalculateNonSyncedCost();
					current.SetNonSyncCost(cost);
				}

				reportStatus.OnReportSent(achievements, inventory);
			}

			failedReportQueue.Add(report);
			networkManager.SendWithCallback(new SendGameReportRequest(failedReportQueue.ToArray()), OnGameReportSentResponseHandler);
		}

		private GameCost CalculateNonSyncedCost()
		{
			var result = new GameCost(0, 0, 0);

			foreach (Game game in saved)
			{
				var player = game.Player;
				var cost = player.CalculateCost(game.ElapsedTimeSeconds);

				result += cost;
			}

			foreach (GameResult report in failedReportQueue)
			{
				var config = report.Config;

				if (!IsSaved(config.Type, config.Subtype))
				{
					result += report.Cost;
				}
			}

			return result;
		}

		private Level GetLevel(GameType type, string id)
		{
			if (type == GameType.Busted)
			{
				var description = bustedLevels.Where((desc) => { return desc.asset.name == id; }).FirstOrDefault();
				return new Level(description.asset.name, description.asset.text);
			}
			
			if (type == GameType.Classic)
			{
				var description = classicLevels.Where((desc) => { return desc.asset.name == id; }).FirstOrDefault();
				return new Level(description.asset.name, description.asset.text);
			}

			return null;
		}

		private Level GetTutorialLevel()
		{
			return new Level(tutorialLevel.asset.name, tutorialLevel.asset.text);
		}

		private Level GetLevel(GameType type, GameDifficulty difficulty)
		{
			if (type == GameType.Classic)
			{
				var descriptions = classicLevels.Where((description) => 
				{
					return description.difficulty == difficulty; 
				});

				var count = descriptions.Count();
				var index = Random.Range(0, count);
				var descriptionElement = descriptions.ElementAt(index);
				return new Level(descriptionElement.asset.name, descriptionElement.asset.text);
			}

			if (type == GameType.Busted)
			{
				var descriptions = bustedLevels.Where((description) =>
				{
					return description.difficulty == difficulty;
				});

				var count = descriptions.Count();
				var index = Random.Range(0, count);
				var descriptionElement = descriptions.ElementAt(index);
				return new Level(descriptionElement.asset.name, descriptionElement.asset.text);
			}

			return null;
		}

		private void SaveGame(Game game)
		{
			var config = game.Config;
			RemoveSave(config.Type, config.Subtype, false);
			saved.Add(game);
		}

		private void RemoveSave(Game game, bool sendReport)
		{
			var config = game.Config;
			RemoveSave(config.Type, config.Subtype, sendReport);
		}

		private void RemoveSave(GameType type, GameSubtype subtype, bool sendReport)
		{
			var similarSavedGames = saved.FindAll((game) => { return game.Config.Type == type && game.Config.Subtype == subtype; });
			
			foreach (var game in similarSavedGames)
			{
				if (sendReport)
				{
					var result = new GameResult(game);
					SendGameReport(result, new ReportStatusImpl(result));
				}

				saved.Remove(game);
			}
		}

		private Game FindSavedGame(GameType type, GameSubtype subtype)
		{
			return saved.Find((game) => { return game.Config.Type == type && game.Config.Subtype == subtype; });
		}

		private Game FindSavedGame(GameSubtype subtype)
		{
			return saved.Find((game) => { return game.Config.Subtype == subtype; });
		}

		private TimeConfig FindDefaultTimeConfig(GameDifficulty difficulty)
		{
			if (defaultTimeConfigs == null)
			{
				return null;
			}

			return (from difficultyTimeConfig in defaultTimeConfigs
				where difficultyTimeConfig.difficulty == difficulty
				select difficultyTimeConfig.timeConfigInstaller.ToConfig()).FirstOrDefault();
		}

		private void Save()
		{
			if (saved.Count > 0)
			{
				saveManager.Set("game_manager.countOfGames", saved.Count.ToString());
				for (int idx = 0; idx < saved.Count; ++idx)
				{
					var data = saved[idx].Serialize();
					saveManager.Set($"game_manager.game_{idx}", data);
				}
			}

			if (failedReportQueue.Count > 0)
			{
				saveManager.Set("game_manager.countOfReports", failedReportQueue.Count.ToString());
			}
		}

		private void Load()
		{
			if (saveManager.Has("game_manager.countOfGames"))
			{
				var profile = profileManager.Profile;
				var inventory = profile.Inventory;

				var count = int.Parse(saveManager.Get("game_manager.countOfGames"));

				for (int idx = 0; idx < count; ++idx)
				{
					var data = saveManager.Get($"game_manager.game_{idx}");
					var obj = new Record(data);
					var configObj = obj.GetField("config");
					var config = new GameConfig(configObj.ToString());
					var level = GetLevel(config.Type, config.LevelId);

					var game = new Game(inventory, data, level);
					saved.Add(game);
				}
			}
		}

#if REF_DEBUG_CONSOLE
		public void CheatWin()
		{
			if (current == null)
			{
				return;
			}

			var card = current.Card;

			for (int x = 0; x < card.Size; ++x)
			{
				for (int y = 0; y < card.Size; ++y)
				{
					current.ExtraHint();
					current.Hint(new Vector2Int(x, y));
				}
			}
		}

		public void CheatLive()
		{
			current?.ExtraLive();
		}
		
		public void CheatHints()
		{
			current?.ExtraHint();
		}
#endif
	}
}
