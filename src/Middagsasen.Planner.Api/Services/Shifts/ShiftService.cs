using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Events;

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
        internal const string NegativeMinimumStaffMessage = "Minimum bemanning kan ikke være negativ.";
        internal const string SmsFailedWarning = "Endringen er lagret, men SMS til trenerne kunne ikke sendes. Gi beskjed til en trener direkte.";

        internal static string TrainingAnswerRequiredMessage(string resourceTypeName)
            => $"Du må svare på om du trenger opplæring på {resourceTypeName}.";

        public ShiftService(IShiftRepository repository, ICurrentUserService currentUser, ITrainerNotifier trainerNotifier, TimeProvider timeProvider)
        {
            Repository = repository;
            CurrentUser = currentUser;
            TrainerNotifier = trainerNotifier;
            TimeProvider = timeProvider;
        }

        public IShiftRepository Repository { get; }
        public ICurrentUserService CurrentUser { get; }
        public ITrainerNotifier TrainerNotifier { get; }
        public TimeProvider TimeProvider { get; }

        public async Task<ResourceMapper> CreateResourceMapper()
        {
            var actor = CurrentUser.ToActor();
            var trainingResourceTypeIds = await Repository.GetTrainingResourceTypeIds(actor.UserId);
            return new ResourceMapper(actor, TimeProvider.GetUtcNow(), trainingResourceTypeIds);
        }

        /// <summary>
        /// Opplæring: har ressurstypen opplæring og målbrukeren ingen opplæringsrad, er <see cref="SignUpRequest.NeedsTraining"/>
        /// påkrevd (se <see cref="AddTrainingFromAnswer"/>). Har brukeren en rad, ignoreres svaret.
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
                var facts = ResourceMapper.ToFacts(resource);

                Enforce(ShiftRules.CheckSignUp(actor, facts, now, targetUserId, request.StartTime, request.EndTime));

                var newTraining = await AddTrainingFromAnswer(actor, resource, targetUserId, request.NeedsTraining, utcNow);

                Repository.AddShift(new EventResourceUser
                {
                    EventResourceId = resourceId,
                    UserId = targetUserId,
                    StartTime = request.StartTime ?? resource.StartTime,
                    EndTime = request.EndTime ?? resource.EndTime,
                    Comment = request.Comment,
                });

                await Repository.SaveChangesAsync();
                return (newTraining, resource.ResourceTypeId, resource.StartTime);
            }));

            var warnings = training?.TrainingComplete == false
                ? await NotifyTrainers(targetUserId, resourceTypeId, resourceStart)
                : [];

            return await BuildResult(resourceId, training?.ResourceTypeTrainingId, warnings);
        }

        /// <summary>
        /// Null-semantikk: <see cref="ChangeShiftRequest.UserId"/>, <see cref="ChangeShiftRequest.StartTime"/> og
        /// <see cref="ChangeShiftRequest.EndTime"/> beholdes når de er <c>null</c>; <see cref="ChangeShiftRequest.Comment"/>
        /// settes alltid (klienten sender hele vakta), så <c>null</c> fjerner kommentaren.
        /// Flyttes vakta til en annen bruker, gjelder samme opplæringsregel som ved påmelding
        /// (<see cref="ChangeShiftRequest.NeedsTraining"/>, se <see cref="AddTrainingFromAnswer"/>).
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

                ResourceTypeTraining? newTraining = null;
                if (request.UserId is { } newUserId && newUserId != shift.UserId)
                {
                    if (!await Repository.UserExists(newUserId))
                        throw new EntityNotFoundException(UserNotFoundMessage);
                    newTraining = await AddTrainingFromAnswer(actor, resource, newUserId, request.NeedsTraining, utcNow);
                    shift.UserId = newUserId;
                }

                if (request.StartTime.HasValue)
                    shift.StartTime = request.StartTime;
                if (request.EndTime.HasValue)
                    shift.EndTime = request.EndTime;
                shift.Comment = request.Comment;

                await Repository.SaveChangesAsync();
                return (newTraining, resource.ResourceTypeId, resource.StartTime);
            }));

            var warnings = training?.TrainingComplete == false
                ? await NotifyTrainers(training.UserId, resourceTypeId, resourceStart)
                : [];

            return await BuildResult(resourceId, training?.ResourceTypeTrainingId, warnings);
        }

        /// <summary>
        /// Upserter opplæringen til eieren av vakta på ressursens ressurstype. <c>true</c> setter Confirmed/ConfirmedBy
        /// til nå/innlogget bruker; <c>false</c> nullstiller dem og varsler trenerne etter commit, men bare når opplæringen
        /// ikke allerede var ønsket (så gjentatte kall ikke sender SMS på nytt).
        /// </summary>
        public async Task<ShiftResult> SetTraining(int shiftId, SetTrainingRequest request)
        {
            var actor = CurrentUser.ToActor();
            var resourceId = await Repository.GetResourceIdForShift(shiftId)
                ?? throw new EntityNotFoundException(ShiftNotFoundMessage);
            var utcNow = TimeProvider.GetUtcNow();
            var now = utcNow.ToNorwegianLocalTime();

            var (training, notifyTrainers, resourceTypeId, resourceStart, ownerId) = await RetryOnTrainingConflict(() => Repository.InResourceLock(resourceId, async () =>
            {
                var (_, facts, shiftFacts) = await GetFacts(resourceId, shiftId);

                Enforce(ShiftRules.CheckSetTraining(actor, facts, now, shiftFacts));
                if (!facts.HasTraining)
                    throw new DomainValidationException(NoTrainingMessage);

                var existing = await Repository.GetTraining(shiftFacts.UserId, facts.ResourceTypeId);
                var wasRequested = existing?.TrainingComplete == false;
                var training = existing ?? new ResourceTypeTraining { UserId = shiftFacts.UserId, ResourceTypeId = facts.ResourceTypeId };
                if (existing is null)
                    Repository.AddTraining(training);

                training.TrainingComplete = request.TrainingCompleted;
                training.Confirmed = request.TrainingCompleted ? utcNow.UtcDateTime : null;
                training.ConfirmedBy = request.TrainingCompleted ? actor.UserId : null;

                await Repository.SaveChangesAsync();
                return (training, !request.TrainingCompleted && !wasRequested, facts.ResourceTypeId, facts.StartTime, shiftFacts.UserId);
            }));

            var warnings = notifyTrainers
                ? await NotifyTrainers(ownerId, resourceTypeId, resourceStart)
                : [];

            return await BuildResult(resourceId, training.ResourceTypeTrainingId, warnings);
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

        public async Task<ResourceResponse> SetMinimumStaff(int resourceId, MinimumStaffRequest request)
        {
            var actor = CurrentUser.ToActor();
            if (!actor.IsAdmin)
                throw new ForbiddenAccessException();
            if (request.MinimumStaff < 0)
                throw new DomainValidationException(NegativeMinimumStaffMessage);

            // Under ressurslåsen fordi MinimumStaff inngår i kapasitetsregelen (ShiftRules.IsFull): en samtidig
            // påmelding skal enten se den gamle eller den nye verdien, ikke vurdere kapasitet mens den endres.
            await Repository.InResourceLock(resourceId, async () =>
            {
                await Repository.SetMinimumStaff(resourceId, request.MinimumStaff);
                return true;
            });

            var result = await BuildResult(resourceId, null, []);
            return result.Resource;
        }

        /// <summary>
        /// Felles opplæringsregel for påmelding og flytting av vakt: har ressurstypen opplæring og <paramref name="userId"/>
        /// ingen opplæringsrad for den, er svaret <paramref name="needsTraining"/> påkrevd (ellers 400).
        /// <c>true</c> legger til en rad med TrainingComplete = false (trenerne varsles etter commit av kalleren);
        /// <c>false</c> legger til en rad med TrainingComplete = true (selverklæring, bekreftet av innlogget bruker).
        /// Ellers ignoreres svaret og ingenting legges til.
        /// </summary>
        /// <returns>Den nye opplæringsraden (ikke lagret), eller <c>null</c>.</returns>
        private async Task<ResourceTypeTraining?> AddTrainingFromAnswer(Actor actor, EventResource resource, int userId, bool? needsTraining, DateTimeOffset utcNow)
        {
            if (resource.ResourceType.Trainers.Count == 0)
                return null;
            if (await Repository.GetTraining(userId, resource.ResourceTypeId) is not null)
                return null;
            if (needsTraining is not { } needs)
                throw new DomainValidationException(TrainingAnswerRequiredMessage(resource.ResourceType.Name));

            var training = new ResourceTypeTraining
            {
                UserId = userId,
                ResourceTypeId = resource.ResourceTypeId,
                TrainingComplete = !needs,
                Confirmed = needs ? null : utcNow.UtcDateTime,
                ConfirmedBy = needs ? null : actor.UserId,
            };
            Repository.AddTraining(training);
            return training;
        }

        /// <summary>
        /// Kjører <paramref name="operation"/> (en hel <see cref="IShiftRepository.InResourceLock{T}"/>) og prøver den én gang
        /// til hvis en samtidig forespørsel opprettet samme opplæringsrad (<see cref="TrainingConflictException"/>). Første
        /// transaksjon er da rullet tilbake; konteksten tømmes, og andre forsøk ser raden og lar den stå.
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
            var facts = ResourceMapper.ToFacts(resource);
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
            var mapper = await CreateResourceMapper();
            var resource = await Repository.GetResource(resourceId)
                ?? throw new EntityNotFoundException(ResourceNotFoundMessage);
            var training = changedTrainingId is { } id ? await Repository.GetTrainingForResponse(id) : null;

            return new ShiftResult
            {
                Resource = mapper.Map(resource),
                ChangedTraining = training is null ? null : ResourceMapper.MapTraining(training),
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
