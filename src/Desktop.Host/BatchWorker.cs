using System;
using System.Threading;
using System.Threading.Tasks;
using AICad.Application;
using Microsoft.Extensions.Hosting;
internal sealed class BatchWorker(TaskOrchestrator batches):BackgroundService {
 protected override async Task ExecuteAsync(CancellationToken stoppingToken){using var timer=new PeriodicTimer(TimeSpan.FromSeconds(3));while(await timer.WaitForNextTickAsync(stoppingToken))try{await batches.Tick(stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}catch{ /* Persisted batches retain state; next tick retries only non-dispatched work. */ }}
}
