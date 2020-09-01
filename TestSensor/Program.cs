using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using MarsSensor;
using SensorStandard;
using SensorStandard.MrsTypes;
using TestSensor.Properties;
using File = System.IO.File;

namespace TestSensor
{
	class Program
	{

		static void Main(string[] args)
		{

			var serializer = new XmlSerializer(typeof(DeviceConfiguration), new XmlRootAttribute(nameof(DeviceConfiguration)));
			var deviceConfiguration =
				(DeviceConfiguration) serializer.Deserialize(new StringReader(Resources.DeviceConfiguration));

			serializer = new XmlSerializer(typeof(DeviceStatusReport), new XmlRootAttribute(nameof(DeviceStatusReport)));
			var statusReport = (DeviceStatusReport) serializer.Deserialize(new StringReader(Resources.Status));

			Sensor sensor = new Sensor(deviceConfiguration, statusReport);
			sensor.MessageReceived += Sensor_MessageReceived;
			sensor.MessageSent += Sensor_MessageSent;
			sensor.ValidationErrorOccured += Sensor_ValidationErrorOccured;
			sensor.ValidateMessages = true;

			Console.WriteLine("Opening sensor web service...");
			sensor.OpenWebService();
			Console.WriteLine("Sensor web service opened on " + sensor.ServerAddress);
			Console.WriteLine("Press Esc to Exit");

			while (Console.ReadKey(true).Key != ConsoleKey.Escape)
			{
				sensor.SendFullDeviceStatusReport();
				Console.WriteLine("Press Esc to Exit");
			}

			Console.WriteLine("Closing web service...");
			sensor.CloseWebService();
			Console.WriteLine("Web service closed");
		}

		private static void Sensor_ValidationErrorOccured(object sender, InvalidMessageException messageException)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("Vaildation Error!\n" + messageException.Message);
			Console.ResetColor();
		}

		private static void Sensor_MessageSent(MrsMessage message, string marsName)
		{
			Console.WriteLine($"{message.MrsMessageType} message sent to {marsName}");
		}

		private static void Sensor_MessageReceived(MrsMessage message, string marsName)
		{
			if (message is CommandMessage commandMessage)
			{
				string command = commandMessage.Command.Item.ToString();
				Console.WriteLine($"{message.MrsMessageType} ({command}) received from {marsName}");
			}
			else
			{
				Console.WriteLine($"{message.MrsMessageType} received from {marsName}");
			}
		}

		private static string GetLocalIPAddress()
		{
			var host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (var ip in host.AddressList)
			{
				if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
				{
					return ip.ToString();
				}
			}

			return string.Empty;
		}

		private static PictureStatus CreatePictureStatus(FileInfo fileInfo)
		{
			return new PictureStatus
			{
				PictureState = PictureState.PicturesAttached,
				MediaFile = new[]
				{
					new ImageFile
					{
						CreationTime = new TimeType
						{
							Zone = TimezoneType.GMT,
							Value = fileInfo.CreationTime
						},
						Item = fileInfo.Name,
						File = File.ReadAllBytes(fileInfo.FullName)
					}
				}
			};
		}
	}
}
