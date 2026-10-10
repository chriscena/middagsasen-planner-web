using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>
    /// Varsler trenerne for en vakttype på SMS. Skal kalles <b>etter</b> at endringen er lagret (commit),
    /// slik at en SMS aldri sendes for en endring som ble rullet tilbake.
    /// </summary>
    public interface ITrainerNotifier
    {
        /// <summary>
        /// Sender «X ønsker opplæring på Y og er satt opp på vakt den dd.MM.yyyy» til alle trenere for vakttypen.
        /// Trenere hvis brukernavn ikke er et gyldig telefonnummer (f.eks. «admin») hoppes over med en advarsel i
        /// loggen; de andre varsles likevel (<see cref="ISmsFanOut"/>). Kaster ikke ved SMS-feil: feilen logges og
        /// returneres i <see cref="SmsFanOutResult"/>.
        /// </summary>
        /// <param name="userId">Brukeren som ønsker opplæring.</param>
        /// <param name="resourceTypeId">Vakttypen opplæringen gjelder.</param>
        /// <param name="shiftDate">Datoen vakta starter (norsk lokal tid).</param>
        Task<SmsFanOutResult> NotifyTrainingRequested(int userId, int resourceTypeId, DateTime shiftDate);
    }

    public class TrainerNotifier : ITrainerNotifier
    {
        public TrainerNotifier(ITrainerRepository repository, ISmsFanOut smsFanOut, ILogger<TrainerNotifier> logger)
        {
            Repository = repository;
            SmsFanOut = smsFanOut;
            Logger = logger;
        }

        public ITrainerRepository Repository { get; }
        public ISmsFanOut SmsFanOut { get; }
        public ILogger<TrainerNotifier> Logger { get; }

        public async Task<SmsFanOutResult> NotifyTrainingRequested(int userId, int resourceTypeId, DateTime shiftDate)
        {
            try
            {
                var trainers = await Repository.GetTrainers(resourceTypeId);
                if (trainers.Count == 0)
                    return SmsFanOutResult.Nothing;

                var fullName = (await Repository.GetName(userId)).FullName();
                var resourceType = await Repository.GetResourceType(resourceTypeId);

                return await SmsFanOut.SendToUsers(
                    trainers,
                    trainer => $"Hei {trainer.FirstName}! {fullName} ønsker opplæring på {resourceType.Name} og er satt opp på vakt den {shiftDate:dd'.'MM'.'yyyy}.",
                    $"opplæring for bruker {userId} på vakttype {resourceTypeId}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Kunne ikke varsle trenere om opplæring for bruker {UserId} på vakttype {ResourceTypeId}", userId, resourceTypeId);
                return SmsFanOutResult.Failed;
            }
        }
    }
}
