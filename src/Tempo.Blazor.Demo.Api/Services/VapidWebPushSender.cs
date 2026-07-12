using System.Text.Json;
using Microsoft.Extensions.Options;
using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Shared;
using WebPush;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// <see cref="IWebPushSender"/> that encrypts and delivers payloads to browser push services
/// using VAPID (via the WebPush library). A 404/410 from the push service surfaces as an expired
/// result so the caller can prune the subscription.
/// </summary>
public sealed class VapidWebPushSender : IWebPushSender
{
    private readonly WebPushClient _client = new();
    private readonly VapidDetails? _vapid;

    public VapidWebPushSender(IOptions<WebPushOptions> options)
    {
        var o = options.Value;
        if (o.IsConfigured)
        {
            _vapid = new VapidDetails(o.Subject, o.PublicKey, o.PrivateKey);
        }
    }

    public async Task<TmWebPushResult> SendAsync(
        TmPushSubscription subscription,
        TmWebPushPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (_vapid is null)
        {
            return TmWebPushResult.Failed(0, "VAPID keys are not configured.");
        }

        var pushSubscription = new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);
        var json = JsonSerializer.Serialize(new
        {
            title = payload.Title,
            body = payload.Body,
            url = payload.Url,
            icon = payload.Icon,
            badge = payload.Badge,
            tag = payload.Tag,
            notificationId = payload.NotificationId
        });

        try
        {
            await _client.SendNotificationAsync(pushSubscription, json, _vapid, cancellationToken).ConfigureAwait(false);
            return TmWebPushResult.Ok();
        }
        catch (WebPushException ex)
        {
            return TmWebPushResult.Failed((int)ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return TmWebPushResult.Failed(0, ex.Message);
        }
    }
}
