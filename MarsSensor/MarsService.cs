using System;
using SensorStandard;
using SensorStandard.MrsTypes;

namespace MarsSensor
{
    internal class MarsService : SNSR_STDSOAPPort
    {
        private readonly Sensor _sensor = Sensor.Instance;
        private Action _configAction;
        private Action _subscriptionAction;
        private Action _commnadMessageAction;

        public IAsyncResult BegindoCommandMessage(doCommandMessageRequest request, AsyncCallback callback, object asyncState)
        {
            _commnadMessageAction = () =>
            {
                _sensor.HandleCommandMessageRequest(request.CommandMessage);
            };
            return _commnadMessageAction.BeginInvoke(null, null);
        }

        public IAsyncResult BegindoDeviceConfiguration(doDeviceConfigurationRequest request, AsyncCallback callback, object asyncState)
        {
            _configAction = () =>
            {
                _sensor.HandleConfigRequest(request.DeviceConfiguration);
            };
            return _configAction.BeginInvoke(null, null);
        }

        public IAsyncResult BegindoDeviceIndicationReport(doDeviceIndicationReportRequest request, AsyncCallback callback, object asyncState)
        {
            // this is for mars, not sensors
            return null;
        }

        public IAsyncResult BegindoDeviceStatusReport(doDeviceStatusReportRequest request, AsyncCallback callback, object asyncState)
        {
            // this is for mars, not sensors
            return null;
        }

        public IAsyncResult BegindoDeviceSubscriptionConfiguration(doDeviceSubscriptionConfigurationRequest request, AsyncCallback callback, object asyncState)
        {
            _subscriptionAction = () =>
            {
                _sensor.HandleSubscriptionRequest(request.DeviceSubscriptionConfiguration);
            };
            return _subscriptionAction.BeginInvoke(null, null);
        }

        public doCommandMessageResponse doCommandMessage(doCommandMessageRequest request)
        {
            _sensor.HandleCommandMessageRequest(request.CommandMessage);
            return new doCommandMessageResponse();
        }

        public doDeviceConfigurationResponse doDeviceConfiguration(doDeviceConfigurationRequest request)
        {
            _sensor.HandleConfigRequest(request.DeviceConfiguration);
            return new doDeviceConfigurationResponse();
        }

        public doCommandMessageResponse doDeviceIndicationReport(doDeviceIndicationReportRequest request)
        {
            // this is for mars, not sensors
            return new doCommandMessageResponse();
        }

        public doCommandMessageResponse doDeviceStatusReport(doDeviceStatusReportRequest request)
        {
            // this is for mars, not sensors
            return new doCommandMessageResponse();
        }

        public doDeviceSubscriptionConfigurationResponse doDeviceSubscriptionConfiguration(doDeviceSubscriptionConfigurationRequest request)
        {
            _sensor.HandleSubscriptionRequest(request.DeviceSubscriptionConfiguration);
            return new doDeviceSubscriptionConfigurationResponse();
        }

        public doCommandMessageResponse EnddoCommandMessage(IAsyncResult result)
        {
            _commnadMessageAction.EndInvoke(result);
            return new doCommandMessageResponse();
        }

        public doDeviceConfigurationResponse EnddoDeviceConfiguration(IAsyncResult result)
        {
            _configAction.EndInvoke(result);
            return new doDeviceConfigurationResponse();
        }

        public doCommandMessageResponse EnddoDeviceIndicationReport(IAsyncResult result)
        {
            // this is for mars, not sensors
            return new doCommandMessageResponse();
        }

        public doCommandMessageResponse EnddoDeviceStatusReport(IAsyncResult result)
        {
            // this is for mars, not sensors
            return new doCommandMessageResponse();
        }

        public doDeviceSubscriptionConfigurationResponse EnddoDeviceSubscriptionConfiguration(IAsyncResult result)
        {
            _subscriptionAction.EndInvoke(result);
            return new doDeviceSubscriptionConfigurationResponse();
        }
    }
}
