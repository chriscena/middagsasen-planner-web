using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Services
{
    public interface IEventTemplatesService
    {
        Task<IEnumerable<EventTemplateResponse>> GetEventTemplates();
        Task<EventTemplateResponse?> GetEventTemplateById(int id);
        Task<EventTemplateResponse?> CreateEventTemplate(EventTemplateRequest request);
        Task<EventTemplateResponse?> DeleteEventTemplate(int id);
        Task<EventTemplateResponse?> UpdateEventTemplate(int id, EventTemplateRequest request);
        Task<EventTemplateResponse?> CreateTemplateFromEvent(int id, TemplateFromEventRequest request);
    }
}