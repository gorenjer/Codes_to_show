using UnityEngine.UI.Windows;
using Game.Global;
using Game.Diagnostic;
using Game.Window.Parameters;

using Game.UI.PopUp;
using Game.UI.Game;
using Game.UI.Menu;
using Game.UI.Menu.MainMenu;
using Game.UI.Menu.Reward;
using Game.UI.PopUp.Upsell;
using Game.UI.PopUp.GameType;
using Game.UI.PopUp.Settings;
using Game.UI.PopUp.AgeSelect;
using Game.UI.PopUp.Achievement;
using Game.UI.PopUp.Store;
using Game.UI.PopUp.Configuration;
using Game.UI.PopUp.FailedPurchase;
using Game.UI.PopUp.GameOver;
using Game.UI.PopUp.Resume;
using Game.UI.PopUp.FirstGame;

namespace Game.Window
{
	public interface IWindowsManager : IManager
	{
		void ShowGameOverScreen(GameOverScreenParams parameters);
		void ShowAchievementScreen(AchievementScreenParams parameters);
		void ShowAgeSelectScreen(AgeSelectScreenParams parameters);
		void ShowConfigurationScreen(ConfigurationScreenParams parameters);
		void ShowRewardScreen(RewardScreenParams parameters);
		void ShowGameScreen(GameScreenParams parameters);
		void ShowGameTypeScreen(GameTypeScreenParams parameters);
		void ShowMainMenuScreen(MainMenuScreenParams parameters);
		void ShowSettingScreen(SettingsScreenParams parameters);
		void ShowStoreScreen(StoreScreenParams parameters);
		void ShowUpsellScreen(UpsellScreenParams parameters);
		void ShowResumeScreen(ResumeScreenParams parameters);
		void ShowFailedPurchaseScreen(FailedPurchaseScreenParams parameters);
		void ShowFirstGameScreen(FirstGameScreenParams parameters);
		
		void HideCurrent();
	}

	public class WindowsManager : Manager, IWindowsManager
	{
		private IApp app;
		private WindowSystemWrapper windowSystemManager;

		private readonly History history = new History();
		private System.Type currentType;
		private WindowBase current;
		private System.Action initCallback;

		public void Init(IApp app, System.Action callback)
		{
			this.app = app;
			this.initCallback = callback;
			windowSystemManager = WindowSystem.instance as WindowSystemWrapper;

			if (windowSystemManager.IsInitialized())
			{
				initCallback?.Invoke();
			}
			else
			{
				windowSystemManager.OnInitialized += OnWindowSystemInitializedHandler;
			}
		}

		public void Release()
		{
			app = null;
			windowSystemManager = null;
			currentType = null;
			current = null;
			history.Clear();
			initCallback = null;

			SetInitialized(false);
		}

		private void OnWindowSystemInitializedHandler()
		{
			SetInitialized(true);
			initCallback?.Invoke();
		}

		public void ShowGameOverScreen(GameOverScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<GameOverScreen>(parameters);
		}

		public void ShowAchievementScreen(AchievementScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<AchievementScreen>(parameters);
		}

		public void ShowAgeSelectScreen(AgeSelectScreenParams parameters)
		{
			Analytics.AgeScreen();
			SetUpBaseParams(parameters);
			ShowInternal<AgeSelectScreen>(parameters);
		}

		public void ShowConfigurationScreen(ConfigurationScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<ConfigurationScreen>(parameters);
			Analytics.ShownConfigurationScreen();
		}

		public void ShowRewardScreen(RewardScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<RewardScreen>(parameters);
		}

		public void ShowGameScreen(GameScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<GameScreen>(parameters);
		}

		public void ShowGameTypeScreen(GameTypeScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<GameTypeScreen>(parameters);
		}

		public void ShowMainMenuScreen(MainMenuScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<MainMenuScreen>(parameters);
		}

		public void ShowSettingScreen(SettingsScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<SettingsScreen>(parameters);
		}

		public void ShowStoreScreen(StoreScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<StoreScreen>(parameters);
			Analytics.ShopScreen();
		}

		public void ShowUpsellScreen(UpsellScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<UpsellScreen>(parameters);
		}

		public void ShowResumeScreen(ResumeScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<ResumeScreen>(parameters);
		}

		public void ShowFailedPurchaseScreen(FailedPurchaseScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<FailedPurchaseScreen>(parameters);
		}

		public void ShowFirstGameScreen(FirstGameScreenParams parameters)
		{
			SetUpBaseParams(parameters);
			ShowInternal<FirstGameScreen>(parameters);
		}

		public void HideCurrent()
		{
			var previous = history.GetPrevious();

			if (previous == null)
			{
				return;
			}

			if (IsPopUp(current.GetType()))
			{
				var cachedCurrent = current;

				currentType = previous.Window.GetType();
				current = previous.Window;

				if (IsPopUp(current.GetType()))
				{
					history.RemoveLast();
				}

				GameDebug.Log(nameof(WindowManager), $"redirect to {currentType.Name}");
				GameDebug.SetScreen(currentType.Name);
				
				cachedCurrent.Hide();
			}
			else
			{
				GameDebug.Error(nameof(WindowManager), $"Hide current is not implemented for not pop-up screens!");
			}
		}

		private void ShowInternal<T>(BaseWindowParams parameters) where T : WindowBase
		{
			OnBeforeTransition<T>(parameters);

			var instantiatedWindows = WindowSystem.GetCurrentList();
			currentType = typeof(T);

			GameDebug.Log(nameof(WindowManager), $"redirect to {currentType.Name}");
			GameDebug.SetScreen(currentType.Name);
			Analytics.ShowScreen(currentType.Name, currentType.FullName);

			WindowSystem.Show<T>(afterGetInstance: (createdWindow) =>
			{
				current = createdWindow;
				OnAfterTransition<T>(current);
			}, null, parameters: parameters);
		}

		private bool IsPopUp(System.Type windowType)
		{
			return windowType.IsSubclassOf(typeof(PopUpContainer));
		}

		private bool IsMenu(System.Type windowType)
		{
			return windowType.IsSubclassOf(typeof(MenuContainer));
		}

		private bool IsGame(System.Type windowType)
		{
			return windowType.IsSubclassOf(typeof(GameContainer));
		}

		private void OnAfterTransition<T>(WindowBase to) where T : WindowBase
		{
			
		}

		private void OnBeforeTransition<To>(BaseWindowParams parameters)
		{
			var toWindowType = typeof(To);
			var current = GetCurrentWindow();

			if (current == null)
			{
				return;
			}

			var currentWindowType = current.GetType();

			if (currentWindowType == toWindowType)
			{
				return;
			}

			if (!IsPopUp(currentWindowType) || (IsPopUp(currentWindowType) && IsPopUp(toWindowType)))
			{
				history.Add(parameters, current);
			}

			if (IsGame(toWindowType) || IsMenu(toWindowType))
			{
				if (IsPopUp(currentWindowType))
				{
					var previous = history.GetPrevious();
					var window = previous.Window;
					window.Hide();
				}

				current.Hide();
			}

			if (IsPopUp(currentWindowType))
			{
				var previous = history.GetPrevious();
				RefreshHistory(previous.Window.GetType(), toWindowType);
			}
			else
			{
				RefreshHistory(currentWindowType, toWindowType);
			}
		}

		private void RefreshHistory(System.Type currentWindowType, System.Type toWindowType)
		{
			if ((IsMenu(currentWindowType) && IsGame(toWindowType)) || IsGame(currentWindowType) && IsMenu(toWindowType))
			{
				history.Clear();
			}
		}

		private WindowBase GetCurrentWindow()
		{
			return current;
		}

		private void SetUpBaseParams(BaseWindowParams parameters)
		{
			parameters.App = app;
		}
	}
}
