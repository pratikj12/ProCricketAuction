using System.Data.Common;

namespace ProCricketAuction.Api.Services;

public sealed class AuctionDeadlineWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AuctionDeadlineWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var auctionService = scope.ServiceProvider.GetRequiredService<IAuctionService>();
                await auctionService.ExpireDueAuctionsAsync(stoppingToken);
            }
            catch (DbException exception)
            {
                logger.LogError(exception, "Auction deadline scan failed; it will retry on the next interval.");
            }
            catch (InvalidOperationException exception)
            {
                logger.LogError(exception, "Auction deadline processing failed; it will retry on the next interval.");
            }
        }
    }
}
