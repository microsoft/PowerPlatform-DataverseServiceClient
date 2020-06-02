namespace Microsoft.Xrm.Tooling.Connector.UnitTests
{
	using Microsoft.VisualStudio.TestTools.UnitTesting;
	using System.ServiceModel.Description;
	using System.Runtime.Serialization;

	[TestClass]
	public class DeviceIdManagerTest
	{
		[TestMethod]		
		public void LoadOrRegisterDeviceTest()
		{
			ClientCredentials clientCredentials = DeviceIdManager.LoadOrRegisterDevice();
			Assert.IsNotNull(clientCredentials);
		}

		[TestMethod]		
		public void LoadOrRegisterDeviceWithNameAndPasswordTest()
		{
			ClientCredentials clientCredentials = DeviceIdManager.LoadOrRegisterDevice("deviceName", "devicePasssword");
			Assert.IsNotNull(clientCredentials);
		}

		[TestMethod]		
		public void LoadDeviceCredentialsTest()
		{
			ClientCredentials clientCredentials = DeviceIdManager.LoadDeviceCredentials();
			Assert.IsNotNull(clientCredentials);
		}		
		[TestMethod]		
		public void ToClientCredentialsTest()
		{
			DeviceUserName deviceUserName = new DeviceUserName();
			ClientCredentials clientCredentials=deviceUserName.ToClientCredentials();
			Assert.IsNotNull(clientCredentials);
		}
	}
}