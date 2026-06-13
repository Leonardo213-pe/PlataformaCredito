using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using PlataformaCredito.Models;

namespace PlataformaCredito.Services;

public class SolicitudCacheService
{
    private readonly IDistributedCache _cache;
    private const int CacheSegundos = 60;

    public SolicitudCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    private static string CacheKey(string userId) => $"solicitudes:{userId}";

    public async Task<List<SolicitudCredito>?> ObtenerAsync(string userId)
    {
        var json = await _cache.GetStringAsync(CacheKey(userId));
        if (json == null) return null;
        return JsonSerializer.Deserialize<List<SolicitudCredito>>(json);
    }

    public async Task GuardarAsync(string userId, List<SolicitudCredito> solicitudes)
    {
        var json = JsonSerializer.Serialize(solicitudes);
        await _cache.SetStringAsync(CacheKey(userId), json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheSegundos)
        });
    }

    public async Task InvalidarAsync(string userId)
    {
        await _cache.RemoveAsync(CacheKey(userId));
    }
}
