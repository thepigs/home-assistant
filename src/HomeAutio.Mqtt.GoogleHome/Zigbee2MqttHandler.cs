using System;
using System.Collections.Generic;
using HomeAutio.Mqtt.GoogleHome.Models.Request;
    
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using Device = HomeAutio.Mqtt.GoogleHome.Models.State.Device;
using HomeAutio.Mqtt.Core;

namespace HomeAutio.Mqtt.GoogleHome
{
    public class MqttHandler
    {
        private Device device;
        private IManagedMqttClient mqttClient;

        public MqttHandler(IManagedMqttClient mqttClient, Device device)
        {
            this.device = device;
            this.mqttClient = mqttClient;
        }

        public async void sendMessage(string payload)
        {
            Console.WriteLine(payload);

            await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic("zigbee2mqtt/" + device.FriendlyName + "/set")
                    .WithPayload(payload)
                    .WithAtLeastOnceQoS()
                    .Build())
                .ConfigureAwait(false);
        }

        public void Handle(Execution execution)
        {
            Console.WriteLine(execution);
            switch (execution.Command)
            {
                case "action.devices.commands.OnOff":
                    var on = (bool)execution.Params["on"];
                    sendMessage($"{{\"state\":\"{(on?"ON":"OFF")}\" }}");
                    break;
                case "action.devices.commands.BrightnessAbsolute":
                    var brightness = (long) execution.Params["brightness"];
                    var b = (int) (brightness / 100.0 * 255);
                    sendMessage($"{{\"brightness\": {b} }}");
                    break;
                case "action.devices.commands.ColorAbsolute":
                    var color = (long)((IDictionary<string,object>)execution.Params["color"])["spectrumRGB"];
                    sendMessage($"{{\"color\": {{ \"hex\": \"#{color.ToString("X6")}\" }} }}");
                    break;
            }
        }
    }
}