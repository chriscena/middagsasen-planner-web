using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api.Services.Shifts
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
    /// Varsler trenerne for en vakttype på SMS. Skal kalles <b>etter</b> at endringen er lagret (commit),
    /// slik at en SMS aldri sendes for en endring som ble rullet tilbake.
    /// </summary>
    public interface ITrainerNotifier
    {
        /// <summary>
        /// Sender «X ønsker opplæring på Y og er satt opp på vakt den dd.MM.yyyy» til alle trenere for vakttypen.
        /// Kaster ikke ved SMS-feil: feilen logges og returneres i <see cref="TrainerNotificationResult"/>.
        /// </summary>
        /// <param name="userId">Brukeren som ønsker opplæring.</param>
        /// <param name="resourceTypeId">Vakttypen opplæringen gjelder.</param>
        /// <param name="shiftDate">Datoen vakta starter (norsk lokal tid).</param>
        Task<TrainerNotificationResult> NotifyTrainingRequested(int userId, int resourceTypeId, DateTime shiftDate);
    }

    public class TrainerNotifier : ITrainerNotifier
    {
        public TrainerNotifier(ITrainerRepository repository, ISmsSender smsSender, ILogger<TrainerNotifier> logger)
        {
            Repository = repository;
            SmsSender = smsSender;
            Logger = logger;
        }

        public ITrainerRepository Repository { get; }
        public ISmsSender SmsSender { get; }
        public ILogger<TrainerNotifier> Logger { get; }

        public async Task<TrainerNotificationResult> NotifyTrainingRequested(int userId, int resourceTypeId, DateTime shiftDate)
        {
            try
            {
                var user = await Repository.GetUser(userId);
                var resourceType = await Repository.GetResourceType(resourceTypeId);
                var trainers = await Repository.GetTrainers(resourceTypeId);

                if (trainers.Count == 0)
                    return new TrainerNotificationResult(true, 0, null);

                var fullName = user.FullName();
                var messages = trainers.Select(trainer => new SmsMessage
                {
                    ReceiverPhoneNo = trainer.UserName.ToNumericPhoneNo(),
                    Body = $"Hei {trainer.FirstName}! {fullName} ønsker opplæring på {resourceType.Name} og er satt opp på vakt den {shiftDate:dd'.'MM'.'yyyy}.",
                }).ToList();

                var result = await SmsSender.SendMessages(messages);
                var success = result.Success && (result.Messages?.All(m => m.Success) ?? true);

                if (success)
                {
                    Logger.LogInformation("Varslet {TrainerCount} trenere om opplæring for bruker {UserId} på vakttype {ResourceTypeId}",
                        trainers.Count, userId, resourceTypeId);
                }
                else
                {
                    Logger.LogWarning("SMS til trenere feilet for bruker {UserId} på vakttype {ResourceTypeId}: {Info}",
                        userId, resourceTypeId, result.Info);
                }

                return new TrainerNotificationResult(success, trainers.Count, result);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Kunne ikke varsle trenere om opplæring for bruker {UserId} på vakttype {ResourceTypeId}", userId, resourceTypeId);
                return new TrainerNotificationResult(false, 0, null);
            }
        }
    }
}
