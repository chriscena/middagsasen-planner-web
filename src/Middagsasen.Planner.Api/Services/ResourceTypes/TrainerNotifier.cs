using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    /// <summary>Resultatet av et varsel til trenerne.</summary>
    /// <param name="Success">
    /// <c>true</c> hvis alle SMS-ene ble sendt, eller det ikke var noen trenere å varsle.
    /// <c>false</c> hvis sendingen feilet helt eller delvis.
    /// </param>
    /// <param name="TrainerCount">Antall trenere som skulle varsles.</param>
    /// <param name="SmsResult">Svaret fra SMS-tjenesten, eller <c>null</c> hvis ingenting ble sendt (ingen trenere eller unntak).</param>
    public sealed record TrainerNotificationResult(bool Success, int TrainerCount, SmsResult? SmsResult);

    /// <summary>
    /// Varsler trenerne for en ressurstype på SMS. Skal kalles <b>etter</b> at endringen er lagret (commit),
    /// slik at en SMS aldri sendes for en endring som ble rullet tilbake.
    /// </summary>
    public interface ITrainerNotifier
    {
        /// <summary>
        /// Sender «X ønsker opplæring på Y og er satt opp på vakt den dd.MM.yyyy» til alle trenere for ressurstypen.
        /// Kaster ikke ved SMS-feil: feilen logges og returneres i <see cref="TrainerNotificationResult"/>.
        /// </summary>
        /// <param name="userId">Brukeren som ønsker opplæring.</param>
        /// <param name="resourceTypeId">Ressurstypen opplæringen gjelder.</param>
        /// <param name="shiftDate">Datoen vakta starter (norsk lokal tid).</param>
        Task<TrainerNotificationResult> NotifyTrainingRequested(int userId, int resourceTypeId, DateTime shiftDate);
    }

    public class TrainerNotifier : ITrainerNotifier
    {
        public TrainerNotifier(PlannerDbContext dbContext, ISmsSender smsSender, ILogger<TrainerNotifier> logger)
        {
            DbContext = dbContext;
            SmsSender = smsSender;
            Logger = logger;
        }

        public PlannerDbContext DbContext { get; }
        public ISmsSender SmsSender { get; }
        public ILogger<TrainerNotifier> Logger { get; }

        public async Task<TrainerNotificationResult> NotifyTrainingRequested(int userId, int resourceTypeId, DateTime shiftDate)
        {
            try
            {
                var user = await DbContext.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);
                var resourceType = await DbContext.ResourceTypes.AsNoTracking().SingleAsync(t => t.ResourceTypeId == resourceTypeId);
                var trainers = await DbContext.ResourceTypeTrainers
                    .Include(t => t.User)
                    .AsNoTracking()
                    .Where(t => t.ResourceTypeId == resourceTypeId)
                    .ToListAsync();

                if (trainers.Count == 0)
                    return new TrainerNotificationResult(true, 0, null);

                var fullName = $"{user.FirstName ?? ""} {user.LastName ?? ""}".Trim();
                var messages = trainers.Select(trainer => new SmsMessage
                {
                    ReceiverPhoneNo = trainer.User.UserName.ToNumericPhoneNo(),
                    Body = $"Hei {trainer.User.FirstName}! {fullName} ønsker opplæring på {resourceType.Name} og er satt opp på vakt den {shiftDate:dd'.'MM'.'yyyy}.",
                }).ToList();

                var result = await SmsSender.SendMessages(messages);
                var success = result.Success && (result.Messages?.All(m => m.Success) ?? true);

                if (success)
                {
                    Logger.LogInformation("Varslet {TrainerCount} trenere om opplæring for bruker {UserId} på ressurstype {ResourceTypeId}",
                        trainers.Count, userId, resourceTypeId);
                }
                else
                {
                    Logger.LogWarning("SMS til trenere feilet for bruker {UserId} på ressurstype {ResourceTypeId}: {Info}",
                        userId, resourceTypeId, result.Info);
                }

                return new TrainerNotificationResult(success, trainers.Count, result);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Kunne ikke varsle trenere om opplæring for bruker {UserId} på ressurstype {ResourceTypeId}", userId, resourceTypeId);
                return new TrainerNotificationResult(false, 0, null);
            }
        }
    }
}
