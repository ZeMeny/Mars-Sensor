using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using MrsSensor.Socket;
using MrsSensor.Socket.Extensions;
using SensorStandard;
using SensorStandard.MrsTypes;
using WatsonWebsocket;

namespace MrsSensor.Socket
{
    public class Sensor
    {
        #region / / / / /  Private fields  / / / / /

        private WatsonWsServer _socket;
        private readonly Dictionary<string, MrsClient> _marsClients;
        private readonly Timer _timer;
        private TimeSpan _timerTimeStamp;
        private readonly object _syncToken = new object();
        private readonly List<IndicationType> _pendingIndications;

        #endregion


        #region / / / / /  Properties  / / / / /

        public string IP => Configuration?.NotificationServiceIPAddress;

        public int Port => int.Parse(Configuration.NotificationServicePort);

        public bool IsActive => _socket != null;

        public DeviceConfiguration Configuration { get; }

        public DeviceStatusReport StatusReport { get; }

        public bool ValidateMessages { get; set; } = true;

        public bool ValidateClients { get; set; } = true;

        public bool AutoStatusReports { get; set; }

        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(1);

        public TimeSpan FullStatusInterval { get; set; } = TimeSpan.FromSeconds(60);

        public bool CanTimeout { get; set; } = true;

        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(1);

        #endregion


        #region / / / / /  Private methods  / / / / /

        private void HandleConfigRequest(string ipPort, DeviceConfiguration configuration)
        {
            string name = configuration.RequestorIdentification;

            // if message is invalid - throw
            if (!ValidateMessages || configuration.IsValid(out var ex))
            {
                // notify message
                MessageReceived?.BeginInvoke(configuration, name, null, null);

                if (_marsClients.ContainsKey(name) == false)
                {
                    _marsClients.Add(name, new MrsClient
                    {
                        LastConnectionTime = DateTime.Now,
                        IPPort = ipPort
                    });
                }
                else
                {
                    // if client is already registered - update values
                    _marsClients[name].LastConnectionTime = DateTime.Now;
                    _marsClients[name].IPPort = ipPort;
                }

                _socket.SendAsync(_marsClients[name].IPPort, Configuration.ToXml());
            }
            else
            {
                ValidationErrorOccured?.BeginInvoke(this, new InvalidMessageException(configuration, ex), null, null);
            }

        }

        private void HandleCommandMessage(CommandMessage commandMessage)
        {
            string name = commandMessage.RequestorIdentification;

            // if message is invalid - throw
            if (!ValidateMessages || commandMessage.IsValid(out var ex))
            {
                // notify message
                MessageReceived?.BeginInvoke(commandMessage, name, null, null);

                // if client is registered
                if (!ValidateClients || _marsClients.ContainsKey(name))
                {
                    _marsClients[name].LastConnectionTime = DateTime.Now;
                    SendEmptyDeviceStatusReport(name);
                } 
            }
            else
            {
                ValidationErrorOccured?.BeginInvoke(this, new InvalidMessageException(commandMessage, ex), null, null);
            }
        }

        private void HandleSubscriptionRequest(DeviceSubscriptionConfiguration subscription)
        {
            string name = subscription.RequestorIdentification;

            // if message is invalid - throw
            if (!ValidateMessages || subscription.IsValid(out var ex))
            {
                // notify message
                MessageReceived?.BeginInvoke(subscription, name, null, null);

                // if client is registered
                if (!ValidateClients || _marsClients.ContainsKey(name))
                {
                    // if subscription has any values
                    if (subscription.SubscriptionType != null && subscription.SubscriptionType.Length > 0)
                    {
                        _marsClients[name].LastConnectionTime = DateTime.Now;
                        _marsClients[name].SubscriptionTypes = subscription.SubscriptionType;
                        SendFullDeviceStatusReport(name);
                    }
                    else
                    {
                        // unsubscribe
                        _marsClients.Remove(name);
                    }
                }
            }
            else
            {
                ValidationErrorOccured?.BeginInvoke(this, new InvalidMessageException(subscription, ex), null, null);
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _timerTimeStamp = _timerTimeStamp.Add(TimeSpan.FromMilliseconds(_timer.Interval));

            if (CanTimeout)
            {
                List<string> namesToRemove = new List<string>();

                foreach (var client in _marsClients)
                {
                    // if mars timeout occured
                    if (DateTime.Now - client.Value.LastConnectionTime >= ConnectionTimeout)
                    {
                        // unsubscribe this mars
                        namesToRemove.Add(client.Key);
                    }
                }

                // remove here to avoid collection modified exception
                namesToRemove.ForEach(x => _marsClients.Remove(x));
            }

            if (AutoStatusReports)
            {
                SendEmptyDeviceStatusReport();
            }

            if (Math.Abs(_timerTimeStamp.TotalSeconds % FullStatusInterval.TotalSeconds) < 0.5)
            {
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

            lock (_syncToken)
            {
                if (_pendingIndications.Count > 0)
                {
                    foreach (var marsClient in _marsClients)
                    {
                        if (marsClient.Value.SubscriptionTypes != null &&
                            marsClient.Value.SubscriptionTypes.Contains(SubscriptionTypeType.OperationalIndication))
                        {
#pragma warning disable 4014
                            _socket.SendAsync(marsClient.Value.IPPort, CreateIndicationReport(_pendingIndications).ToXml());
#pragma warning restore 4014
                        }
                    }
                    _pendingIndications.Clear();
                }
            }
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

        private DeviceIndicationReport CreateIndicationReport(IEnumerable<IndicationType> indicationTypes)
        {
            var sensorId = Configuration.SensorConfiguration != null && Configuration.SensorConfiguration.Length > 0
                ? Configuration.SensorConfiguration[0].SensorIdentification
                : new SensorIdentificationType
                {
                    SensorName = "MrsSensor",
                    SensorType = SensorTypeType.Undefined
                };
            return new DeviceIndicationReport
            {
                DeviceIdentification = Configuration.DeviceIdentification,
                ProtocolVersion = Configuration.ProtocolVersion,
                Items = new object[]
                {
                    new SensorIndicationReport
                    {
                        SensorIdentification = sensorId,
                        IndicationType = indicationTypes.ToArray()
                    }
                }
            };
        }

        private void Socket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            if (e.Data == null || e.Data.Length == 0)
            {
                Console.WriteLine("Empty message");
                return;
            }

            var message = Encoding.UTF8.GetString(e.Data);

            var type = ExtensionMethods.GetXmlType(message);
            if (type.HasValue)
            {
                switch (type)
                {
                    case MrsMessageTypes.DeviceConfiguration:
                        HandleConfigRequest(e.IpPort,
                            ExtensionMethods.XmlConvert<DeviceConfiguration>(message));
                        break;
                    case MrsMessageTypes.DeviceSubscriptionConfiguration:
                        HandleSubscriptionRequest(ExtensionMethods.XmlConvert<DeviceSubscriptionConfiguration>(message));
                        break;
                    case MrsMessageTypes.CommandMessage:
                        HandleCommandMessage(ExtensionMethods.XmlConvert<CommandMessage>(message));
                        break;
                }
            }
            else
            {
                Console.WriteLine("Unknown Message Type");
            }
        }

        #endregion


        #region / / / / /  Public methods  / / / / /

        public Sensor(DeviceConfiguration configuration, DeviceStatusReport status)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            StatusReport = status ?? throw new ArgumentNullException(nameof(status));

            _marsClients = new Dictionary<string, MrsClient>();
            _pendingIndications = new List<IndicationType>(400);

            //_socket.NewMessageReceived += WebSocketMessageHandler;
            //_socket.Setup(IP, Port);

            _timer = new Timer();
            _timer.Elapsed += Timer_Elapsed;
        }

        public void Start()
        {
            if (IsActive)
            {
                Stop();
            }

            _socket = new WatsonWsServer(IP, Port, false);
            _socket.MessageReceived += Socket_MessageReceived;
            _socket.Start();

            _timer.Interval = KeepAliveInterval.TotalMilliseconds;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            _socket?.Dispose();
            _socket = null;
        }

        public void SendFullDeviceStatusReport(string clientName)
        {
            if (_marsClients.ContainsKey(clientName))
            {
                if (!ValidateMessages || StatusReport.IsValid(out var exception))
                {
                    _socket.SendAsync(_marsClients[clientName].IPPort, StatusReport.ToXml());
                    MessageSent?.BeginInvoke(StatusReport, clientName, null, null);
                }
                else
                {
                    ValidationErrorOccured?.BeginInvoke(this, new InvalidMessageException(StatusReport, exception), null, null);
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
        /// <param name="clientName">mars name</param>
        public void SendEmptyDeviceStatusReport(string clientName)
        {
            if (_marsClients.ContainsKey(clientName))
            {
                var emptyStatus = GetEmptyStatus(StatusReport);
                if (!ValidateMessages || emptyStatus.IsValid(out var exception))
                {
                    _socket.SendAsync(_marsClients[clientName].IPPort, emptyStatus.ToXml());
                    MessageSent?.BeginInvoke(emptyStatus, clientName, null, null);
                }
                else
                {
                    ValidationErrorOccured?.BeginInvoke(this, new InvalidMessageException(emptyStatus, exception), null, null);
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

        public void RegisterIndications(params IndicationType[] indications)
        {
            _pendingIndications.AddRange(indications);
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
        public event EventHandler<InvalidMessageException> ValidationErrorOccured;


        #endregion


        #region / / / / /  Nested classes  / / / / /

        private sealed class MrsClient
        {
            public SubscriptionTypeType[] SubscriptionTypes { get; set; }
            public DateTime LastConnectionTime { get; set; }
            public string IPPort { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// Event Handler for any mars message events
    /// </summary>
    /// <param name="message">the message object</param>
    /// <param name="marsName">Name of the mars client associated with the event</param>
    public delegate void MarsMessageEventHandler(MrsMessage message, string marsName);
}
