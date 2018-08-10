<h1>Modbus module configuration</h1>
<ul>
<li>Follow steps in <a href='http://search.abb.com/library/Download.aspx?DocumentID=3AUA0000093568&LanguageCode=en&DocumentPartId=1&Action=LaunchDirect'>documentation ABB FENA adapter</a> for ModBus TCP configuration</li>
<li>Follow steps in <a href='https://docs.microsoft.com/en-us/azure/iot-edge/deploy-modbus-gateway#run-the-solution'>this reference</a> for IoT Edge modbus module configuration</li>
<li>Set desired properties of modbus module to get all registers required for calculating values of your telemetry
	<pre>
	{
        "PublishInterval": "2000",
        "SlaveConfigs": {
          "acs580-01": {
            "SlaveConnection": "<FENA ehternet adapter IPV4 address>",
            "HwId": "ABB-ACS580-01-02A6-4",
            "Operations": {
              "Speed": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "400101",
                "Count": "1",
                "DisplayName": "Speed",
                "CorrelationId": "Speed"
              },
              "SpeedScale": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "404601",
                "Count": "1",
                "DisplayName": "SpeedScale",
                "CorrelationId": "Speed"
              },
              "Frequency": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "400106",
                "Count": "1",
                "DisplayName": "Frequency",
                "CorrelationId": "Frequency"
              },
              "Current": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "400108",
                "Count": "1",
                "DisplayName": "Current"
              },
              "Torque": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "400110",
                "Count": "1",
                "DisplayName": "Torque",
                "CorrelationId": "Torque"
              },
              "TorqueScale": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "404603",
                "Count": "1",
                "DisplayName": "TorqueScale",
                "CorrelationId": "Torque"
              },
              "DCvoltage": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "400111",
                "Count": "1",
                "DisplayName": "DCvoltage"
              },
              "OutputPower": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "400114",
                "Count": "1",
                "DisplayName": "OutputPower",
                "CorrelationId": "Power"
              },
              "InverterKWh": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "400120",
                "Count": "1",
                "DisplayName": "InverterKWh"
              },
              "StatusWord": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "400051",
                "Count": "1",
                "DisplayName": "StatusWord"
              },
              "FrequencyScale": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "404602",
                "Count": "1",
                "DisplayName": "FrequencyScale",
                "CorrelationId": "Frequency"
              },
              "PowerScale": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "404604",
                "Count": "1",
                "DisplayName": "PowerScale",
                "CorrelationId": "Power"
              },
              "CurrentScale": {
                "PollingInterval": "1000",
                "UnitId": "1",
                "StartAddress": "404605",
                "Count": "1",
                "DisplayName": "CurrentScale"
              }
            }
          }
        }
      }
	</pre>
	<li>You can add other drive or motor paramters. Please reference your <a href='http://search.abb.com/library/ABBLibrary.asp?DocumentID=9AKK105713A8085&DocumentPartId=1&Action=LaunchDirect'>ABB ACS Firmware manual</a></li>
	<li>Please make sure you set the same <i>CorrelationId</i> for all signals required to calculate actual parameters (For example "Speed" and "SpeedScale" should have the same "CorrelationId")
	<li>Click "Save" and add abbDriveProfileModule to continue</br>
		<img src='https://github.com/MaxKhlupnov/SmartHive.AbbEdge/blob/master/Docs/Images/ModbusModuleDeployment.JPG?raw=true'>
	</li>
</li>
</ul>