namespace Middagsasen.Planner.Api.Services.Events
{
    public interface IEventsService
    {
        Task<IEnumerable<EventStatusResponse>> GetEventStatuses(int month, int year);
        Task<EventResponse?> CreateEvent(EventRequest request);
        Task<EventResponse?> GetEventById(int id);
        Task<IEnumerable<EventResponse?>> GetEvents();
        Task<IEnumerable<EventResponse?>> GetEvents(DateTime start, DateTime end);
        Task<EventResponse?> UpdateEvent(int eventId, EventRequest request);
        Task<ShiftResponse?> AddShift(int eventResourceId, ShiftRequest request);
        Task<ShiftResponse?> UpdateShift(int id, ShiftRequest request);
        Task<ShiftResponse?> DeleteShift(int id, int userId, bool isAdmin);
        Task<IEnumerable<UserShiftResponse>> GetShiftsByUserId(int id);
        Task<EventResponse?> DeleteEvent(int id);
        Task<EventResponse?> CreateEventFromTemplate(int templateId, EventFromTemplateRequest request);
    }
}