#
# Score drive telemetry

import json
import numpy
import pickle
import pandas as pd
from sklearn.externals import joblib
from azureml.core.model import Model

def scoreTelemetry(telemetryMessageText):
    try:
        data = json.loads(telemetryMessageText)
        data = pd.DataFrame(data)
        testdata = [1304.85, 16.73, 44.6575, 0.732, 1.8, 0.9]
        model_path = Model.get_model_path('anomily_detect.pkl')
        model = joblib.load(model_path)
        result = model.predict(testdata)
    except Exception as e:
        result = str(e)
    if isinstance(result, list):
        result = json.dumps({"result": result.tolist()})
    return result