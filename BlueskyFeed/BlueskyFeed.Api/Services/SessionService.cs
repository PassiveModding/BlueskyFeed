using FishyFlip;
using FishyFlip.Models;
using Microsoft.Extensions.Options;

namespace BlueskyFeed.Api.Services;

public class SessionService : IDisposable, IService
{
    private readonly ILogger<SessionService> _logger;
    private readonly IOptions<AtProtoConfig> _config;
    private readonly ATProtocol _proto;
    private Session? _session;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SessionService(ILogger<SessionService> logger, IOptions<AtProtoConfig> config)
    {
        _logger = logger;
        _config = config;
        _proto = new ATProtocolBuilder()
            .WithLogger(logger)
            .EnableAutoRenewSession(true)
            .Build();
    }
    
    public ATProtocol GetProtocol()
    {
        return _proto;
    }
    
    public async Task<Session> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {        
            if (_session == null)
            {
                _session =  await _proto.AuthenticateWithPasswordAsync(
                    _config.Value.LoginIdentifier, 
                    _config.Value.LoginToken, cancellationToken);
                
                if (_session == null)
                {
                    throw new Exception("Failed to authenticate");
                }
                
                _logger.LogInformation("Did: {Did}, Doc: {Doc}", _session.Did, _session.DidDoc);
            }
        
            return _session;
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public void Dispose()
    {
        _proto.Dispose();
        _logger.LogInformation("Disposed");
    }
}