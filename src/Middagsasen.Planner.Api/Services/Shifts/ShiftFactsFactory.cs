using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>
    /// Lager faktaene <see cref="ShiftRules"/> trenger ut fra en lastet ressurs. Brukes både av <see cref="ShiftService"/>
    /// (håndhevelse) og av lesemodulen for ressurser (flagg), slik at begge ser de samme faktaene.
    /// </summary>
    public static class ShiftFactsFactory
    {
        /// <summary>
        /// Faktaene om ressursen og vaktene. Krever <c>ResourceType.Trainers</c> og <c>Shifts.User.Trainings</c>.
        /// </summary>
        public static ResourceFacts From(EventResource resource) => new(
            resource.EventResourceId,
            resource.ResourceTypeId,
            resource.StartTime,
            resource.EndTime,
            resource.MinimumStaff,
            HasTraining: resource.ResourceType.Trainers.Count > 0,
            TrainerUserIds: resource.ResourceType.Trainers.Select(t => t.UserId).ToHashSet(),
            Shifts: resource.Shifts.Select(s => From(s, resource.ResourceTypeId)).ToList());

        private static ShiftFacts From(EventResourceUser shift, int resourceTypeId) => new(
            shift.EventResourceUserId,
            shift.UserId,
            NeedsTraining: shift.User?.Trainings.Any(t => t.ResourceTypeId == resourceTypeId && t.TrainingComplete == false) ?? false);
    }
}
