using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Microsoft.PowerPlatform.Dataverse.Ui.Styles
{
	/// <summary>
	/// HighContrastSettingsEventManager
	/// </summary>
	public class HighContrastSettingsEventManager : WeakEventManager
	{
		private HighContrastSettingsEventManager()
		{

		}

		/// <summary>
		/// Add a handler for the given HighContrastSettingsChanged event.
		/// </summary>
		public static void AddHandler(EventHandler<PropertyChangedEventArgs> handler)
		{
			if (handler == null)
				throw new ArgumentNullException("handler");

			CurrentManager.ProtectedAddHandler(null, handler);
		}

		/// <summary>
		/// Remove a handler for the given HighContrastSettingsChanged event.
		/// </summary>
		public static void RemoveHandler(EventHandler<PropertyChangedEventArgs> handler)
		{
			if (handler == null)
				throw new ArgumentNullException("handler");

			CurrentManager.ProtectedRemoveHandler(null, handler);
		}

		/// <summary>
		/// Get the event manager for the current thread.
		/// </summary>
		private static HighContrastSettingsEventManager CurrentManager
		{
			get
			{
				Type managerType = typeof(HighContrastSettingsEventManager);
				HighContrastSettingsEventManager manager =
					(HighContrastSettingsEventManager)GetCurrentManager(managerType);

				// at first use, create and register a new manager
				if (manager == null)
				{
					manager = new HighContrastSettingsEventManager();
					SetCurrentManager(managerType, manager);
				}
				return manager;
			}
		}

		/// <summary>
		/// Return a new list to hold listeners to the event.
		/// </summary>
		protected override ListenerList NewListenerList()
		{
			return new ListenerList<PropertyChangedEventArgs>();
		}


		/// <summary>
		/// Listen to static event StaticPropertyChanged.
		/// </summary>
		protected override void StartListening(object source)
		{
			SystemParameters.StaticPropertyChanged += OnStaticPropertyChanged;
		}

		/// <summary>
		/// Stop Listening to static event StaticPropertyChanged.
		/// </summary>
		protected override void StopListening(object source)
		{
			SystemParameters.StaticPropertyChanged -= OnStaticPropertyChanged;
		}

		/// <summary>
		/// Event handler for the StaticPropertyChanged event.
		/// </summary>
		void OnStaticPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != "HighContrast")
				return;

			DeliverEvent(null, e);
		}
	}
}
