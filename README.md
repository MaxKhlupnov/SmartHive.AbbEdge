# SmartHive.AbbEdge is an IoT Edge device gateway, remote monitoring and predictive maintenance modules for ABB ACS drive managed electric motors 
<h1>Architecture pieces</h1>
<ul>
<li><a href='https://new.abb.com/drives/low-voltage-ac/industrial-drives/industrial-acs800-series'>ACS800 industrial drives solution</a></li>
<li><a href='https://github.com/Azure/azure-iot-remote-monitoring'>Azure IoT Remote Monitoring preconfigured solution</a></li>
<li><a href="https://github.com/Azure/iotedge">Microsoft IoT Edge platform</a> to build IoT Gateway and run modules.</li>
<li><a href="https://github.com/Azure/iot-edge-modbus">Modbus protocol module</a> for use with the Azure IoT Edge.</li>
<li><a href="https://github.com/MaxKhlupnov/SmartHive.AbbEdge/tree/master/AbbEdge/modules/abbDriveProfile">Abb drive profile module</a> for translation Modbus register values into electric drive physical parameters</li>
<li><a href="https://github.com/MaxKhlupnov/SmartHive.AbbEdge/tree/master/AbbEdge/modules/abbRemoteMonitoringGateway">Remote monitoring gateway module</a> for sending telemetry to the cloud</li>
</ul>
<h1>How to deploy</h1>
<li>Install ModBus TCP support with <a href='https://new.abb.com/drives/connectivity/fieldbus-connectivity/modbus-tcp/fena-11'>FENA module</a> to collect ABB drive telemetry via Modbus TCP to IoT Edge gateway over Ethernet network</li>
