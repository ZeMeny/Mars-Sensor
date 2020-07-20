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
		private static readonly Sensor sensor = Sensor.Instance;

		static void Main(string[] args)
		{
			sensor.ValidateMessages = true;
			sensor.MessageReceived += Sensor_MessageReceived;
			sensor.MessageSent += Sensor_MessageSent;
			sensor.ValidationErrorOccured += Sensor_ValidationErrorOccured;

			var serializer = new XmlSerializer(typeof(DeviceConfiguration), new XmlRootAttribute(nameof(DeviceConfiguration)));
			var deviceConfiguration =
				(DeviceConfiguration) serializer.Deserialize(new StringReader(Resources.DeviceConfiguration));

			serializer = new XmlSerializer(typeof(DeviceStatusReport), new XmlRootAttribute(nameof(DeviceStatusReport)));
			var statusReport = (DeviceStatusReport) serializer.Deserialize(new StringReader(Resources.Status));

			//FileInfo fileInfo;
			//ItemChoiceType3 fileType;

			//while (true)
			//{
			//	Console.WriteLine("Video or image? (v/i)");
			//	var key =  Console.ReadLine();
			//	if (key?.ToLower() == "i")
			//	{
			//		fileInfo = new FileInfo(@"D:\Development\קוד סימולטור\חדש\17977960.jpg");
			//		fileType = ItemChoiceType3.NameJPEG;
			//		if (fileInfo.Exists)
			//		{
			//			Console.WriteLine("Image loaded successfuly");
			//		}
			//		break;
			//	}
			//	if (key?.ToLower() == "v")
			//	{
			//		fileInfo = new FileInfo(@"D:\Development\קוד סימולטור\חדש\486360.mp4");
			//		fileType = ItemChoiceType3.NameMP4;
			//		if (fileInfo.Exists)
			//		{
			//			Console.WriteLine("Video loaded successfuly");
			//		}
			//		break;
			//	}

			//	Console.WriteLine("Invalid input, try again.");
			//}

			//var pictureStatus = CreatePictureStatus(fileInfo, fileType);

			Console.WriteLine("Opening sensor web service...");
			sensor.OpenWebService(deviceConfiguration, statusReport);
			Console.WriteLine("Sensor web service opened on " + sensor.ServerAddress);
			Console.WriteLine("Press Esc to Exit");

			//var opticalStatus = statusReport.Items.OfType<SensorStatusReport>()
			//	.FirstOrDefault(x => x.Item is OpticalStatus);
			//if (opticalStatus != null)
			//{
			//	opticalStatus.PictureStatus = pictureStatus;
			//}
			//else
			//{
			//	Console.ForegroundColor = ConsoleColor.Red;
			//	Console.WriteLine("No optical status found");
			//	Console.ResetColor();
			//}

			while (Console.ReadKey(true).Key != ConsoleKey.Escape)
			{
				sensor.SendFullDeviceStatusReport();
				Console.WriteLine("Press Esc to Exit");
			}

			Console.WriteLine("Closing web service...");
			sensor.CloseWebService();
			Console.WriteLine("Web service closed");
		}

		private static void Sensor_ValidationErrorOccured(InvalidMessageException messageException)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("Vaildation Error!\n" + messageException.Message);
			Console.ResetColor();
		}

		private static void Sensor_MessageSent(MarsMessageTypes messageType, object message, string marsName)
		{
			Console.WriteLine($"{messageType.ToString()} message sent to {marsName}");
		}

		private static void Sensor_MessageReceived(MarsMessageTypes messageType, object message, string marsName)
		{
			Console.WriteLine($"{messageType.ToString()} message recieved from {marsName}");
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

		private static PictureStatus CreatePictureStatus(FileInfo fileInfo, ItemChoiceType3 fileType)
		{
			return new PictureStatus
			{
				PictureState = PictureState.PicturesAttached,
				MediaFile = new[]
				{
					new SensorStandard.MrsTypes.File
					{
						CreationTime = new TimeType
						{
							Zone = TimezoneType.GMT,
							Value = fileInfo.CreationTime
						},
						ItemElementName = fileType,
						Item = fileInfo.Name,
						File1 = File.ReadAllBytes(fileInfo.FullName)
					}
				}
			};
		}
	}
}
