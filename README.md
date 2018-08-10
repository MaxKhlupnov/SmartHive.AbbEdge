# SmartHive.AbbEdge is an IoT Edge device gateway, remote monitoring and predictive maintenance modules for ABB ACS drive managed electric motors 
<a href='https://youtu.be/Se2Y26gVw-8'>Video</a>
<h1>Architecture pieces</h1>
<ul>
<li><a href='https://new.abb.com/drives/low-voltage-ac/industrial-drives/industrial-acs800-series'>ACS800 industrial drives solution</a></li>
<li><a href='https://github.com/Azure/azure-iot-remote-monitoring'>Azure IoT Remote Monitoring preconfigured solution</a></li>
<li><a href="https://github.com/Azure/iotedge">Microsoft IoT Edge platform</a> to build IoT Gateway and run modules.</li>
<li><a href="https://github.com/Azure/iot-edge-modbus">Modbus protocol module</a> for use with the Azure IoT Edge.</li>
<li><a href="https://github.com/MaxKhlupnov/SmartHive.AbbEdge/tree/master/AbbEdge/modules/abbDriveProfile">Abb drive profile module</a> for translation Modbus register values into electric drive physical parameters</li>
<li><a href="https://github.com/MaxKhlupnov/SmartHive.AbbEdge/tree/master/AbbEdge/modules/abbRemoteMonitoringGateway">Remote monitoring gateway module</a> for sending telemetry to the cloud</li>
</ul>
<h3>Architecture diagram</h3>
<img src="https://github.com/MaxKhlupnov/SmartHive.AbbEdge/blob/master/Docs/Images/Architecture.jpg?raw=true"/>
<h1>How to deploy</h1>
<ul>
<li>Install <a href='http://search.abb.com/library/Download.aspx?DocumentID=3AUA0000093568&LanguageCode=en&DocumentPartId=1&Action=LaunchDirect'>'>FENA module</a> and configure ModBus TCP support to collect ABB drive telemetry from IoT Edge gateway over Ethernet network</li>
<li>Deploy <a href='https://docs.microsoft.com/en-us/azure/iot-accelerators/iot-accelerators-remote-monitoring-sample-walkthrough'>Azure remote monitoring solution accelerator</a> into your Azure subscription via www.azureiotsuite.com or manually</li>
<li>Find resource group with deployed Remote monitoring solution in your Azure subscription and open IoT Hub Azure Service instance. Use it in future steps.</li>
<li><a href='https://docs.microsoft.com/en-us/azure/iot-edge/'>Deploy Azure IoT Edge runtime</a> on some IoT Gateway devices. Connect this deices into the same Ethernet network as ACS Drive FENA adapter. Assign correct IP address and make sure your IoT Gatewy devices can ping ABB FENA adpater.</li>
  <li>Deploy and configure IoT Edge modules
   <ol>
		<li>On the <a href="https://portal.azure.com/">Azure portal</a>, go to your IoT hub.</li>
		<li>Go to <strong>IoT Edge</strong> and click on your IoT Edge device.</li>
		<li>Select <strong>Set modules</strong>.</li>
		<li>Click <strong>Add</strong> and select <strong>IoT Edge module</strong>.</li>
		<li>Add <strong>modbus module</strong> as described <a href="https://github.com/MaxKhlupnov/SmartHive.AbbEdge/blob/master/Docs/Modbus-module-configuration.md">here</a></li>
		<li>Add <strong>abbDriveProfile module</strong> as described<a href="https://github.com/MaxKhlupnov/SmartHive.AbbEdge/blob/master/Docs/abbDriveProfile-module-configuration.md">here</a></li>		
		<li>Add <strong>abbRemoteMonitoringGateway module</strong> as described<a href="https://github.com/MaxKhlupnov/SmartHive.AbbEdge/blob/master/Docs/abbRemoteMonitoringGateway.md">here</a></li>
		<li>Select <strong>Next</strong>.</li>
		<li><p>In the <strong>Specify Routes</strong> step, copy the following JSON into the text box. </p>
		<pre>
"routes": {
	"modbusToAbbAcsEdgeProfile": "FROM /messages/modules/modbus/outputs/modbusOutput INTO BrokeredEndpoint(\"/modules/abbDriveProfile/inputs/driveProfileInput\")",
	"abbDriveProfileToRemoteMonitoringGateway": "FROM /messages/modules/abbDriveProfile/outputs/driveProfileOutput INTO BrokeredEndpoint(\"/modules/abbRemoteMonitoringGateway/inputs/gatewayInput\")",          
	"abbRemoteMonitoringGatewayToIoTHub": "FROM /messages/modules/abbRemoteMonitoringGateway/outputs/* INTO $upstream"
}
		</pre>
		<ul>
			<li>Route "modbusToAbbAcsEdgeProfile" sends all messages collected by the Modbus module to abbDriveProfile module for actual  parameters calculation. 
			In this route, ''modbusOutput'' is the endpoint that Modbus module use to output data, and driveProfileInput is the endpoint that abbDriveProfile use for reading data
			and calculate actual parameter values based on modbus register values</li>
			<li>Route "abbDriveProfileToRemoteMonitoringGateway" sends all actual parameters calculated by abbDriveProfile to abbRemoteMonitoringGateway for transforming data into format required for Azure remote monitoring solution accelerator.
			In this route, ''driveProfileOutput'' is the endpoint that abbDriveProfile module use to output data, and gatewayInput is the endpoint that abbRemoteMonitoringGateway use for reading data
			and tarasofrm data to Azure remote monitoring solution telemetry format.</li>
			<li>Route ''abbRemoteMonitoringGatewayToIoTHub''ends all telemetry messages formatted by "abbRemoteMonitoringGateway" module to output data, and ''upstream'' is a 
				special destination that tells Edge Hub to send messages to IoT Hub. </p> 
			</li>			
			<li><p>Select <strong>Next</strong>.</li>
			<li>In the <strong>Review Deployment</strong> step, select <strong>Submit</strong>. </li>
			<li>Return to the device details page and select <strong>Refresh</strong>. You should see the new <strong>modbus</strong> module running along with the IoT Edge runtime.</li>
			<img src="https://github.com/MaxKhlupnov/SmartHive.AbbEdge/blob/master/Docs/Images/EdgeModulesPicture.PNG?raw=true"/>
		</ul> 
	</ol>
	</li>
	<li>Use IoT Hub Device Explorer to trace messages from ACS drive to IoT Hub<br/>
	<img src='https://github.com/MaxKhlupnov/SmartHive.AbbEdge/blob/master/Docs/Images/DeviceExplorer.png?raw=true'/></li>
	<li>Check your Azure remote monitoring solution web portal graphs for telemetry<br>
	<img src="https://github.com/MaxKhlupnov/SmartHive.AbbEdge/blob/master/Docs/Images/RemoteMonitoring.jpg?raw=true">
	</li>
</ul>
