using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public partial class ZeroBounce
    {
        protected static readonly ZeroBounce _instance = new ZeroBounce();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static ZeroBounce()
        {
        }
        protected ZeroBounce()
        {
        }
        public static ZeroBounce Instance => _instance;

        private const string BulkApiBaseUrl = "https://bulkapi.zerobounce.net/v2";
        protected HttpClient _client = CreateHttpClient();
        private string _apiKey;
        private string _apiBaseUrl = ZBApiURLConverter.GetApiURLString(ZBApiURL.ApiDefaultURL);

        private static readonly string PackageVersionValue = ReadPackageVersion();

        private static string ReadPackageVersion()
        {
            var attributes = typeof(ZeroBounce).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false);
            foreach (AssemblyMetadataAttribute attribute in attributes)
            {
                if (attribute.Key == "ZeroBounce.PackageVersion" && !string.IsNullOrEmpty(attribute.Value))
                    return attribute.Value;
            }
            return "0.0.0";
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ZeroBounceSDK/" + PackageVersionValue);
            return client;
        }

        private static void RequireHttpsUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) ||
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new ZBClientException("base_url must be an https:// URL");
            }
        }

        private static string Enc(string value) => Uri.EscapeDataString(value ?? "");

        private static string RedactApiKey(string message, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(message))
                return message;
            return message.Replace(apiKey, "***");
        }

        private static string SafeMultipartFileName(string fileName)
        {
            var baseName = Path.GetFileName(fileName ?? "upload.csv");
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "upload.csv";
            return baseName.Replace("\"", "").Replace("\r", "").Replace("\n", "");
        }

        /// <summary>
        /// Initialize the SDK with your API key. Uses default API base URL.
        /// </summary>
        /// <exception cref="ZBClientException">Thrown when apiKey is null or whitespace.</exception>
        public void Initialize(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ZBClientException("Empty parameter: api_key");
            _apiKey = apiKey;
        }

        /// <summary>
        /// Initialize the SDK with your API key and a predefined API base URL.
        /// </summary>
        /// <exception cref="ZBClientException">Thrown when apiKey is null or whitespace.</exception>
        public void Initialize(string apiKey, ZBApiURL apiBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ZBClientException("Empty parameter: api_key");
            _apiKey = apiKey;
            _apiBaseUrl = ZBApiURLConverter.GetApiURLString(apiBaseUrl);
        }

        /// <summary>
        /// Initialize the SDK with your API key and a custom API base URL (e.g. for testing).
        /// Trailing slashes are removed from the base URL.
        /// </summary>
        /// <exception cref="ZBClientException">Thrown when apiKey is null or whitespace.</exception>
        public void Initialize(string apiKey, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ZBClientException("Empty parameter: api_key");
            _apiKey = apiKey;
            _apiBaseUrl = baseUrl == null ? ZBApiURLConverter.GetApiURLString(ZBApiURL.ApiDefaultURL) : baseUrl.TrimEnd('/');
            RequireHttpsUrl(_apiBaseUrl);
        }

        /// <param name="email">The email address you want to validate</param>
        /// <param name="ipAddress"> The IP Address the email signed up from (Can be blank).</param>
        /// <param name="successCallback"> The success callback function, called with a ZBValidateResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void Validate(string email, string ipAddress, Action<ZBValidateResponse> successCallback,
            Action<string> failureCallback)
        {
            Validate(email, ipAddress, null, successCallback, failureCallback);
        }

        public void Validate(string email, string ipAddress, ZBValidateOptions options,
            Action<ZBValidateResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (RejectTimeout(options?.Timeout, false, failureCallback)) return;
            if (RejectCallbackUrl(options, failureCallback)) return;

            if (options != null && options.UsePost)
            {
                var form = new Dictionary<string, string>
                {
                    { "api_key", _apiKey },
                    { "email", email },
                    { "ip_address", ipAddress ?? "" }
                };
                if (options.Timeout.HasValue)
                    form["timeout"] = options.Timeout.Value.ToString();
                if (options.ActivityData.HasValue)
                    form["activity_data"] = options.ActivityData.Value ? "true" : "false";
                if (options.VerifyPlus.HasValue)
                    form["verify_plus"] = options.VerifyPlus.Value ? "true" : "false";
                if (options.DomainInfo.HasValue)
                    form["domain_info"] = options.DomainInfo.Value ? "true" : "false";
                if (options.GreylistProcessing.HasValue)
                    form["greylist_processing"] = options.GreylistProcessing.Value ? "true" : "false";
                if (options.CallbackUrl != null)
                    form["callback_url"] = options.CallbackUrl;
                _sendFormPostRequest(_apiBaseUrl + "/validate", form, successCallback, failureCallback).Wait();
                return;
            }

            var query = _apiBaseUrl + "/validate?api_key=" + Enc(_apiKey) + "&email=" + Enc(email) + "&ip_address=" + Enc(ipAddress ?? "");
            if (options != null)
            {
                if (options.Timeout.HasValue)
                    query += "&timeout=" + Enc(options.Timeout.Value.ToString());
                if (options.ActivityData.HasValue)
                    query += "&activity_data=" + Enc(options.ActivityData.Value ? "true" : "false");
                if (options.VerifyPlus.HasValue)
                    query += "&verify_plus=" + Enc(options.VerifyPlus.Value ? "true" : "false");
                if (options.DomainInfo.HasValue)
                    query += "&domain_info=" + Enc(options.DomainInfo.Value ? "true" : "false");
                if (options.GreylistProcessing.HasValue)
                    query += "&greylist_processing=" + Enc(options.GreylistProcessing.Value ? "true" : "false");
                if (options.CallbackUrl != null)
                    query += "&callback_url=" + Enc(options.CallbackUrl);
            }

            _sendRequest(query, successCallback, failureCallback).Wait();
        }

        /// <param name="emailBatch">A list of ZBValidateEmailRow objects</param>
        /// <param name="successCallback"> The success callback function, called with a ZBValidateResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void ValidateBatch(List<ZBValidateEmailRow> emailBatch, Action<ZBValidateBatchResponse> successCallback,
            Action<string> failureCallback)
        {
            ValidateBatch(emailBatch, null, successCallback, failureCallback);
        }

        public void ValidateBatch(List<ZBValidateEmailRow> emailBatch, ZBValidateOptions options,
            Action<ZBValidateBatchResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (RejectTimeout(options?.Timeout, true, failureCallback)) return;
            if (emailBatch == null || emailBatch.Count == 0)
            {
                failureCallback("Empty parameter: email_batch");
                return;
            }
            foreach (var row in emailBatch)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.EmailAddress))
                {
                    failureCallback("Empty parameter: email_address");
                    return;
                }
            }

            var requestData = new ZBValidateEmailRequest { ApiKey = _apiKey, EmailBatch = emailBatch };
            if (options != null)
            {
                if (options.Timeout.HasValue)
                    requestData.Timeout = options.Timeout.Value.ToString(CultureInfo.InvariantCulture);
                if (options.ActivityData.HasValue) requestData.ActivityData = options.ActivityData;
                if (options.VerifyPlus.HasValue) requestData.VerifyPlus = options.VerifyPlus;
            }
            var json = JsonConvert.SerializeObject(requestData);

            _sendJsonRequest(
                _apiBaseUrl + "/validatebatch",
                json,
                successCallback,
                failureCallback).Wait();
        }

        /// <sumary>This API will tell you how many credits you have left on your account. It's simple, fast and easy to use.</sumary>
        /// <param name="successCallback"> The success callback function, called with a ZBCreditsResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void GetCredits(Action<ZBCreditsResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(_apiBaseUrl + "/getcredits?api_key=" + Enc(_apiKey),
                successCallback, failureCallback).Wait();
        }

        /// <param name="startDate">The start date of when you want to view API usage</param>
        /// <param name="endDate">The end date of when you want to view API usage</param>
        public void GetApiUsage(
            DateTime startDate, DateTime endDate,
            Action<ZBGetApiUsageResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                _apiBaseUrl + "/getapiusage?api_key=" + Enc(_apiKey)
                + "&start_date=" + Enc(startDate.ToString("yyyy-MM-dd"))
                + "&end_date=" + Enc(endDate.ToString("yyyy-MM-dd")),
                successCallback,
                failureCallback).Wait();
        }

        /// <param name="email">The email address for which we are interested in getting the engagement</param>
        public void GetActivity(string email, Action<ZBActivityResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                _apiBaseUrl + "/activity?api_key=" + Enc(_apiKey)
                + "&email=" + Enc(email),
                successCallback,
                failureCallback).Wait();
        }

        /// <summary>Real-time AI scoring for a single email (GET /scoring).</summary>
        public void ScoreEmail(string email, Action<ZBScoreEmailResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                _apiBaseUrl + "/scoring?api_key=" + Enc(_apiKey)
                + "&email=" + Enc(email),
                successCallback,
                failureCallback).Wait();
        }

        /// <summary>Real-time AI scoring for a single email (POST /scoring, form-encoded).</summary>
        public void ScoreEmailPost(string email, Action<ZBScoreEmailResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            var form = new Dictionary<string, string>
            {
                { "api_key", _apiKey },
                { "email", email }
            };
            _sendFormPostRequest(_apiBaseUrl + "/scoring", form, successCallback, failureCallback).Wait();
        }

        /// <param name="domain">The email domain for which to find the email format.</param>
        /// <param name="first_name">The first name of the person whose email format is being searched.</param>
        /// <param name="middle_name">The middle name of the person whose email format is being searched. [optional]</param>
        /// <param name="last_name">The last name of the person whose email format is being searched. [optional]</param>
        public void FindEmailByDomain(
            string domain, string firstName, string middleName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindEmail(domain, null, firstName, middleName, lastName, successCallback, failureCallback);
        }

        /// <param name="domain">The email domain for which to find the email format.</param>
        /// <param name="first_name">The first name of the person whose email format is being searched.</param>
        /// <param name="last_name">The last name of the person whose email format is being searched. [optional]</param>
        public void FindEmailByDomain(
            string domain, string firstName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindEmail(domain, null, firstName, null, lastName, successCallback, failureCallback);
        }

        /// <param name="domain">The email domain for which to find the email format.</param>
        /// <param name="first_name">The first name of the person whose email format is being searched.</param>
        public void FindEmailByDomain(
            string domain, string firstName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindEmail(domain, null, firstName, null, null, successCallback, failureCallback);
        }

        /// <param name="company_name">The company name for which to find the email format.</param>
        /// <param name="first_name">The first name of the person whose email format is being searched.</param>
        /// <param name="middle_name">The middle name of the person whose email format is being searched. [optional]</param>
        /// <param name="last_name">The last name of the person whose email format is being searched. [optional]</param>
        public void FindEmailByCompanyName(
            string companyName, string firstName, string middleName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindEmail(null, companyName, firstName, middleName, lastName, successCallback, failureCallback);
        }

        /// <param name="company_name">The company name for which to find the email format.</param>
        /// <param name="first_name">The first name of the person whose email format is being searched.</param>
        /// <param name="last_name">The last name of the person whose email format is being searched. [optional]</param>
        public void FindEmailByCompanyName(
            string companyName, string firstName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindEmail(null, companyName, firstName, null, lastName, successCallback, failureCallback);
        }

        /// <param name="company_name">The company name for which to find the email format.</param>
        /// <param name="first_name">The first name of the person whose email format is being searched.</param>
        public void FindEmailByCompanyName(
            string companyName, string firstName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindEmail(null, companyName, firstName, null, null, successCallback, failureCallback);
        }

        private void _FindEmail(
            string domain, string companyName, string firstName, string middleName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (string.IsNullOrWhiteSpace(domain) && string.IsNullOrWhiteSpace(companyName))
            {
                failureCallback("Empty parameter: domain or company_name required");
                return;
            }
            if (!string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(companyName))
            {
                failureCallback("Parameter error: domain and company_name cannot be used together");
                return;
            }

            string url = _apiBaseUrl + "/guessformat?api_key=" + Enc(_apiKey);
            if (domain != null) {
                url += "&domain=" + Enc(domain ?? "");
            }
            if (companyName != null) {
                url += "&company_name=" + Enc(companyName ?? "");
            }
            if (firstName != null) {
                url += "&first_name=" + Enc(firstName ?? "");
            }
            if (middleName != null) {
                url += "&middle_name=" + Enc(middleName ?? "");
            }
            if (lastName != null) {
                url += "&last_name=" + Enc(lastName ?? "");
            }

            _sendRequest(
                url,
                successCallback,
                failureCallback).Wait();
        }


        /// <param name="domain">The email domain for which to find the email format.</param>
        public void FindDomainByDomain(
            string domain,
            Action<ZBDomainSearchResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindDomain(domain, null, successCallback, failureCallback);
        }

        /// <param name="company_name">The company name for which to find the email format</param>
        public void FindDomainByCompanyName(
            string companyName,
            Action<ZBDomainSearchResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindDomain(null, companyName, successCallback, failureCallback);
        }

        private void _FindDomain(
            string domain, string companyName,
            Action<ZBDomainSearchResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (string.IsNullOrWhiteSpace(domain) && string.IsNullOrWhiteSpace(companyName))
            {
                failureCallback("Empty parameter: domain xor company_name required");
                return;
            }
            if (!string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(companyName))
            {
                failureCallback("Parameter error: domain and company_name cannot be used together");
                return;
            }

            string url = _apiBaseUrl + "/guessformat?api_key=" + Enc(_apiKey);
            if (domain != null) {
                url += "&domain=" + Enc(domain ?? "");
            }
            if (companyName != null) {
                url += "&company_name=" + Enc(companyName ?? "");
            }

            _sendRequest(
                url,
                successCallback,
                failureCallback).Wait();
        }


        /// <param name="domain">The email domain for which to find the email format.</param>
        /// <param name="first_name">The first name of the person whose email format is being searched. [optional]</param>
        /// <param name="middle_name">The middle name of the person whose email format is being searched. [optional]</param>
        /// <param name="last_name">The last name of the person whose email format is being searched. [optional]</param>
        [Obsolete("Use FindEmail() for Email Finder API, or FindDomain() for Domain Search API")]
        public void EmailFinder(
            string domain, string firstName, string middleName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                _apiBaseUrl + "/guessformat?api_key=" + Enc(_apiKey)
                + "&domain=" + Enc(domain ?? "")
                + "&first_name=" + Enc(firstName ?? "")
                + "&middle_name=" + Enc(middleName ?? "")
                + "&last_name=" + Enc(lastName ?? ""),
                successCallback,
                failureCallback).Wait();
        }

        /// <param name="fileId">The returned file ID when calling sendFile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBFileStatusResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void FileStatus(string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback)
        {
            _FileStatus(false, fileId, successCallback, failureCallback);
        }
        
        /// <param name="fileId">The returned file ID when calling scoringSendFile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBFileStatusResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void ScoringFileStatus(string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback)
        {
            _FileStatus(true, fileId, successCallback, failureCallback);
        }

        private void _FileStatus(bool scoring, string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (string.IsNullOrWhiteSpace(fileId))
            {
                failureCallback("Empty parameter: file_id");
                return;
            }

            _sendRequest(
                BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/filestatus?api_key=" + Enc(_apiKey) + "&file_id=" + Enc(fileId),
                successCallback, failureCallback).Wait();
        }

        
        public class SendFileOptions
        {
            public string ReturnUrl;
            public int EmailAddressColumn;
            public int FirstNameColumn;
            public int LastNameColumn;
            public int GenderColumn;
            public int IpAddressColumn;
            public bool HasHeaderRow;
            public bool RemoveDuplicate;
            /// <summary>
            /// When set, sent as multipart <c>allow_phase_2</c> (validation bulk only). See API sendfile docs.
            /// </summary>
            public bool? AllowPhase2;
        }

        /// <summary>
        /// Optional query parameters for bulk <c>getfile</c> (validation overload with <c>GetFileOptions</c>).
        /// <see cref="ActivityData"/> applies to validation bulk only; ignored for scoring getfile.
        /// </summary>
        public class GetFileOptions
        {
            public ZBDownloadType? DownloadType { get; set; }
            public bool? ActivityData { get; set; }
        }
        
        /// <summary>
        /// The scoringSendfile API allows user to send a file for bulk email validation
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void ScoringSendFile(string filePath, SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                using (var file = File.OpenRead(filePath))
                {
                    _SendFile(true, file, fileName, options, successCallback, failureCallback).Wait();
                }
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(e.Message, _apiKey));
            }
        }

        /// <summary>
        /// The scoringSendfile API allows user to send a file for bulk email validation (from a stream).
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void ScoringSendFile(Stream file, string fileName, SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _SendFile(true, file, fileName, options, successCallback, failureCallback).Wait();
        }

        /// <summary>
        /// The sendfile API allows user to send a file for bulk email validation
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void SendFile(string filePath, SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                using (var file = File.OpenRead(filePath))
                {
                    _SendFile(false, file, fileName, options, successCallback, failureCallback).Wait();
                }
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(e.Message, _apiKey));
            }
        }

        /// <summary>
        /// The sendfile API allows user to send a file for bulk email validation (from a stream).
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void SendFile(Stream file, string fileName, SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _SendFile(false, file, fileName, options, successCallback, failureCallback).Wait();
        }

        private async Task _SendFile(bool scoring, Stream file, string fileName, SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            
            try
            {
                var content = new MultipartFormDataContent();
                content.Add(new StreamContent(file), "file", SafeMultipartFileName(fileName));

                content.Add(new StringContent(_apiKey), "api_key");

                if (options != null)
                {
                    content.Add(new StringContent(options.EmailAddressColumn.ToString()), "email_address_column");
                    if (options.ReturnUrl != null)
                        content.Add(new StringContent(options.ReturnUrl), "return_url");
                    if (!scoring)
                    {
                        if (options.FirstNameColumn > 0)
                            content.Add(new StringContent(options.FirstNameColumn.ToString()), "first_name_column");
                        if (options.LastNameColumn > 0)
                            content.Add(new StringContent(options.LastNameColumn.ToString()), "last_name_column");
                        if (options.GenderColumn > 0)
                            content.Add(new StringContent(options.GenderColumn.ToString()), "gender_column");
                        if (options.IpAddressColumn > 0)
                            content.Add(new StringContent(options.IpAddressColumn.ToString()), "ip_address_column");
                        if (options.AllowPhase2.HasValue)
                            content.Add(new StringContent(options.AllowPhase2.Value ? "true" : "false"), "allow_phase_2");
                    }
                    content.Add(new StringContent(options.HasHeaderRow ? "true" : "false"), "has_header_row");
                    content.Add(new StringContent(options.RemoveDuplicate ? "true" : "false"), "remove_duplicate");
                }

                var url = BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/sendfile";
                var result = await _client.PostAsync(url, content);
                await DispatchJson(result, successCallback, failureCallback);
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(e.Message, _apiKey));
            }
        }

        /// <summary>
        /// The scoringGetFile API allows users to get the validation results file for the file been submitted using scoringSendfile API
        /// <param name="successCallback"> The success callback function, called with a ZBGetFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void ScoringGetFile(string fileId, string localDownloadPath, 
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            _GetFile(true, fileId, localDownloadPath, null, successCallback, failureCallback).Wait();
        }

        /// <summary>
        /// Scoring getfile with optional <see cref="GetFileOptions.DownloadType"/> (<c>activity_data</c> is ignored).
        /// </summary>
        public void ScoringGetFile(string fileId, string localDownloadPath, GetFileOptions getFileOptions,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            _GetFile(true, fileId, localDownloadPath, getFileOptions, successCallback, failureCallback).Wait();
        }
        
        /// <summary>
        /// The getfile API allows users to get the validation results file for the file been submitted using sendfile API
        /// <param name="successCallback"> The success callback function, called with a ZBGetFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void GetFile(string fileId, string localDownloadPath, 
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            _GetFile(false, fileId, localDownloadPath, null, successCallback, failureCallback).Wait();
        }

        /// <summary>
        /// Validation getfile with optional <c>download_type</c> and <c>activity_data</c> query parameters.
        /// </summary>
        public void GetFile(string fileId, string localDownloadPath, GetFileOptions getFileOptions,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            _GetFile(false, fileId, localDownloadPath, getFileOptions, successCallback, failureCallback).Wait();
        }

        private static string DownloadTypeToQueryValue(ZBDownloadType t)
        {
            switch (t)
            {
                case ZBDownloadType.Phase1: return "phase_1";
                case ZBDownloadType.Phase2: return "phase_2";
                case ZBDownloadType.Combined: return "combined";
                default: throw new ArgumentOutOfRangeException(nameof(t), t, null);
            }
        }

        private static string BuildGetFileUrl(string bulkBase, bool scoring, string apiKey, string fileId, GetFileOptions options)
        {
            var url = bulkBase + (scoring ? "/scoring" : "") + "/getfile?api_key=" + Enc(apiKey) + "&file_id=" + Enc(fileId);
            if (options == null) return url;
            if (options.DownloadType.HasValue)
                url += "&download_type=" + DownloadTypeToQueryValue(options.DownloadType.Value);
            if (!scoring && options.ActivityData.HasValue)
                url += "&activity_data=" + (options.ActivityData.Value ? "true" : "false");
            return url;
        }

        private async Task _GetFile(bool scoring, string fileId, string localDownloadPath, GetFileOptions getFileOptions,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (string.IsNullOrWhiteSpace(fileId))
            {
                failureCallback("Empty parameter: file_id");
                return;
            }

            try
            {
                var url = BuildGetFileUrl(BulkApiBaseUrl, scoring, _apiKey, fileId, getFileOptions);
                var response = await _client.GetAsync(url);
                await DeliverDownloadedFile(response, localDownloadPath, successCallback, failureCallback);
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(e.Message, _apiKey));
            }
        }
        
        
        /// <param name="fileId">The returned file ID when calling scoringSendfile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBDeleteFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void ScoringDeleteFile(string fileId, Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback) {
            _DeleteFile(true, fileId, successCallback, failureCallback);
        }


        /// <param name="fileId">The returned file ID when calling sendfile API.</param>
        /// <param name="successCallback"> The success callback function, called with a ZBDeleteFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void DeleteFile(string fileId, Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback) {
            _DeleteFile(false, fileId, successCallback, failureCallback);
        }

        private void _DeleteFile(bool scoring, string fileId, Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback) {
            if (InvalidApiKey(failureCallback)) return;
            if (string.IsNullOrWhiteSpace(fileId))
            {
                failureCallback("Empty parameter: file_id");
                return;
            }

            _sendRequest(
                BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/deletefile?api_key=" + Enc(_apiKey) + "&file_id=" + Enc(fileId),
                successCallback,
                failureCallback).Wait();
        }

        /// <summary>
        /// The detail of the failure the most recent callback on this client
        /// reported: the same short message, plus the HTTP status.
        /// <para>
        /// It is set synchronously, immediately before the failure callback
        /// runs, so reading it from inside that callback always describes the
        /// call that is failing. Use one client per key and per thread, as the
        /// rest of this SDK already asks.
        /// </para>
        /// </summary>
        public ZBApiError LastApiError { get; private set; }

        /// <summary>Records the detail, then hands the short message to the caller.</summary>
        private void Fail(Action<string> failureCallback, string message, int statusCode)
        {
            LastApiError = new ZBApiError(message, statusCode);
            failureCallback(message);
        }

        private bool InvalidApiKey(Action<string> failureCallback)
        {
            if (_apiKey != null) return false;
            failureCallback("ZeroBounce SDK is not initialized. " +
                            "Please call ZeroBounce.Instance.Initialize(apiKey) first");
            return true;
        }

        private static bool RejectTimeout(int? timeout, bool batch, Action<string> failureCallback)
        {
            if (!timeout.HasValue)
                return false;
            var value = timeout.Value;
            if (batch)
            {
                if (value >= 10 && value <= 120)
                    return false;
                failureCallback("Invalid parameter: timeout (must be between 10 and 120 seconds)");
                return true;
            }
            if (value >= 3 && value <= 60)
                return false;
            failureCallback("Invalid parameter: timeout (must be between 3 and 60 seconds)");
            return true;
        }

        /// <summary>
        /// The callback is where the greylist retry posts its result, so it is
        /// meaningless — and rejected by the API — without the retry itself.
        /// Checked before any request is sent.
        /// </summary>
        private static bool RejectCallbackUrl(ZBValidateOptions options, Action<string> failureCallback)
        {
            if (options?.CallbackUrl == null)
                return false;
            if (string.IsNullOrWhiteSpace(options.CallbackUrl))
            {
                failureCallback("Empty parameter: callback_url");
                return true;
            }
            if (options.GreylistProcessing != true)
            {
                failureCallback("Invalid parameter: callback_url requires greylist_processing to be true");
                return true;
            }
            return false;
        }

        private async Task _sendRequest<T>(string url, Action<T> successCallback, Action<string> failureCallback)
            where T : ZBResponse
        {
            try
            {
                var result = await _client.GetAsync(url);
                await DispatchJson(result, successCallback, failureCallback);
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(HttpErrors.MessageFromBody(e.Message, 0), _apiKey));
            }
        }

        private async Task _sendListRequest<T>(string url, Action<List<T>> successCallback, Action<string> failureCallback)
        {
            try
            {
                var result = await _client.GetAsync(url);
                var body = await ReadUsableBody(result, failureCallback);
                if (body == null)
                    return;
                var response = JsonConvert.DeserializeObject<List<T>>(body);
                successCallback(response ?? new List<T>());
            }
            catch (JsonException)
            {
                failureCallback("ZeroBounce returned a response that is not valid JSON.");
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(HttpErrors.MessageFromBody(e.Message, 0), _apiKey));
            }
        }

        private async Task _sendJsonRequest<T>(string url, string json, Action<T> successCallback, Action<string> failureCallback)
            where T : ZBResponse
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var result = await _client.PostAsync(url, content);
                await DispatchJson(result, successCallback, failureCallback);
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(HttpErrors.MessageFromBody(e.Message, 0), _apiKey));
            }
        }

        private async Task _sendFormPostRequest<T>(string url, Dictionary<string, string> form,
            Action<T> successCallback, Action<string> failureCallback)
            where T : ZBResponse
        {
            try
            {
                var content = new FormUrlEncodedContent(form);
                var result = await _client.PostAsync(url, content);
                await DispatchJson(result, successCallback, failureCallback);
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(HttpErrors.MessageFromBody(e.Message, 0), _apiKey));
            }
        }

        private async Task DispatchJson<T>(HttpResponseMessage result, Action<T> successCallback, Action<string> failureCallback)
            where T : ZBResponse
        {
            var status = (int)result.StatusCode;
            var body = await ReadUsableBody(result, failureCallback);
            if (body == null)
                return;
            try
            {
                var response = JsonConvert.DeserializeObject<T>(body);
                if (response == null)
                {
                    failureCallback(RedactApiKey(HttpErrors.MessageFromBody(body, status), _apiKey));
                    return;
                }
                if (response is ZBValidateBatchResponse batch && ReportAccountBatchError(batch, failureCallback))
                    return;
                successCallback(response);
            }
            catch (JsonException)
            {
                failureCallback(RedactApiKey(HttpErrors.MessageFromBody(body, status), _apiKey));
            }
        }

        private bool ReportAccountBatchError(ZBValidateBatchResponse batch, Action<string> failureCallback)
        {
            if (batch.Errors == null)
                return false;
            foreach (var item in batch.Errors)
            {
                if (item == null || !string.Equals(item.EmailAddress, "all", StringComparison.OrdinalIgnoreCase))
                    continue;
                var message = string.IsNullOrEmpty(item.Error)
                    ? "Invalid API Key or your account ran out of credits"
                    : HttpErrors.StripErrorPrefix(item.Error);
                failureCallback(RedactApiKey(message, _apiKey));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the body when it can be parsed as an API payload. On HTML, an empty body,
        /// or a non-success status, reports a short error and returns null.
        /// </summary>
        private async Task<string> ReadUsableBody(HttpResponseMessage result, Action<string> failureCallback)
        {
            var body = result.Content == null ? "" : await result.Content.ReadAsStringAsync();
            var media = result.Content?.Headers.ContentType?.MediaType ?? "";
            var status = (int)result.StatusCode;
            if (status > 299 || HttpErrors.ContentTypeIsHtml(media) || HttpErrors.BodyLooksLikeHtml(body) || string.IsNullOrWhiteSpace(body))
            {
                Fail(failureCallback, RedactApiKey(HttpErrors.MessageFromBody(body, status), _apiKey), status);
                return null;
            }
            // LIVE: {"success": false} on an HTTP 200 is a failure, not a
            // result. The API answers an unknown bulk file id that way.
            if (HttpErrors.SuccessMeansFailure(body))
            {
                Fail(failureCallback, RedactApiKey(HttpErrors.MessageFromBody(body, status), _apiKey), status);
                return null;
            }
            return body;
        }

        private async Task DeliverDownloadedFile(HttpResponseMessage response, string localDownloadPath,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            var bytes = response.Content == null
                ? new byte[0]
                : await response.Content.ReadAsByteArrayAsync();
            var body = Encoding.UTF8.GetString(bytes);
            var media = response.Content?.Headers.ContentType?.MediaType ?? "";
            var status = (int)response.StatusCode;

            if (status > 299 || HttpErrors.ContentTypeIsHtml(media) || HttpErrors.BodyLooksLikeHtml(body))
            {
                failureCallback(RedactApiKey(HttpErrors.MessageFromBody(body, status), _apiKey));
                return;
            }

            if (media.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ZBGetFileResponse jsonResponse;
                try
                {
                    jsonResponse = JsonConvert.DeserializeObject<ZBGetFileResponse>(body);
                }
                catch (JsonException)
                {
                    failureCallback(RedactApiKey(HttpErrors.MessageFromBody(body, status), _apiKey));
                    return;
                }
                var isError = jsonResponse != null &&
                    (!jsonResponse.Success ||
                     !string.IsNullOrEmpty(jsonResponse.Message) ||
                     !string.IsNullOrEmpty(jsonResponse.Error) ||
                     !string.IsNullOrEmpty(jsonResponse.ErrorMessage));
                if (isError || jsonResponse == null)
                {
                    var message = jsonResponse == null
                        ? HttpErrors.MessageFromBody(body, status)
                        : (jsonResponse.Message
                           ?? jsonResponse.ErrorMessage
                           ?? jsonResponse.Error
                           ?? "Request not processed successfully.");
                    failureCallback(RedactApiKey(HttpErrors.StripErrorPrefix(message), _apiKey));
                    return;
                }
                successCallback(jsonResponse);
                return;
            }

            var dirPath = Path.GetDirectoryName(localDownloadPath);
            if (!string.IsNullOrEmpty(dirPath)) Directory.CreateDirectory(dirPath);
            File.WriteAllBytes(localDownloadPath, bytes);
            successCallback(new ZBGetFileResponse
            {
                Success = true,
                LocalFilePath = Path.GetFullPath(localDownloadPath)
            });
        }
    }
}
