using System;
using System.Globalization;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

namespace Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises
{
	internal sealed partial class ServiceConfiguration<TService> : IEndpointSwitch
	{
		public bool EndpointAutoSwitchEnabled { get; set; }

		public string GetAlternateEndpointAddress(string host)
		{
			var index = host.IndexOf('.');
			return host.Insert(index, "." + AlternateEndpointToken);
		}

		public void OnEndpointSwitchRequiredEvent()
		{
			var tmp = EndpointSwitchRequired;
			HandleEndpointEvent(tmp, (CurrentServiceEndpoint.Address.Uri == PrimaryEndpoint) ? AlternateEndpoint : PrimaryEndpoint, CurrentServiceEndpoint.Address.Uri);
		}

		public void OnEndpointSwitchedEvent()
		{
			var tmp = EndpointSwitched;
			HandleEndpointEvent(tmp, CurrentServiceEndpoint.Address.Uri, (CurrentServiceEndpoint.Address.Uri == PrimaryEndpoint) ? AlternateEndpoint : PrimaryEndpoint);
		}

		private void HandleEndpointEvent(EventHandler<EndpointSwitchEventArgs> tmp, Uri newUrl, Uri previousUrl)
		{
			if (tmp != null)
			{
				var args = new EndpointSwitchEventArgs();
				lock (_lockObject)
				{
					args.NewUrl = newUrl;
					args.PreviousUrl = previousUrl;
				}

				tmp(this, args);
			}
		}

		public event EventHandler<EndpointSwitchEventArgs> EndpointSwitched;

		public event EventHandler<EndpointSwitchEventArgs> EndpointSwitchRequired;

		public string AlternateEndpointToken { get; set; }

		public Uri AlternateEndpoint { get; internal set; }

		public Uri PrimaryEndpoint { get; internal set; }

		private void SetEndpointSwitchingBehavior()
		{
			if (ServiceEndpointMetadata.ServiceUrls == null)
			{
				return;
			}

			PrimaryEndpoint = ServiceEndpointMetadata.ServiceUrls.PrimaryEndpoint;

			bool enableFailover = false;
			bool endpointEnabled = true;
			if (!ServiceEndpointMetadata.ServiceUrls.GeneratedFromAlternate)
			{
				var bindingElements = CurrentServiceEndpoint.Binding.CreateBindingElements();
				var xrmPolicy = bindingElements.Find<FailoverPolicy>();
				if (xrmPolicy != null)
				{
					if (xrmPolicy.PolicyElements.ContainsKey(FailoverPolicy.FailoverAvailable))
					{
						enableFailover = Convert.ToBoolean(xrmPolicy.PolicyElements[FailoverPolicy.FailoverAvailable], CultureInfo.InvariantCulture);
						endpointEnabled = Convert.ToBoolean(xrmPolicy.PolicyElements[FailoverPolicy.EndpointEnabled], CultureInfo.InvariantCulture);
					}
				}
			}
			else
			{
				enableFailover = true;
			}

			if (enableFailover)
			{
				AlternateEndpoint = ServiceEndpointMetadata.ServiceUrls.AlternateEndpoint;
				if (!endpointEnabled)
				{
					SwitchEndpoint();
				}
			}
		}

		public bool IsPrimaryEndpoint
		{
			get
			{
				lock (_lockObject)
				{
					return AlternateEndpoint == null || CurrentServiceEndpoint.Address.Uri != AlternateEndpoint;
				}
			}
		}

		public bool CanSwitch(Uri currentUri)
		{
			ClientExceptionHelper.ThrowIfNull(currentUri, "currentUri");

			lock (_lockObject)
			{
				return currentUri == CurrentServiceEndpoint.Address.Uri;
			}
		}

		public bool HandleEndpointSwitch()
		{
			if (AlternateEndpoint != null)
			{
				OnEndpointSwitchRequiredEvent();
				if (EndpointAutoSwitchEnabled)
				{
					SwitchEndpoint();
					return true;
				}
			}

			return false;
		}

		public void SwitchEndpoint()
		{
			if (AlternateEndpoint == null)
			{
				return;
			}

			lock (_lockObject)
			{
#if NETFRAMEWORK
				if (CurrentServiceEndpoint.Address.Uri != AlternateEndpoint)
				{
					// Switch to backup, otherwise do nothing.
					CurrentServiceEndpoint.Address = new EndpointAddress(AlternateEndpoint, CurrentServiceEndpoint.Address.Identity,
						CurrentServiceEndpoint.Address.Headers);
				}
				else
				{
					CurrentServiceEndpoint.Address = new EndpointAddress(PrimaryEndpoint, CurrentServiceEndpoint.Address.Identity,
						CurrentServiceEndpoint.Address.Headers);
				}
#else
				throw new PlatformNotSupportedException("Xrm.Sdk WSDL");
#endif

				//OnEndpointSwitchedEvent();
			}
		}

		internal static ServiceUrls CalculateEndpoints(Uri serviceUri)
		{
			ServiceUrls endpoints = new ServiceUrls();
			var uBuilder = new UriBuilder(serviceUri);
			var segments = uBuilder.Host.Split('.');
			if (segments[0].EndsWith("--s", StringComparison.OrdinalIgnoreCase))
			{
				endpoints.AlternateEndpoint = uBuilder.Uri;

				// This is the secondary url
				segments[0] = segments[0].Remove(segments[0].Length - 3);
				uBuilder.Host = string.Join(".", segments);
				endpoints.PrimaryEndpoint = uBuilder.Uri;
				endpoints.GeneratedFromAlternate = true;
			}
			else
			{
				endpoints.PrimaryEndpoint = uBuilder.Uri;
				segments[0] += "--s";
				uBuilder.Host = string.Join(".", segments);
				endpoints.AlternateEndpoint = uBuilder.Uri;
			}

			return endpoints;
		}
	}
}
