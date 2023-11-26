using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text;

namespace Middagsasen.Planner.Api.Services.SmsSender
{
    public class SmsSenderService : ISmsSender
    {
        public SmsSenderService(ISmsSenderSettings settings)
        {
            if (string.IsNullOrEmpty(settings.SmsUsername) || string.IsNullOrEmpty(settings.SmsPassword) || string.IsNullOrEmpty(settings.SmsSenderName))
                throw new InvalidOperationException("Missing SMS Sender Configuration");
            Username = settings.SmsUsername;
            Password = settings.SmsPassword;
            SenderName = settings.SmsSenderName;
            DeliveryReportUrl = settings.SmsDeliveryReportUrl;
        }

        public string Username { get; }
        public string Password { get; }
        public string SenderName { get; }
        public string? DeliveryReportUrl { get; }
        private const string BaseUrl = "https://api.eurobate.com/";

        private static HttpClient? _httpClient;
        private static HttpClient HttpClient { get { return _httpClient ?? (_httpClient = new HttpClient() { BaseAddress = new Uri(BaseUrl) }); } }

        private string SendSmsUri = "json_api.php";
        private readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new DefaultNamingStrategy()
            }
        };
        public async Task<SmsResult> SendMessages(IEnumerable<SmsMessage> messages)
        {
            var eurobateMessages = messages.Select(m => new EurobateMessageModel
            {
                originator = SenderName,
                msisdn = m.ReceiverPhoneNo,
                message = m.Body,
                dlrurl = DeliveryReportUrl != null ? DeliveryReportUrl + $"?id={m.SmsNotificationId}&msgId=MSGID&status=STATUS" : null,
            }).ToList();

            var model = new EurobateMessagesModel
            {
                user = Username,
                password = Password,
                messages = eurobateMessages,
            };
            var jsonModel = JsonConvert.SerializeObject(model, serializerSettings);
            var content = new StringContent(jsonModel, Encoding.UTF8, "application/json");
            try
            {
                var response = await HttpClient.PostAsync(SendSmsUri, content);

                if (response.IsSuccessStatusCode)
                {
                    var resultJson = await response.Content.ReadAsStringAsync();

                    var result = JsonConvert.DeserializeObject<EurobateSmsResultModel>(resultJson);

                    var messageResult = result?.messages.Select(m => new SmsMessageResult
                    {
                        ReceiverPhoneNo = m.msisdn,
                        Success = m.error == 0,
                        ExternalId = m.transactionid.ToString(),
                        Info = m.info,
                    }).ToList();

                    return new SmsResult
                    {
                        Success = result?.error == 0,
                        Messages = messageResult ?? new List<SmsMessageResult>(),
                    };
                }

                return new SmsResult
                {
                    Success = false,
                    Messages = new List<SmsMessageResult>(),
                    Info = $"{(int)response.StatusCode} {response.ReasonPhrase}"
                };
            }
            catch (Exception ex)
            {
                return new SmsResult
                {
                    Success = false,
                    Messages = new List<SmsMessageResult>(),
                    Info = ex.Message,
                };
            }
        }
    }

    internal class EurobateMessagesModel
    {
        public string user { get; set; } = null!;
        public string password { get; set; } = null!;
        public IEnumerable<EurobateMessageModel> messages { get; set; } = new List<EurobateMessageModel>();
    }

    internal class EurobateMessageModel
    {
        public string originator { get; set; } = null!;
        public long msisdn { get; set; }
        public string message { get; set; } = null!;
        public string? dlrurl { get; set; }
    }

    //{"LOGON":"OK","STATUS":"OK","error":0,"messages":[{"msisdn":4791761026,"transactionid":254729875,"error":0,"info":"Ok","messageParts":1,"uuid":"445c0ba6-a39e-11ec-aa0b-42010a84000c"}]}

    internal class EurobateSmsResultModel
    {
        public string? LOGON { get; set; }
        public string? STATUS { get; set; }
        public int error { get; set; }
        public IEnumerable<EurobateSmsResultMessage> messages { get; set; } = new List<EurobateSmsResultMessage>();
    }

    public class EurobateSmsResultMessage
    {
        public long msisdn { get; set; }
        public long transactionid { get; set; }
        public int error { get; set; }
        public string? info { get; set; }
        public int messageParts { get; set; }
        public Guid uuid { get; set; }
    }
}
