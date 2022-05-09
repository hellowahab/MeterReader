from google.protobuf.timestamp_pb2 import Timestamp
import grpc

from meterservice_pb2 import ReadingMessage, ReadingPacket, ReadingStatus
from meterservice_pb2_grpc import MeterReaderServiceStub

def main():
    print("Starting Client Call")

    packet = ReadingPacket(Successful = ReadingStatus.Success)

    now = Timestamp()
    now.GetCurrentTime()

    reading = ReadingMessage(customerId = 1, ReadingTime = now, ReadingValue = 10000)
    packet.Readings.append(reading)

    channel = grpc.insecure_channel("localhost:8888")
    stub = MeterReaderServiceStub(channel)

    reponse = stub.AddReading(packet)
    if(response.Status == ReadingStatus.Success):
        print("Succeeded")  
    else:
        print("Failure")

main()