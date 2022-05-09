using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MeterReader.gRPC;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using static MeterReader.gRPC.MeterReaderService;

namespace MeterReader.Services;

// [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Authorize(AuthenticationSchemes = CertificateAuthenticationDefaults.AuthenticationScheme)]
public class MeterReadingService : MeterReaderServiceBase
{
    private readonly IReadingRepository _repository;
    private readonly ILogger<MeterReadingService> _logger;
    private readonly JwtTokenValidationService _tokenService;

    public MeterReadingService(IReadingRepository repository,           ILogger<MeterReadingService> logger,
        JwtTokenValidationService tokenService)
    {
        _repository = repository;
        _logger = logger;
        _tokenService = tokenService;
    }

    [AllowAnonymous]
    public async override Task<TokenResponse> GenerateToken(TokenRequest request, ServerCallContext context)
    { 
        var creds = new CredentialModel()
        {
            UserName = request.Username,
            Passcode = request.Password,
        };

        var result = await _tokenService.GenerateTokenModelAsync(creds);

        if (result.Success)
        {
            return new TokenResponse()
            {
                Success = true,
                Token = result.Token,
                Expiration = Timestamp.FromDateTime(result.Expiration)
            };
        }
        else
        {
            return new TokenResponse();
        }
 
    }

    public override async Task<StatusMessage> AddReading(ReadingPacket request, ServerCallContext context)
    {
        if (request.Successful == ReadingStatus.Success)
        {
            foreach (var reading in request.Readings)
            {
                var readingValue = new MeterReading()
                {
                    CustomerId = reading.CustomerId,
                    Value = reading.ReadingValue,
                    ReadingDate = reading.ReadingTime.ToDateTime()
                };
                _logger.LogInformation($"Adding {reading.ReadingValue}");
                _repository.AddEntity(readingValue);
            }
            if (await _repository.SaveAllAsync())
            {
                _logger.LogInformation("Successfully Saved new Readings..");
                return new StatusMessage()
                {
                    //Notes = "Successfully added to the database.",
                    Status = ReadingStatus.Success
                };
            }
        }
        _logger.LogError("Failed to Save new readings");
        return new StatusMessage()
        {
            //Notes = "Failed to store readings in Database",
            Status = ReadingStatus.Success
        };

    }

    public async override Task AddReadingStream(IAsyncStreamReader<ReadingMessage> requestStream,   IServerStreamWriter<ErrorMessage> responseStream,
        ServerCallContext context)
    {
        while (await requestStream.MoveNext())
        { 
            var msg = requestStream.Current;

            if (msg.ReadingValue < 500)
            {
                await responseStream.WriteAsync(new ErrorMessage() 
                { 
                    Message = $"Value less than 500. {msg.ReadingValue}" 
                });
            }

            var readingValue = new MeterReading()
            {
                CustomerId = msg.CustomerId,
                Value = msg.ReadingValue,
                ReadingDate = msg.ReadingTime.ToDateTime()
            };
            _logger.LogInformation($"Adding {msg.ReadingValue}");
            _repository.AddEntity(readingValue);

            await _repository.SaveAllAsync();
        }        
    }
}
