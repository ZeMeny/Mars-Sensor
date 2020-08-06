using System;
using MrsSensor.Core;
using SensorStandard;
using SensorStandard.MrsTypes;

namespace TestSensor.Core
{
    class Program
    {
        static void Main(string[] args)
        {
            var devieId = new DeviceIdentificationType
            {
                DeviceName = "Device",
                DeviceType = DeviceTypeType.AcousticDetectionSystem
            };
            var sensorId = new SensorIdentificationType
            {
                SensorName = "Sensor",
                SensorType = SensorTypeType.Acoustic
            };
            DeviceConfiguration configuration = new DeviceConfiguration
            {
                NotificationServiceIPAddress = "127.0.0.1",
                NotificationServicePort = "13001",
                DeviceIdentification = devieId,
                SensorConfiguration = new[]
                {
                    new SensorConfiguration
                    {
                        SensorIdentification = sensorId
                    }
                }
            };
            DeviceStatusReport status = new DeviceStatusReport
            {
                DeviceIdentification = configuration.DeviceIdentification,
                Items = new object[]
                {
                    new DetailedSensorBITType
                    {
                        SensorIdentification = sensorId,
                        FaultCode = new string[0]
                    }, 
                    new SensorStatusReport
                    {
                        SensorIdentification = sensorId,
                        CommunicationState = BITResultType.OK,
                        PowerState = StatusType.Yes,
                        SensorTechnicalState = BITResultType.OK,
                        SensorMode = SensorModeType.Normal
                    }
                }
            };

            Sensor sensor = new Sensor(configuration, status);
            sensor.MessageReceived += Sensor_MessageReceived;
            sensor.MessageSent += Sensor_MessageSent;
            sensor.ValidationErrorOccured += Sensor_ValidationErrorOccured;

            Console.WriteLine("Starting Sensor...");
            sensor.Start();
            Console.WriteLine($"Sensor Started on {sensor.IP}:{sensor.Port}");

            int i = 0;
            while (Console.ReadKey(true).Key != ConsoleKey.Escape)
            {
                sensor.RegisterIndications(CreateIndocation(++i, 32.5, 34.5));
            }
            Console.ReadKey(true);

            sensor.Stop();
        }

        private static void Sensor_ValidationErrorOccured(object sender, InvalidMessageException e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Validation Error on {e.MarsMessage.MrsMessageType}! \n{e}");
            Console.ResetColor();
        }

        private static void Sensor_MessageSent(MrsMessage message, string marsName)
        {
            Console.WriteLine($"{message.MrsMessageType} sent to {marsName}");
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

        private static IndicationType CreateIndocation(long id, double lat, double lon)
        {
            return new IndicationType
            {
                ID = id.ToString(),
                CreationTime = new TimeType
                {
                    Zone = TimezoneType.GMT,
                    Value = DateTime.Now
                },
                Item = new AcousticDetectionType
                {
                    Location = new Point
                    {
                        Item = new LocationType
                        {
                            Item = new GeodeticLocation
                            {
                                Latitude = new Latitude
                                {
                                    Units = LatLonUnitsType.DecimalDegrees,
                                    Value = lat
                                },
                                Longitude = new Longitude
                                {
                                    Units = LatLonUnitsType.DecimalDegrees,
                                    Value = lon
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
