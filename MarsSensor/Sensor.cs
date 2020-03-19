using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using MarsSensor.Extensions;
using SensorStandard;
using SensorStandard.MrsTypes;
using SensorIdentificationType = SensorStandard.MrsTypes.SensorIdentificationType;

namespace MarsSensor
{
	/// <summary>
	/// Generic class for mars sensors
	/// </summary>
	public class Sensor
	{
		#region / / / / /  Singleton  / / / / /

		/// <summary>
		/// Singleton instance
		/// </summary>
		public static Sensor Instance
		{
			get
			{
				if (instance != null)
				{
					return instance;
				}
				return new Sensor();
			}
		}
		private static Sensor instance;
		private Sensor()
		{
			instance = this;
			_marsClients = new Dictionary<string, MarsClient>();
			_sensorTimer = new Timer(1000);
			_sensorTimer.Elapsed += SensorTimer_Elapsed;
		}

		#endregion


		#region / / / / /  Private fields  / / / / /

		private ServiceHost _sensorServiceHost;
		private readonly Dictionary<string, MarsClient> _marsClients;
		private readonly Timer _sensorTimer;
		private const double FullStatusInterval = 60; // once a minute
		private TimeSpan _timerTimeStamp;
		private IndicationType _lastDetectionReceived;
		private readonly object _syncToken = new object();

		#endregion


		#region / / / / /  Properties  / / / / /

		/// <summary>
		/// Sensor IP
		/// </summary>
		public string IP { get; private set; }

		/// <summary>
		/// Sensor listening port
		/// </summary>
		public int Port { get; private set; }

		/// <summary>
		/// Gets a value that indicates weather the server is open
		/// </summary>
		public bool IsOpen { get; private set; }

		/// <summary>
		/// Gets or sets a value that indicates if the sensor will stop sending messages to clients 
		/// that didn't send any request in the last <see cref="ConnectionTimeout"/>
		/// </summary>
		public bool CanTimeout { get; set; } = true;

		/// <summary>
		/// Gets or sets the Client idle timeout
		/// </summary>
		public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(1);

		/// <summary>
		/// Gets the url of the sensor's web service
		/// </summary>
		public string ServerAddress { get; private set; }

		/// <summary>
		/// Gets or sets weather the sensor will Send invalid messages
		/// </summary>
		public bool ValidateMessages { get; set; } = true;

		/// <summary>
		/// Gets or Sets the Current Device Configuration
		/// </summary>
		public DeviceConfiguration DeviceConfiguration { get; set; }

		/// <summary>
		/// Gets or Sets the Current Full Status Report
		/// </summary>
		public DeviceStatusReport StatusReport { get; set; }

		#endregion


		#region / / / / /  Public methods  / / / / /

		/// <summary>
		/// Open sensor web service
		/// </summary>
		/// <param name="configuration">sensor's configuration</param>
		/// <param name="status">sensor's current FULL status report</param>
		public void OpenWebService(DeviceConfiguration configuration, DeviceStatusReport status)
		{
			if (configuration == null)
			{
				throw new ArgumentNullException(nameof(configuration));
			}

			var pattern = @"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";

			IP = configuration.NotificationServiceIPAddress;

			if (!Regex.IsMatch(IP, pattern))
			{
				throw new ArgumentException("Invalid IP adderss!", nameof(configuration.NotificationServiceIPAddress));
			}

			if (int.TryParse(configuration.NotificationServicePort, out int port))
			{
				Port = port;
			}
			else
			{
				throw new ArgumentException("Invalid Port", nameof(configuration.NotificationServicePort));
			}

			if (Port <= 0 || Port > 65535)
			{
				throw new ArgumentException("Invaild port! port must be higher than zero and lower than 65535", nameof(port));
			}

			DeviceConfiguration = configuration;
			StatusReport = status ?? throw new ArgumentNullException(nameof(status));

			string address = $"http://{IP}:{Port}/";

			try
			{
				// close existing host
				_sensorServiceHost?.Abort();
				_sensorServiceHost = new ServiceHost(typeof(MarsService), new Uri(address));

				// add detailed exception reports
				foreach (var serviceBehavior in _sensorServiceHost.Description.Behaviors)
				{
					if (serviceBehavior is ServiceBehaviorAttribute serviceBehaviorAttribute)
					{
						serviceBehaviorAttribute.IncludeExceptionDetailInFaults = true;
					}
				}

				// add behavior for our MEX endpoint
				var behavior = new ServiceMetadataBehavior()
				{
					HttpGetEnabled = true
				};
				_sensorServiceHost.Description.Behaviors.Add(behavior);

				// Create basicHttpBinding endpoint
				_sensorServiceHost.AddServiceEndpoint(typeof(SNSR_STDSOAPPort), CreateBindingConfig(),
					"SNSR_STD-SOAP");

				// Add MEX endpoint
				_sensorServiceHost.AddServiceEndpoint(typeof(IMetadataExchange), new BasicHttpBinding(),
					"MEX");

				ServerAddress = address + "SNSR_STD-SOAP";

				_sensorServiceHost.Open();
				IsOpen = _sensorServiceHost.State == CommunicationState.Opened;

				_sensorTimer.Start();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				throw;
			}
		}

		/// <summary>
		/// Close the web service
		/// </summary>
		public void CloseWebService()
		{
			if (_sensorServiceHost != null)
			{
				// close the server
				_sensorServiceHost.Close();
				IsOpen = false;
			}

			lock (_syncToken)
			{
				// close all clients
				foreach (var client in _marsClients.Values)
				{
					client.SoapClient.Close();
				}
				_marsClients.Clear(); 
			}

			// stop the status timer
			_sensorTimer.Stop();
		}

		/// <summary>
		/// Send a <see cref="DeviceConfiguration"/> message to a specific mars client
		/// </summary>
		/// <param name="clientName">mars name (can be found in <see cref="_marsClients"/>)</param>
		public void SendDeviceConfig(string clientName)
		{
			if (_marsClients.ContainsKey(clientName))
			{
				if (!ValidateMessages || DeviceConfiguration.IsValid(out var exception))
				{
					_marsClients[clientName].SoapClient.BegindoDeviceConfiguration(DeviceConfiguration, null, null);
					MessageSent?.Invoke(MarsMessageTypes.DeviceConfiguration, DeviceConfiguration, clientName);
				}
				else
				{
					ValidationErrorOccured?.Invoke(new InvalidMessageException(MarsMessageTypes.DeviceConfiguration, DeviceConfiguration, exception.Message));
				}
			}
		}

		/// <summary>
		/// Send a <see cref="DeviceConfiguration"/> message to every mars client subscribed
		/// </summary>
		public void SendDeviceConfig()
		{
			foreach (var client in _marsClients)
			{
				SendDeviceConfig(client.Key);
			}
		}

		/// <summary>
		/// Send a full <see cref="DeviceStatusReport"/> message to a specific mars client
		/// </summary>
		/// <param name="clientName">mars name (can be found in <see cref="_marsClients"/>)</param>
		public void SendFullDeviceStatusReport(string clientName)
		{
			if (_marsClients.ContainsKey(clientName))
			{
				if (!ValidateMessages || StatusReport.IsValid(out var exception))
				{
					_marsClients[clientName].SoapClient.BegindoDeviceStatusReport(StatusReport, null, null);
					MessageSent?.BeginInvoke(MarsMessageTypes.DeviceStatusReport, StatusReport, clientName, null, null);
				}
				else
				{
					ValidationErrorOccured?.Invoke(new InvalidMessageException(MarsMessageTypes.DeviceStatusReport, StatusReport, exception.Message));
				}
			}
		}

		/// <summary>
		/// Send a full <see cref="DeviceStatusReport"/> message to every mars client subscribed
		/// </summary>
		public void SendFullDeviceStatusReport()
		{
			foreach (var client in _marsClients)
			{
				SendFullDeviceStatusReport(client.Key);
			}
		}

		/// <summary>
		/// Send an empty <see cref="DeviceStatusReport"/> message to a specific mars client
		/// </summary>
		/// <param name="clientName">mars name (can be found in <see cref="_marsClients"/>)</param>
		public void SendEmptyDeviceStatusReport(string clientName)
		{
			if (_marsClients.ContainsKey(clientName))
			{
				var emptyStatus = GetEmptyStatus(StatusReport);
				if (!ValidateMessages || emptyStatus.IsValid(out var exception))
				{
					_marsClients[clientName].SoapClient.BegindoDeviceStatusReport(emptyStatus, null, null);
					MessageSent?.BeginInvoke(MarsMessageTypes.DeviceStatusReport, emptyStatus, clientName, null, null);
				}
				else
				{
					ValidationErrorOccured?.Invoke(new InvalidMessageException(MarsMessageTypes.DeviceStatusReport, emptyStatus, exception.Message));
				}
			}
		}

		/// <summary>
		/// Send an empty <see cref="DeviceStatusReport"/> message to every mars client subscribed
		/// </summary>
		public void SendEmptyDeviceStatusReport()
		{
			foreach (var client in _marsClients)
			{
				SendEmptyDeviceStatusReport(client.Key);
			}
		}

		/// <summary>
		/// Send a custom <see cref="DeviceStatusReport"/> message to a specific mars client
		/// </summary>
		/// <param name="statusReport">the status report</param>
		/// <param name="clientName">mars name (can be found in <see cref="_marsClients"/>)</param>
		public void SendCustomDeviceStatusReport(DeviceStatusReport statusReport, string clientName)
		{
			if (_marsClients.ContainsKey(clientName))
			{
				var emptyStatus = GetEmptyStatus(StatusReport);
				if (!ValidateMessages || emptyStatus.IsValid(out var exception))
				{
					_marsClients[clientName].SoapClient.BegindoDeviceStatusReport(emptyStatus, null, null);
					MessageSent?.BeginInvoke(MarsMessageTypes.DeviceStatusReport, statusReport, clientName, null, null);
				}
				else
				{
					ValidationErrorOccured?.Invoke(new InvalidMessageException(MarsMessageTypes.DeviceStatusReport, emptyStatus, exception.Message));
				}
			}
		}

		/// <summary>
		/// Send a custom <see cref="DeviceStatusReport"/> message to every mars client subscribed
		/// </summary>
		public void SendCustomDeviceStatusReport(DeviceStatusReport statusReport)
		{
			foreach (var client in _marsClients)
			{
				SendCustomDeviceStatusReport(statusReport, client.Key);
			}
		}

		/// <summary>
		/// Set the current communication, technical and power state of the sensor
		/// </summary>
		/// <param name="status">true for OK, false for Fault</param>
		public void SetSensorStatus(bool status)
		{
			if (StatusReport != null)
			{
				foreach (SensorStatusReport item in StatusReport.Items.OfType<SensorStatusReport>())
				{
					item.CommunicationState = status ? BITResultType.OK : BITResultType.Fault;
					item.PowerState = status ? StatusType.Yes : StatusType.No;
					item.SensorTechnicalState = status ? BITResultType.OK : BITResultType.Fault;
				}
			}
		}

		/// <summary>
		/// Set the location configuration of the sensor
		/// </summary>
		/// <param name="latitude">WGS84 Degree based latitude</param>
		/// <param name="longitude">WGS84 Degree based longitude</param>
		/// <param name="altitude">meters based altitude</param>
		/// <param name="reference">"msl" for sea level, default is ground level</param>
		public void SetSensorLocation(double latitude, double longitude, double altitude, AltitudeReferences reference)
		{
			if (DeviceConfiguration != null)
			{
				DeviceConfiguration.LocationType = new LocationType
				{
					Item = new GeodeticLocation
					{
						Altitude = new AltitudeType
						{
							Units = DistanceUnitsType.Meters,
							Value = altitude,
							Reference = reference == AltitudeReferences.Msl ? AltitudeReferenceType.MSL : AltitudeReferenceType.AGL
						},
						Latitude = new Latitude
						{
							Units = LatLonUnitsType.DecimalDegrees,
							Value = latitude
						},
						Longitude = new Longitude
						{
							Units = LatLonUnitsType.DecimalDegrees,
							Value = longitude
						},
						Datum = DatumType.WGS84,
					}
				};
			}
			if (StatusReport != null)
			{
				foreach (SensorStatusReport item in StatusReport.Items.OfType<SensorStatusReport>())
				{
					if (item.Item is AntennaStatus antenna)
					{
						if (antenna.Sector.Center.Item is LocationType location)
						{
							if (location.Item is GeodeticLocation geodetic)
							{
								geodetic.Altitude.Value = altitude;
								geodetic.Latitude.Value = latitude;
								geodetic.Longitude.Value = longitude;
							}
						}
					}
					else if (item.Item is DroneStatus droneStatus)
					{
						if (droneStatus.LocationType == null)
						{
							droneStatus.LocationType = new LocationType
							{
								Item = new GeodeticLocation()
							};
						}
						if (droneStatus.LocationType.Item is GeodeticLocation geodetic)
						{
							geodetic.Altitude.Value = altitude;
							geodetic.Altitude.Reference = reference == AltitudeReferences.Agl ? AltitudeReferenceType.AGL : AltitudeReferenceType.MSL;
							geodetic.Latitude.Value = latitude;
							geodetic.Longitude.Value = longitude;
						}
					}
				}
				SendFullDeviceStatusReport();
			}
		}

		/// <summary>
		/// Set the the min-max Parameters for every radar in the sensor configuration (if it has any)
		/// </summary>
		/// <param name="latitude">Radar latitude</param>
		/// <param name="longitude">Radar longitude</param>
		/// <param name="altitude">Radar altitude</param>
		/// <param name="minAzimuth">Radar Azimuth range minimum value</param>
		/// <param name="maxAzimuth">Radar Azimuth range maximum value</param>
		/// <param name="range">Radar range</param>
		public void SetRadarPerimiter(double latitude, double longitude, double altitude, double minAzimuth, double maxAzimuth, double range)
		{
			if (DeviceConfiguration?.SensorConfiguration != null)
			{
				foreach (var item in DeviceConfiguration.SensorConfiguration)
				{
					if (item.Item is AntennaConfiguration antenna)
					{
						antenna.AzimuthRange = new AzimuthRangeType
						{
							Min = new AzimuthType
							{
								Units = AngularUnitsType.Mils,
								Value = minAzimuth
							},
							Max = new AzimuthType
							{
								Units = AngularUnitsType.Mils,
								Value = maxAzimuth
							},
						};
						antenna.RangeRange = new ValueRangeType
						{
							Max = range,
						};
					}
				}
			}
			if (StatusReport != null)
			{
				foreach (SensorStatusReport statusReport in StatusReport.Items.OfType<SensorStatusReport>())
				{
					if (statusReport.Item is AntennaStatus antenna)
					{
						if (antenna.Sector.Center.Item is LocationType location)
						{
							if (location.Item is GeodeticLocation geodetic)
							{
								geodetic.Altitude.Value = altitude;
								geodetic.Latitude.Value = latitude;
								geodetic.Longitude.Value = longitude;
							}
						}
						antenna.Sector.AzimuthStart.Value = minAzimuth;
						antenna.Sector.AzimuthEnd.Value = maxAzimuth;
						antenna.Sector.MaxRange.Value = range;
						antenna.Sector.MinRange.Value = 0;
					}
				}
			}
		}

		/// <summary>
		/// Set the direction for every radar in the current sensor configuration (if it has any)
		/// </summary>
		/// <param name="azimuthStart">Radar current perimiter start azimuth</param>
		/// <param name="azimuthEnd">Radar current perimiter end azimuth</param>
		public void SetRadarPosition(double azimuthStart, double azimuthEnd)
		{
			if (StatusReport?.Items != null)
			{
				foreach (var item in StatusReport.Items.OfType<AntennaStatus>())
				{
					item.Sector = new Sector
					{
						AzimuthStart = new AzimuthType
						{
							Units = AngularUnitsType.Mils,
							Value = azimuthStart
						},
						AzimuthEnd = new AzimuthType
						{
							Units = AngularUnitsType.Mils,
							Value = azimuthEnd,
						},
						Center = DeviceConfiguration.LocationType.Item as Point,
					};
				}
			}
		}

		/// <summary>
		/// Send indication report to every mars client subscribed
		/// </summary>
		/// <param name="detections">Report's content</param>
		public void SendIndicationReport(params IndicationType[] detections)
		{
			if (detections.Length > 0)
			{
				foreach (var mars in _marsClients)
				{
					if (mars.Value.SubscriptionTypes != null)
					{
						if (mars.Value.SubscriptionTypes.Contains(SubscriptionTypeType.OperationalIndication))
						{
							if (detections.Length > 400)
							{
								detections = detections.Take(400).ToArray();
							}
							DeviceIndicationReport indicationReport = CreateIndicationReport(detections);
							if (indicationReport != null)
							{
								SendSingleIndicationReport(indicationReport, mars.Key);
							}
						}
					}
				}
				_lastDetectionReceived = detections.Last();
			}
		}

		/// <summary>
		/// Send indication report from a specific sensor to every mars client subscribed
		/// </summary>
		/// <param name="sensorName">configured sensor name</param>
		/// <param name="detections">Report's content</param>
		public void SendIndicationReport(string sensorName, params IndicationType[] detections)
		{
			if (detections.Length > 0)
			{
				foreach (var mars in _marsClients)
				{
					if (mars.Value.SubscriptionTypes != null)
					{
						if (mars.Value.SubscriptionTypes.Contains(SubscriptionTypeType.OperationalIndication))
						{
							if (detections.Length > 400)
							{
								detections = detections.Take(400).ToArray();
							}
							DeviceIndicationReport indicationReport = CreateIndicationReport(detections, sensorName);
							if (indicationReport != null)
							{
								SendSingleIndicationReport(indicationReport, mars.Key);
							}
						}
					}
				}
				_lastDetectionReceived = detections.Last();
			}
		}

		/// <summary>
		/// Send indication report from a specific sensor to every mars client subscribed
		/// </summary>
		/// <param name="detectionTypeName">detection type</param>
		/// <param name="sensorName">configured sensor name</param>
		/// <param name="detections">Report's content</param>
		public void SendIndicationReport(string detectionTypeName, string sensorName, params IndicationType[] detections)
		{
			if (detections.Length > 0)
			{
				foreach (var mars in _marsClients)
				{
					if (mars.Value.SubscriptionTypes != null)
					{
						if (mars.Value.SubscriptionTypes.Contains(SubscriptionTypeType.OperationalIndication))
						{
							if (detections.Length > 400)
							{
								detections = detections.Take(400).ToArray();
							}
							DeviceIndicationReport indicationReport = CreateIndicationReport(detections, sensorName, detectionTypeName);
							if (indicationReport != null)
							{
								SendSingleIndicationReport(indicationReport, mars.Key);
							}
						}
					}
				}
				_lastDetectionReceived = detections.Last();
			}
		}

		#endregion


		#region / / / / /  Internal methods  / / / / /

		internal void HandleConfigRequest(DeviceConfiguration request)
		{
			if (request == null)
			{
				return;
			}

			// if this is not config request
			if (request.MessageTypeSpecified == false ||
				request.MessageType != MessageType.Request)
			{
				return;
			}

			// get mars details
			string marsIp = request.NotificationServiceIPAddress;
			string marsPort = request.NotificationServicePort;
			string marsName = request.RequestorIdentification;

			// save this client under the mars name for future use
			if (_marsClients.ContainsKey(marsName))
			{
				_marsClients[marsName].LastConnectionTime = DateTime.Now;
			}
			else
			{
				// open sensor client for this mars 
				EndpointAddress address = new EndpointAddress($"http://{marsIp}:{marsPort}/SNSR_STD-SOAP");
				SNSR_STDSOAPPortClient soapClient = new SNSR_STDSOAPPortClient(CreateBindingConfig(), address);
				MarsClient client = new MarsClient
				{
					IP = marsIp,
					Port = int.Parse(marsPort),
					LastConnectionTime = DateTime.Now,
					SoapClient = soapClient,
				};

				lock (_syncToken)
				{
					_marsClients.Add(marsName, client); 
				}

				// open the client
				soapClient.Open();
			}

			MessageReceived?.BeginInvoke(MarsMessageTypes.DeviceConfiguration, request,
				request.RequestorIdentification, null, null);

			// send device config
			SendDeviceConfig(marsName);
		}

		internal void HandleSubscriptionRequest(DeviceSubscriptionConfiguration request)
		{
			if (request == null)
			{
				return;
			}

			// get requestor name
			string name = request.RequestorIdentification;
			SubscriptionTypeType[] subscribeTypes = request.SubscriptionType;

			// add mars name to subscribed list
			if (_marsClients.ContainsKey(name))
			{
				// update connection watch to this mars
				_marsClients[name].LastConnectionTime = DateTime.Now;

				// if unsubscribe request
				if (subscribeTypes == null || subscribeTypes.Length == 0)
				{
					lock (_syncToken)
					{
						_marsClients.Remove(name); 
					}
				}
				else
				{
					_marsClients[name].SubscriptionTypes = subscribeTypes;
				}

				// raise the event
				MessageReceived?.BeginInvoke(MarsMessageTypes.DeviceSubscription, request, name, 
					null, null);

				// send full status report for the first time
				SendFullDeviceStatusReport(name);
			}
			else
			{
				Console.WriteLine("Unknown Mars name");
			}
		}

		internal void HandleCommandMessageRequest(CommandMessage request)
		{
			if (request == null)
			{
				return;
			}

			string deviceName = request.RequestorIdentification;

			// update last connection time
			if (_marsClients.ContainsKey(deviceName))
			{
				_marsClients[deviceName].LastConnectionTime = DateTime.Now;

				// raise the event
				MessageReceived?.BeginInvoke(MarsMessageTypes.CommandMessage, request, deviceName,
					null, null);

				// answer with empty status report
				SendEmptyDeviceStatusReport(deviceName);
			}
			else
			{
				Console.WriteLine("Unknown mars name");
			}
		}

		#endregion


		#region / / / / /  Private methods  / / / / /

		private void SensorTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			// increment timestamp
			_timerTimeStamp = _timerTimeStamp.Add(TimeSpan.FromMilliseconds(_sensorTimer.Interval));

			if (CanTimeout)
			{
				List<string> namesToRemove = new List<string>();
				lock (_syncToken)
				{
					foreach (var client in _marsClients)
					{
						// if mars timeout occured
						if (DateTime.Now - client.Value.LastConnectionTime >= ConnectionTimeout)
						{
							// unsubscribe this mars
							client.Value.SoapClient.Abort();
							namesToRemove.Add(client.Key);
						}
					}
					namesToRemove.ForEach(x => _marsClients.Remove(x)); 
				}
			}

			// if its time to send full status report
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (_timerTimeStamp.TotalSeconds % FullStatusInterval == 0)
			{
				// send to all subscribed clients
				foreach (var client in _marsClients)
				{
					// if client has subscribed to status reports
					if (client.Value.SubscriptionTypes != null && 
					    client.Value.SubscriptionTypes.Contains(SubscriptionTypeType.TechnicalStatus))
					{
						SendFullDeviceStatusReport(client.Key);
					}
				}
			}
		}

		private void SendSingleIndicationReport(DeviceIndicationReport report, string marsName)
		{
			if (_marsClients.ContainsKey(marsName))
			{
				if (!ValidateMessages || report.IsValid(out var exception))
				{
					_marsClients[marsName].SoapClient.BegindoDeviceIndicationReport(report, null, null);
					MessageSent?.BeginInvoke(MarsMessageTypes.DeviceIndicationReport, report, marsName, null, null);
				}
				else
				{
					ValidationErrorOccured?.Invoke(new InvalidMessageException(MarsMessageTypes.DeviceIndicationReport, report, exception.Message));
				}                
			}
		}

		private double CalculateAzimuth(System.Windows.Point a, System.Windows.Point b)
		{
			double p1 = UnitConverter.RadianToDegree(a.X);
			double p2 = UnitConverter.RadianToDegree(b.X);
			double l1 = UnitConverter.RadianToDegree(a.Y);
			double l2 = UnitConverter.RadianToDegree(b.Y);
			double dl = l2 - l1;

			return Math.Atan2((Math.Sin(dl) * Math.Cos(p2)),
				Math.Cos(p1) * Math.Sin(p2) - Math.Sin(p1) * Math.Cos(p2) * Math.Cos(dl));
		}

		private DeviceIndicationReport CreateIndicationReport(IndicationType[] detections, string sensorName = null, string detectionTypeName = null)
		{
			List<IndicationType> indications = new List<IndicationType>();

			int i = 0;
			double azimuth = 0;
			foreach (IndicationType indication in detections)
			{
				// add direction to aerial detections
				if (indication.Item is AerialTrackDetectionType trackDetectionType)
				{
					// calculate azimuth by previous location
					if (i > 0 && detections[i - 1].ID == indication.ID)
					{
						IndicationType previous = detections[i - 1];
						if (previous.Item is AerialTrackDetectionType aerialTrack)
						{
							System.Windows.Point a = UnitConverter.LocationToPoint(trackDetectionType.Location);
							System.Windows.Point b = UnitConverter.LocationToPoint(aerialTrack.Location);
							azimuth = UnitConverter.DegreeToMils(UnitConverter.RadianToDegree(CalculateAzimuth(a, b)));
						}
					}
					// if no previous, look for the last aerial detection with the same ID
					else if (_lastDetectionReceived != null && _lastDetectionReceived.ID == indication.ID)
					{
						if (_lastDetectionReceived.Item is AerialTrackDetectionType aerialTrack)
						{
							System.Windows.Point a = UnitConverter.LocationToPoint(trackDetectionType.Location);
							System.Windows.Point b = UnitConverter.LocationToPoint(aerialTrack.Location);
							azimuth = UnitConverter.DegreeToMils(UnitConverter.RadianToDegree(CalculateAzimuth(a, b)));
						}
					}

					trackDetectionType.Direction = new AzimuthType
					{
						Units = AngularUnitsType.Mils,
						Value = azimuth,
					};
				}

				indications.Add(indication);
				i++;
			}

			// create identification items
			DeviceIdentificationType deviceIdentificationType;
			SensorIdentificationType sensorIdentification;
			if (sensorName != null)
			{
				// if device hub
				if (DeviceConfiguration.DeviceConfiguration1 != null)
				{
					// find wanted device (sensorName == device name)
					DeviceConfiguration deviceConfiguration = DeviceConfiguration.DeviceConfiguration1.FirstOrDefault(x => x.DeviceIdentification.DeviceName == sensorName);
					deviceIdentificationType = deviceConfiguration?.DeviceIdentification;
					// take first sensor
					SensorConfiguration sensorConfiguration = deviceConfiguration?.SensorConfiguration.FirstOrDefault();
					sensorIdentification = sensorConfiguration?.SensorIdentification;
				}
				else
				{
					// take first device
					deviceIdentificationType = DeviceConfiguration.DeviceIdentification;
					// find wanted sensor (sensorName == sensor name)
					SensorConfiguration sensorConfiguration = DeviceConfiguration.SensorConfiguration.FirstOrDefault(x => x.SensorIdentification.SensorName == sensorName);
					sensorIdentification = sensorConfiguration?.SensorIdentification;
				}
			}
			else
			{
				deviceIdentificationType = DeviceConfiguration.DeviceIdentification;
				SensorConfiguration sensorConfiguration = DeviceConfiguration.SensorConfiguration.FirstOrDefault();
				sensorIdentification = sensorConfiguration?.SensorIdentification;
			}

			SensorIndicationReport sensorIndication = new SensorIndicationReport
			{
				IndicationType = indications.ToArray(),
				SensorIdentification = sensorIdentification
			};
			object content;

			// if device hub
			if (DeviceConfiguration.DeviceConfiguration1 != null)
			{
				content = new DeviceIndicationReport
				{
					DeviceIdentification = deviceIdentificationType,
					ProtocolVersion = ProtocolVersionType.Item22,
					Items = new object[]
					{
						sensorIndication
					}
				};
			}
			else
			{
				content = sensorIndication;
			}
			// main device
			DeviceIndicationReport indicationReport = new DeviceIndicationReport
			{
				DeviceIdentification = DeviceConfiguration.DeviceIdentification,
				Items = new[]
				{
					content,
				}
			};

			return indicationReport;
		}

		private BasicHttpBinding CreateBindingConfig()
		{
			return new BasicHttpBinding
			{
				CloseTimeout = TimeSpan.FromMinutes(1),
				OpenTimeout = TimeSpan.FromMinutes(1),
				SendTimeout = TimeSpan.FromMinutes(1),
				ReceiveTimeout = TimeSpan.FromMinutes(10),
				AllowCookies = false,
				BypassProxyOnLocal = false,
				HostNameComparisonMode = HostNameComparisonMode.StrongWildcard,
				MaxBufferSize = 65535 * 1000,
				MaxBufferPoolSize = 524288 * 1000,
				MaxReceivedMessageSize = 65535 * 1000,
				MessageEncoding = WSMessageEncoding.Text,
				TextEncoding = Encoding.UTF8,
				TransferMode = TransferMode.Buffered,
				UseDefaultWebProxy = true,
				Security = new BasicHttpSecurity
				{
					Mode = BasicHttpSecurityMode.None,
					Transport = new HttpTransportSecurity
					{
						ClientCredentialType = HttpClientCredentialType.None,
						ProxyCredentialType = HttpProxyCredentialType.None,
						Realm = string.Empty
					},
					Message = new BasicHttpMessageSecurity
					{
						AlgorithmSuite = SecurityAlgorithmSuite.Default,
						ClientCredentialType = BasicHttpMessageCredentialType.UserName
					}
				},
			};
		}

		private DeviceStatusReport GetEmptyStatus(DeviceStatusReport fullStatus)
		{
			// create new deep copy
			var status = fullStatus.Copy();

			// empty out status items
			status.Items = status.Items.OfType<SensorStatusReport>().Select(x => x.Copy()).ToArray();
			foreach (SensorStatusReport statusReport in status.Items)
			{
				statusReport.Item = null;
				statusReport.PictureStatus = null;
			}
			return status;
		}


		#endregion


		#region / / / / /  Events  / / / / /

		/// <summary>
		/// Occurs when a <see cref="DeviceConfiguration"/> request is received
		/// </summary>
		public event MarsMessageEventHandler MessageReceived;

		/// <summary>
		/// Occurs after a <see cref="DeviceIndicationReport"/> was sent
		/// </summary>
		public event MarsMessageEventHandler MessageSent;

		/// <summary>
		/// Occurs after an attempt to send an invalid mars message
		/// </summary>
		public event ValidationErrorEventHandler ValidationErrorOccured;


		#endregion


		#region / / / / /  Nested classes  / / / / /

		private sealed class MarsClient
		{
			public SNSR_STDSOAPPortClient SoapClient { get; set; }
			public SubscriptionTypeType[] SubscriptionTypes { get; set; }
			public DateTime LastConnectionTime { get; set; }
			public string IP { get; set; }
			public int Port { get; set; }
		}

		#endregion
	}

	/// <summary>
	/// Event Handler for any mars message events
	/// </summary>
	/// <param name="messageType">Message type</param>
	/// <param name="message">the message object</param>
	/// <param name="marsName">Name of the mars client associated with the event</param>
	public delegate void MarsMessageEventHandler(MarsMessageTypes messageType, object message, string marsName);
	/// <summary>
	/// Event handler for mars validation errors
	/// </summary>
	/// <param name="messageException">contains details of the validation error</param>
	public delegate void ValidationErrorEventHandler(InvalidMessageException messageException);
}
