using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MrsSensor.Core.Extensions;
using SensorStandard;
using SensorStandard.MrsTypes;
using SuperSocket;
using SuperSocket.WebSocket;
using SuperSocket.WebSocket.Server;

namespace MrsSensor.Core
{
    public class Sensor
    {
        #region / / / / /  Private fields  / / / / /

        private readonly IHost _socketHost;
        private readonly Dictionary<string, MrsClient> _marsClients;
        private readonly Timer _timer;
        private TimeSpan _timerTimeStamp;
        private readonly object _syncToken = new object();
        private readonly List<IndicationType> _pendingIndications;

        #endregion


        #region / / / / /  Properties  / / / / /

        public string IP => Configuration?.NotificationServiceIPAddress;

        public int Port => int.Parse(Configuration.NotificationServicePort);

        public bool IsActive => _socketHost.AsServer().State == ServerState.Started;

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

        private Task HandleConfigRequest(WebSocketSession session, DeviceConfiguration configuration)
        {
            string name = configuration.RequestorIdentification;

            // if message is invalid - throw
            if (!ValidateMessages || configuration.IsValid(out var ex))
            {
                // notify message
                MessageReceived?.Invoke(configuration, name);

                if (_marsClients.ContainsKey(name) == false)
                {
                    _marsClients.Add(name, new MrsClient
                    {
                        SocketSession = session,
                        LastConnectionTime = DateTime.Now,
                        IP = (session.RemoteEndPoint as IPEndPoint)?.Address.ToString()
                    });
                }
                else
                {
                    // if client is already registered - update values
                    _marsClients[name].SocketSession.CloseAsync();
                    _marsClients[name].SocketSession = session;
                    _marsClients[name].LastConnectionTime = DateTime.Now;
                    _marsClients[name].IP = (session.RemoteEndPoint as IPEndPoint)?.Address.ToString();
                }
                return session.SendAsync(Configuration.ToXml()).AsTask(); 
            }

            ValidationErrorOccured?.Invoke(this, new InvalidMessageException(configuration, ex));
            return Task.CompletedTask;
        }

        private Task HandleCommandMessage(CommandMessage commandMessage)
        {
            string name = commandMessage.RequestorIdentification;

            // if message is invalid - throw
            if (!ValidateMessages || commandMessage.IsValid(out var ex))
            {
                // notify message
                MessageReceived?.Invoke(commandMessage, name);

                // if client is registered
                if (!ValidateClients || _marsClients.ContainsKey(name))
                {
                    _marsClients[name].LastConnectionTime = DateTime.Now;
                    return SendEmptyDeviceStatusReport(name);
                } 
                return Task.CompletedTask;
            }

            ValidationErrorOccured?.Invoke(this, new InvalidMessageException(commandMessage, ex));
            return Task.CompletedTask;
        }

        private Task HandleSubscriptionRequest(DeviceSubscriptionConfiguration subscription)
        {
            string name = subscription.RequestorIdentification;

            // if message is invalid - throw
            if (!ValidateMessages || subscription.IsValid(out var ex))
            {
                // notify message
                MessageReceived?.Invoke(subscription, name);

                // if client is registered
                if (!ValidateClients || _marsClients.ContainsKey(name))
                {
                    // if subscription has any values
                    if (subscription.SubscriptionType != null && subscription.SubscriptionType.Length > 0)
                    {
                        _marsClients[name].LastConnectionTime = DateTime.Now;
                        _marsClients[name].SubscriptionTypes = subscription.SubscriptionType;
                        return SendFullDeviceStatusReport(name);
                    }

                    // unsubscribe
                    _marsClients[name].SocketSession.CloseAsync();
                    _marsClients.Remove(name);
                }
                return Task.CompletedTask;
            }

            ValidationErrorOccured?.Invoke(this, new InvalidMessageException(subscription, ex));
            return Task.CompletedTask;
        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
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
                        await client.Value.SocketSession.CloseAsync();
                        namesToRemove.Add(client.Key);
                    }
                }

                // remove here to avoid collection modified exception
                namesToRemove.ForEach(x => _marsClients.Remove(x));
            }

            if (AutoStatusReports)
            {
                await SendEmptyDeviceStatusReport();
            }

            if (Math.Abs(_timerTimeStamp.TotalSeconds % FullStatusInterval.TotalSeconds) < 0.5)
            {
                foreach (var client in _marsClients)
                {
                    // if client has subscribed to status reports
                    if (client.Value.SubscriptionTypes != null &&
                        client.Value.SubscriptionTypes.Contains(SubscriptionTypeType.TechnicalStatus))
                    {
                        await SendFullDeviceStatusReport(client.Key);
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
                            marsClient.Value.SocketSession.SendAsync(CreateIndicationReport(_pendingIndications).ToXml());
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

        #endregion


        #region / / / / /  Public methods  / / / / /

        public Sensor(DeviceConfiguration configuration, DeviceStatusReport status)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            StatusReport = status ?? throw new ArgumentNullException(nameof(status));

            _marsClients = new Dictionary<string, MrsClient>();
            _pendingIndications = new List<IndicationType>(400);

            _socketHost = WebSocketHostBuilder.Create()
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "serverOptions:name", "MrsSensorServer" },
                        { "serverOptions:listeners:0:ip", $"{IP}" },
                        { "serverOptions:listeners:0:port", $"{Port}" }
                    });
                })
                .UseWebSocketMessageHandler(WebSocketMessageHandler)
                .Build();

            _timer = new Timer();
            _timer.Elapsed += Timer_Elapsed;
        }

        public void Start()
        {
            if (IsActive)
            {
                _socketHost.StopAsync();
            }
            _socketHost.RunAsync();

            _timer.Interval = KeepAliveInterval.TotalMilliseconds;
            _timer.Start();
        }

        private Task WebSocketMessageHandler(WebSocketSession session, WebSocketPackage package)
        {
            if (package.Message == null)
            {
                Console.WriteLine("Empty message");
                return Task.CompletedTask;
            }

            var type = ExtensionMethods.GetXmlType(package.Message);
            if (type.HasValue)
            {
                switch (type)
                {
                    case MrsMessageTypes.DeviceConfiguration:
                        return HandleConfigRequest(session,
                            ExtensionMethods.XmlConvert<DeviceConfiguration>(package.Message));
                    case MrsMessageTypes.DeviceSubscriptionConfiguration:
                        return HandleSubscriptionRequest(ExtensionMethods.XmlConvert<DeviceSubscriptionConfiguration>(package.Message));
                    case MrsMessageTypes.CommandMessage:
                        return HandleCommandMessage(ExtensionMethods.XmlConvert<CommandMessage>(package.Message));
                }
            }
            else
            {
                Console.WriteLine("Unknown Message Type");
            }

            return Task.CompletedTask;
        }

        public void Stop()
        {
            _timer.Stop();
            _socketHost.StopAsync();
        }

        public async Task SendFullDeviceStatusReport(string clientName)
        {
            if (_marsClients.ContainsKey(clientName))
            {
                if (!ValidateMessages || StatusReport.IsValid(out var exception))
                {
                    await _marsClients[clientName].SocketSession.SendAsync(StatusReport.ToXml());
                    MessageSent?.Invoke(StatusReport, clientName);
                }
                else
                {
                    ValidationErrorOccured?.Invoke(this, new InvalidMessageException(StatusReport, exception));
                }
            }
        }

        /// <summary>
        /// Send a full <see cref="DeviceStatusReport"/> message to every mars client subscribed
        /// </summary>
        public async Task SendFullDeviceStatusReport()
        {
            foreach (var client in _marsClients)
            {
                await SendFullDeviceStatusReport(client.Key);
            }
        }

        /// <summary>
        /// Send an empty <see cref="DeviceStatusReport"/> message to a specific mars client
        /// </summary>
        /// <param name="clientName">mars name</param>
        public async Task SendEmptyDeviceStatusReport(string clientName)
        {
            if (_marsClients.ContainsKey(clientName))
            {
                var emptyStatus = GetEmptyStatus(StatusReport);
                if (!ValidateMessages || emptyStatus.IsValid(out var exception))
                {
                    await _marsClients[clientName].SocketSession.SendAsync(emptyStatus.ToXml());
                    MessageSent?.Invoke(emptyStatus, clientName);
                }
                else
                {
                    ValidationErrorOccured?.Invoke(this, new InvalidMessageException(emptyStatus, exception));
                }
            }
        }

        /// <summary>
        /// Send an empty <see cref="DeviceStatusReport"/> message to every mars client subscribed
        /// </summary>
        public async Task SendEmptyDeviceStatusReport()
        {
            foreach (var client in _marsClients)
            {
                await SendEmptyDeviceStatusReport(client.Key);
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
            public WebSocketSession SocketSession { get; set; }
            public SubscriptionTypeType[] SubscriptionTypes { get; set; }
            public DateTime LastConnectionTime { get; set; }
            public string IP { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// Event Handler for any mars message events
    /// </summary>
    /// <param name="messageType">Message type</param>
    /// <param name="message">the message object</param>
    /// <param name="marsName">Name of the mars client associated with the event</param>
    public delegate void MarsMessageEventHandler(MrsMessage message, string marsName);
}
