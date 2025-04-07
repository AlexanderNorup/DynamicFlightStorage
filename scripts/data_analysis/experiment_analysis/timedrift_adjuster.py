
from datetime import datetime, timedelta
class TimedriftAdjuster:
    def __init__(self, base_time: datetime, unadjusted_time: datetime, known_latency: float):
        expected_time_at_client = base_time + timedelta(milliseconds=known_latency)
        self.time_drift = unadjusted_time - expected_time_at_client

    def get_adjusted_lag(self, input: float):
        return input - abs(self.time_drift.total_seconds() * 1000.0)
    
    def get_adjusted_time(self, input: datetime):
        return input - abs(self.time_drift)
    
if __name__ == "__main__":
    baseTime = datetime.fromisoformat("2025-04-07T07:26:45.0029431Z")
    consumerTime = datetime.fromisoformat("2025-04-07T07:33:19.0743406Z")

    latency = 27.721899999
    adjuster = TimedriftAdjuster(baseTime, consumerTime, latency)
    print(f"Detected lag of {adjuster.time_drift.total_seconds():.2f} seconds")

    lag = 394076.5138
    adjustedLag = adjuster.get_adjusted_lag(lag)
    print(f"Lag without adjustment: {lag:.2f} ms.")
    print(f"Lag with adjustment: {adjustedLag:.2f} ms.")

    adjustedConsumerTime = adjuster.get_adjusted_time(consumerTime)
    print(f"Regular consumer-time: {consumerTime}")
    print(f"Adjusted consumer-time: {adjustedConsumerTime}")