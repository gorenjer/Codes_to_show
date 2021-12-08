using Game.Diagnostic;

namespace Game.Runtime
{
	public interface IManager
	{
		bool IsInitialized();
	}
	
	public class Manager : IManager
	{
		private bool initialized;
		
		public bool IsInitialized()
		{
			return initialized;
		}

		protected void SetInitialized(bool state)
		{
			if (state)
			{
				GameDebug.Log(GetType().Name, "Initialized!");
			}
			else
			{
				GameDebug.Log(GetType().Name, "Released!");
			}

			this.initialized = state;
		}
	}
}
