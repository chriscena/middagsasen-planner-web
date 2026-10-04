using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.Resources;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    public class ShiftService : IShiftService
    {
        internal const string ResourceNotFoundMessage = "Fant ikke vaktressursen.";
        internal const string ShiftNotFoundMessage = "Fant ikke vakta.";
        internal const string UserNotFoundMessage = "Fant ikke brukeren.";
        internal const string PastMessage = "Vakta er avsluttet og kan ikke endres.";
        internal const string FullMessage = "Det er ingen ledige plasser på denne vakta.";
        internal const string DuplicateMessage = "Brukeren står allerede på denne vakta.";
        internal const string InvalidTimesMessage = "Tidene må ligge innenfor vaktas tider, og start kan ikke være etter slutt.";
        internal const string NoTrainingMessage = "Denne vakttypen har ikke opplæring.";
        internal const string NoEmptySlotMessage = "Det er ingen ledige plasser å fjerne.";
        internal const string SmsFailedWarning = "Endringen er lagret, men SMS til trenerne kunne ikke sendes. Gi beskjed til en trener direkte.";

        internal static string TrainingAnswerRequiredMessage(string resourceTypeName)
            => $"Du må svare på om du trenger opplæring på {resourceTypeName}.";

        public ShiftService(IShiftRepository repository, IResourceReader reader, ICurrentUserService currentUser, ITrainerNotifier trainerNotifier, TimeProvider timeProvider)
        {
            Repository = repository;
            Reader = reader;
            CurrentUser = currentUser;
            TrainerNotifier = trainerNotifier;
            TimeProvider = timeProvider;
        }

        public IShiftRepository Repository { get; }
        public IResourceReader Reader { get; }
        public ICurrentUserService CurrentUser { get; }
        public ITrainerNotifier TrainerNotifier { get; }
        public TimeProvider TimeProvider { get; }

        /// <summary>
        /// Opplæring: svaret <see cref="SignUpRequest.TrainingCompleted"/> lagres i samme transaksjon som vakta, etter
        /// regelen i <see cref="ApplyTrainingAnswer"/>. Svaret er påkrevd når målbrukeren mangler opplæringsrad.
        /// </summary>
        public async Task<ShiftResult> SignUp(int resourceId, SignUpRequest request)
        {
            var actor = CurrentUser.ToActor();
            var targetUserId = request.UserId ?? actor.UserId;

            // Tilgangen sjekkes før brukeren slås opp, så en vanlig bruker ikke kan finne ut hvilke bruker-id-er som finnes.
            if (!actor.IsAdminOrSelf(targetUserId))
                throw new ForbiddenAccessException();
            if (!await Repository.UserExists(targetUserId))
                throw new EntityNotFoundException(UserNotFoundMessage);

            var utcNow = TimeProvider.GetUtcNow();
            var now = utcNow.ToNorwegianLocalTime();

            var (training, resourceTypeId, resourceStart) = await RetryOnTrainingConflict(() => Repository.InResourceLock(resourceId, async () =>
            {
                var resource = await Repository.GetResource(resourceId)
                    ?? throw new EntityNotFoundException(ResourceNotFoundMessage);
                var facts = ShiftFactsFactory.From(resource);

                Enforce(ShiftRules.CheckSignUp(actor, facts, now, targetUserId, request.StartTime, request.EndTime));

                // Den nye vakta har ingen id ennå. CheckSetTraining vurderes likevel mot målbrukeren (ShiftId 0), så regelen
                // bare finnes ett sted; CheckSignUp har allerede krevd admin eller seg selv, så den slår ikke til i praksis.
                var owner = new ShiftFacts(0, targetUserId, NeedsTraining: false);
                var trainingChange = await ApplyTrainingAnswer(actor, resource, facts, owner, request.TrainingCompleted, answerRequired: true, utcNow, now);

                Repository.AddShift(new EventResourceUser
                {
                    EventResourceId = resourceId,
                    UserId = targetUserId,
                    StartTime = request.StartTime ?? resource.StartTime,
                    EndTime = request.EndTime ?? resource.EndTime,
                    Comment = request.Comment,
                });

                await Repository.SaveChangesAsync();
                return (trainingChange, resource.ResourceTypeId, resource.StartTime);
            }));

            var warnings = training is { NotifyTrainers: true }
                ? await NotifyTrainers(targetUserId, resourceTypeId, resourceStart)
                : [];

            return await BuildResult(resourceId, training?.Training.ResourceTypeTrainingId, warnings);
        }

        /// <summary>
        /// Null-semantikk: <see cref="ChangeShiftRequest.UserId"/>, <see cref="ChangeShiftRequest.StartTime"/> og
        /// <see cref="ChangeShiftRequest.EndTime"/> beholdes når de er <c>null</c>; <see cref="ChangeShiftRequest.Comment"/>
        /// settes alltid (klienten sender hele vakta), så <c>null</c> fjerner kommentaren.
        /// Opplæringen til eieren etter endringen (<see cref="ChangeShiftRequest.TrainingCompleted"/>) lagres i samme
        /// transaksjon (se <see cref="ApplyTrainingAnswer"/>). Svaret er påkrevd bare når vakta flyttes til en bruker uten
        /// opplæringsrad; ellers er det valgfritt, men lagres hvis det sendes.
        /// </summary>
        public async Task<ShiftResult> Change(int shiftId, ChangeShiftRequest request)
        {
            var actor = CurrentUser.ToActor();
            var resourceId = await Repository.GetResourceIdForShift(shiftId)
                ?? throw new EntityNotFoundException(ShiftNotFoundMessage);
            var utcNow = TimeProvider.GetUtcNow();
            var now = utcNow.ToNorwegianLocalTime();

            var (training, resourceTypeId, resourceStart) = await RetryOnTrainingConflict(() => Repository.InResourceLock(resourceId, async () =>
            {
                var (resource, facts, shiftFacts) = await GetFacts(resourceId, shiftId);
                var shift = await Repository.GetShift(shiftId)
                    ?? throw new EntityNotFoundException(ShiftNotFoundMessage);

                Enforce(ShiftRules.CheckChange(actor, facts, now, shiftFacts,
                    request.UserId, request.StartTime, request.EndTime, shift.StartTime, shift.EndTime));

                var owner = shiftFacts;
                var movesShift = false;
                if (request.UserId is { } newUserId && newUserId != shift.UserId)
                {
                    if (!await Repository.UserExists(newUserId))
                        throw new EntityNotFoundException(UserNotFoundMessage);
                    owner = new ShiftFacts(shiftId, newUserId, NeedsTraining: false);
                    movesShift = true;
                    shift.UserId = newUserId;
                }

                var trainingChange = await ApplyTrainingAnswer(actor, resource, facts, owner, request.TrainingCompleted, answerRequired: movesShift, utcNow, now);

                if (request.StartTime.HasValue)
                    shift.StartTime = request.StartTime;
                if (request.EndTime.HasValue)
                    shift.EndTime = request.EndTime;
                shift.Comment = request.Comment;

                await Repository.SaveChangesAsync();
                return (trainingChange, resource.ResourceTypeId, resource.StartTime);
            }));

            var warnings = training is { NotifyTrainers: true } change
                ? await NotifyTrainers(change.Training.UserId, resourceTypeId, resourceStart)
                : [];

            return await BuildResult(resourceId, training?.Training.ResourceTypeTrainingId, warnings);
        }

        /// <summary>
        /// Upserter opplæringen til eieren av vakta på ressursens ressurstype (se <see cref="UpsertTraining"/>), også når
        /// verdien er lik den lagrede (trenerens bekreftelse oppdateres da). Trenerne varsles etter commit, men bare når
        /// opplæringen ikke allerede var ønsket (så gjentatte kall ikke sender SMS på nytt).
        /// </summary>
        public async Task<ShiftResult> SetTraining(int shiftId, SetTrainingRequest request)
        {
            var actor = CurrentUser.ToActor();
            var resourceId = await Repository.GetResourceIdForShift(shiftId)
                ?? throw new EntityNotFoundException(ShiftNotFoundMessage);
            var utcNow = TimeProvider.GetUtcNow();
            var now = utcNow.ToNorwegianLocalTime();

            var (training, resourceTypeId, resourceStart, ownerId) = await RetryOnTrainingConflict(() => Repository.InResourceLock(resourceId, async () =>
            {
                var (_, facts, shiftFacts) = await GetFacts(resourceId, shiftId);

                Enforce(ShiftRules.CheckSetTraining(actor, facts, now, shiftFacts));
                if (!facts.HasTraining)
                    throw new DomainValidationException(NoTrainingMessage);

                var existing = await Repository.GetTraining(shiftFacts.UserId, facts.ResourceTypeId);
                var change = UpsertTraining(existing, shiftFacts.UserId, facts.ResourceTypeId, request.TrainingCompleted, actor, utcNow);

                await Repository.SaveChangesAsync();
                return (change, facts.ResourceTypeId, facts.StartTime, shiftFacts.UserId);
            }));

            var warnings = training.NotifyTrainers
                ? await NotifyTrainers(ownerId, resourceTypeId, resourceStart)
                : [];

            return await BuildResult(resourceId, training.Training.ResourceTypeTrainingId, warnings);
        }

        public async Task<ShiftResult> Withdraw(int shiftId)
        {
            var actor = CurrentUser.ToActor();
            var resourceId = await Repository.GetResourceIdForShift(shiftId)
                ?? throw new EntityNotFoundException(ShiftNotFoundMessage);
            var now = TimeProvider.GetUtcNow().ToNorwegianLocalTime();

            await Repository.InResourceLock(resourceId, async () =>
            {
                var (_, facts, shiftFacts) = await GetFacts(resourceId, shiftId);
                Enforce(ShiftRules.CheckWithdraw(actor, facts, now, shiftFacts));

                var shift = await Repository.GetShift(shiftId)
                    ?? throw new EntityNotFoundException(ShiftNotFoundMessage);
                Repository.RemoveShift(shift);

                await Repository.SaveChangesAsync();
                return true;
            });

            return await BuildResult(resourceId, null, []);
        }

        public Task<ResourceResponse> AddEmptySlot(int resourceId)
            => ChangeEmptySlots(resourceId, ShiftRules.MinimumStaffAfterAddingEmptySlot);

        public Task<ResourceResponse> RemoveEmptySlot(int resourceId)
            => ChangeEmptySlots(resourceId, staffing => ShiftRules.MinimumStaffAfterRemovingEmptySlot(staffing)
                ?? throw new DomainValidationException(NoEmptySlotMessage));

        /// <summary>
        /// Endrer <c>MinimumStaff</c> relativt (kun admin). Ny verdi regnes ut fra bemanningen lest under ressurslåsen
        /// (<see cref="IShiftRepository.GetStaffing"/>), ikke fra klientens cache, så to samtidige klikk gir to endringer (#142).
        /// Låsen trengs også fordi MinimumStaff inngår i kapasitetsregelen (<see cref="ShiftRules.IsFull"/>): en samtidig
        /// påmelding ser enten den gamle eller den nye verdien.
        /// </summary>
        private async Task<ResourceResponse> ChangeEmptySlots(int resourceId, Func<ResourceStaffing, int> newMinimumStaff)
        {
            if (!CurrentUser.IsAdmin)
                throw new ForbiddenAccessException();

            await Repository.InResourceLock(resourceId, async () =>
            {
                var staffing = await Repository.GetStaffing(resourceId)
                    ?? throw new EntityNotFoundException(ResourceNotFoundMessage);
                await Repository.SetMinimumStaff(resourceId, newMinimumStaff(staffing));
                return true;
            });

            var result = await BuildResult(resourceId, null, []);
            return result.Resource;
        }

        /// <summary>En opplæringsrad som er opprettet eller endret (ikke lagret), og om trenerne skal varsles etter commit.</summary>
        private readonly record struct TrainingChange(ResourceTypeTraining Training, bool NotifyTrainers);

        /// <summary>
        /// Felles opplæringsregel for påmelding og endring av vakt. Gjelder <paramref name="owner"/>, eieren av vakta etter endringen:
        /// <list type="bullet">
        /// <item>Ressurstypen har ikke opplæring: svaret ignoreres.</item>
        /// <item>Eieren har ingen opplæringsrad: <c>null</c> gir 400 når <paramref name="answerRequired"/>, ellers skjer ingenting.
        /// Et svar oppretter raden (se <see cref="UpsertTraining"/>).</item>
        /// <item>Eieren har en rad: <c>null</c> eller samme verdi som lagret endrer ingenting (ingen ny bekreftelse, ingen SMS).
        /// En annen verdi krever <see cref="ShiftRules.CheckSetTraining"/> og oppdaterer raden som <see cref="SetTraining"/>.</item>
        /// </list>
        /// </summary>
        /// <returns>Raden som ble opprettet eller endret, eller <c>null</c>.</returns>
        private async Task<TrainingChange?> ApplyTrainingAnswer(
            Actor actor, EventResource resource, ResourceFacts facts, ShiftFacts owner,
            bool? trainingCompleted, bool answerRequired, DateTimeOffset utcNow, DateTime now)
        {
            if (!facts.HasTraining)
                return null;
            // Uten svar og uten krav om svar blir resultatet null uansett om raden finnes; slipp oppslaget.
            if (trainingCompleted is null && !answerRequired)
                return null;

            var existing = await Repository.GetTraining(owner.UserId, facts.ResourceTypeId);
            if (trainingCompleted is not { } completed)
            {
                if (existing is null && answerRequired)
                    throw new DomainValidationException(TrainingAnswerRequiredMessage(resource.ResourceType.Name));
                return null;
            }

            if (existing is not null)
            {
                if (existing.TrainingComplete == completed)
                    return null;
                Enforce(ShiftRules.CheckSetTraining(actor, facts, now, owner));
            }

            return UpsertTraining(existing, owner.UserId, facts.ResourceTypeId, completed, actor, utcNow);
        }

        /// <summary>
        /// Oppretter eller oppdaterer opplæringsraden (lagres av kalleren). <c>true</c> setter Confirmed/ConfirmedBy til
        /// nå/innlogget bruker; <c>false</c> nullstiller dem, og trenerne skal varsles hvis opplæringen ikke allerede var ønsket.
        /// </summary>
        private TrainingChange UpsertTraining(ResourceTypeTraining? existing, int userId, int resourceTypeId, bool completed, Actor actor, DateTimeOffset utcNow)
        {
            var wasRequested = existing?.TrainingComplete == false;
            var training = existing ?? new ResourceTypeTraining { UserId = userId, ResourceTypeId = resourceTypeId };
            if (existing is null)
                Repository.AddTraining(training);

            training.TrainingComplete = completed;
            training.Confirmed = completed ? utcNow.UtcDateTime : null;
            training.ConfirmedBy = completed ? actor.UserId : null;
            return new TrainingChange(training, !completed && !wasRequested);
        }

        /// <summary>
        /// Kjører <paramref name="operation"/> (en hel <see cref="IShiftRepository.InResourceLock{T}"/>) og prøver den én gang
        /// til hvis en samtidig forespørsel opprettet samme opplæringsrad (<see cref="TrainingConflictException"/>). Første
        /// transaksjon er da rullet tilbake og konteksten tømmes. Andre forsøk ser raden og følger vanlig flyt for en
        /// eksisterende rad (<see cref="ApplyTrainingAnswer"/>): er svaret <c>null</c> eller likt det lagrede, står raden
        /// urørt; er det ulikt, oppdateres raden (med samme tilgangssjekk som ellers).
        /// </summary>
        private async Task<T> RetryOnTrainingConflict<T>(Func<Task<T>> operation)
        {
            try
            {
                return await operation();
            }
            catch (TrainingConflictException)
            {
                Repository.DiscardChanges();
                return await operation();
            }
        }

        private async Task<(EventResource Resource, ResourceFacts Facts, ShiftFacts Shift)> GetFacts(int resourceId, int shiftId)
        {
            var resource = await Repository.GetResource(resourceId)
                ?? throw new EntityNotFoundException(ResourceNotFoundMessage);
            var facts = ShiftFactsFactory.From(resource);
            var shift = facts.Shifts.SingleOrDefault(s => s.ShiftId == shiftId)
                ?? throw new EntityNotFoundException(ShiftNotFoundMessage);
            return (resource, facts, shift);
        }

        private async Task<IReadOnlyList<string>> NotifyTrainers(int userId, int resourceTypeId, DateTime shiftDate)
        {
            var result = await TrainerNotifier.NotifyTrainingRequested(userId, resourceTypeId, shiftDate);
            return result.Success ? [] : [SmsFailedWarning];
        }

        private async Task<ShiftResult> BuildResult(int resourceId, int? changedTrainingId, IReadOnlyList<string> warnings)
        {
            var resource = await Reader.GetResource(CurrentUser.ToActor(), resourceId)
                ?? throw new EntityNotFoundException(ResourceNotFoundMessage);
            var training = changedTrainingId is { } id ? await Reader.GetTraining(id) : null;

            return new ShiftResult
            {
                Resource = resource,
                ChangedTraining = training,
                Warnings = warnings,
            };
        }

        private static void Enforce(ShiftRuleViolation? violation)
        {
            switch (violation)
            {
                case null: return;
                case ShiftRuleViolation.Forbidden: throw new ForbiddenAccessException();
                case ShiftRuleViolation.Past: throw new DomainValidationException(PastMessage);
                case ShiftRuleViolation.Full: throw new DomainValidationException(FullMessage);
                case ShiftRuleViolation.Duplicate: throw new DomainValidationException(DuplicateMessage);
                case ShiftRuleViolation.InvalidTimes: throw new DomainValidationException(InvalidTimesMessage);
                default: throw new ArgumentOutOfRangeException(nameof(violation), violation, null);
            }
        }
    }
}
