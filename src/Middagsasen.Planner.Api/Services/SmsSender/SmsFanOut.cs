using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Services.SmsSender
{
    /// <summary>
    /// En bruker som skal ha SMS. Lest med en lett projeksjon, så passordhash og resten av <see cref="Data.User"/>
    /// aldri hentes bare for å sende en melding.
    /// </summary>
    /// <param name="UserId">Brukeren, til loggen.</param>
    /// <param name="UserName">Brukernavnet, som er telefonnummeret (se <see cref="UserNameExtensions.ToNormalizedUserName"/>).</param>
    /// <param name="FirstName">Fornavnet, til hilsenen i meldingen. Kan mangle.</param>
    public sealed record SmsRecipient(int UserId, string UserName, string? FirstName);

    /// <summary>Resultatet av en SMS-utsending til flere brukere (<see cref="ISmsFanOut.SendToUsers"/>).</summary>
    /// <param name="Success">
    /// <c>true</c> hvis alle SMS-ene ble sendt, eller det ikke var noe å sende (ingen mottakere, eller ingen med gyldig
    /// telefonnummer). <c>false</c> hvis sendingen feilet helt eller delvis.
    /// </param>
    /// <param name="RecipientCount">Antall brukere som fikk SMS (brukere uten gyldig telefonnummer er ikke med).</param>
    /// <param name="SmsResult">Svaret fra SMS-tjenesten, eller <c>null</c> hvis ingenting ble sendt (ingen mottakere eller unntak).</param>
    public sealed record SmsFanOutResult(bool Success, int RecipientCount, SmsResult? SmsResult)
    {
        /// <summary>Ingenting å sende: regnes som vellykket.</summary>
        public static readonly SmsFanOutResult Nothing = new(true, 0, null);

        /// <summary>Sendingen feilet før SMS-tjenesten svarte (unntak).</summary>
        public static readonly SmsFanOutResult Failed = new(false, 0, null);
    }

    /// <summary>
    /// Sender samme type SMS til flere brukere, med samme regler for alle varsler: brukere hvis brukernavn ikke er et
    /// gyldig telefonnummer hoppes over med en advarsel i loggen, resten varsles, og utsendingen er vellykket bare når
    /// SMS-tjenesten godtok alle meldingene. Felles for <see cref="Shifts.TrainerNotifier"/> og
    /// <see cref="StaffingAlerts.StaffingAlertNotifier"/>; de eier selv hva meldingen sier og hva som skjer ved unntak.
    /// </summary>
    public interface ISmsFanOut
    {
        /// <summary>
        /// Sender én SMS til hver mottaker med gyldig telefonnummer. Kaster hvis SMS-tjenesten kaster; kalleren
        /// bestemmer hva det betyr for den.
        /// </summary>
        /// <param name="recipients">Brukerne som skal varsles.</param>
        /// <param name="body">Meldingsteksten til hver mottaker (f.eks. med fornavnet i hilsenen).</param>
        /// <param name="purpose">
        /// Hva varselet gjelder, til loggen, f.eks. «bemanningsvarsel for oppgave 12». Skrives i advarselen om brukere
        /// som hoppes over og i meldingen om at sendingen lyktes eller feilet.
        /// </param>
        Task<SmsFanOutResult> SendToUsers(IReadOnlyList<SmsRecipient> recipients, Func<SmsRecipient, string> body, string purpose);
    }

    public class SmsFanOut : ISmsFanOut
    {
        public SmsFanOut(ISmsSender smsSender, ILogger<SmsFanOut> logger)
        {
            SmsSender = smsSender;
            Logger = logger;
        }

        public ISmsSender SmsSender { get; }
        public ILogger<SmsFanOut> Logger { get; }

        public async Task<SmsFanOutResult> SendToUsers(IReadOnlyList<SmsRecipient> recipients, Func<SmsRecipient, string> body, string purpose)
        {
            var messages = new List<SmsMessage>(recipients.Count);
            foreach (var recipient in recipients)
            {
                // Lagrede brukernavn er normalt normalisert (8 sifre), men eldre brukere kan ha brukernavn som
                // «admin» (deploy-skriptet beholder brukernavn det ikke kan normalisere). De kan ikke få SMS.
                var phoneNo = recipient.UserName.ToNormalizedUserName();
                if (phoneNo == null)
                {
                    Logger.LogWarning("Bruker {UserId} har ikke et gyldig telefonnummer som brukernavn og får ikke SMS: {Purpose}",
                        recipient.UserId, purpose);
                    continue;
                }
                messages.Add(new SmsMessage
                {
                    ReceiverPhoneNo = phoneNo.ToSmsPhoneNo(),
                    Body = body(recipient),
                });
            }

            if (messages.Count == 0)
                return SmsFanOutResult.Nothing;

            var result = await SmsSender.SendMessages(messages);
            var success = result.Success && (result.Messages?.All(m => m.Success) ?? true);

            if (success)
                Logger.LogInformation("Sendte SMS til {RecipientCount} brukere: {Purpose}", messages.Count, purpose);
            else
                Logger.LogWarning("SMS til {RecipientCount} brukere feilet: {Purpose}: {Info}", messages.Count, purpose, result.Info);

            return new SmsFanOutResult(success, messages.Count, result);
        }
    }
}
