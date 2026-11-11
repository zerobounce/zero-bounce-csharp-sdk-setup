using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
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

        /// <summary>
        /// A key with surrounding whitespace or control characters is never a
        /// valid key, and the transport would quote it, unredacted, in its own
        /// error. The Warmup client rejects it the same way.
        /// </summary>
        private static void RequireUsableApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ZBClientException("Empty parameter: api_key");
            if (apiKey != apiKey.Trim())
                throw new ZBClientException("api_key has surrounding whitespace or control characters");
            foreach (var c in apiKey)
            {
                if (char.IsControl(c))
                    throw new ZBClientException("api_key has surrounding whitespace or control characters");
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
        /// <exception cref="ZBClientException">
        /// Thrown when apiKey is null or whitespace, or has surrounding whitespace
        /// or control characters.
        /// </exception>
        public void Initialize(string apiKey)
        {
            RequireUsableApiKey(apiKey);
            _apiKey = apiKey;
        }

        /// <summary>
        /// Initialize the SDK with your API key and a predefined API base URL.
        /// </summary>
        /// <exception cref="ZBClientException">
        /// Thrown when apiKey is null or whitespace, or has surrounding whitespace
        /// or control characters.
        /// </exception>
        public void Initialize(string apiKey, ZBApiURL apiBaseUrl)
        {
            RequireUsableApiKey(apiKey);
            _apiKey = apiKey;
            _apiBaseUrl = ZBApiURLConverter.GetApiURLString(apiBaseUrl);
        }

        /// <summary>
        /// Initialize the SDK with your API key and a custom API base URL (e.g. for testing).
        /// Trailing slashes are removed from the base URL.
        /// </summary>
        /// <exception cref="ZBClientException">
        /// Thrown when apiKey is null or whitespace, or has surrounding whitespace
        /// or control characters, or when baseUrl is not an https:// URL.
        /// Nothing is stored when it throws: the key and base URL set before stay in place.
        /// </exception>
        public void Initialize(string apiKey, string baseUrl)
        {
            RequireUsableApiKey(apiKey);
            var url = baseUrl == null ? ZBApiURLConverter.GetApiURLString(ZBApiURL.ApiDefaultURL) : baseUrl.TrimEnd('/');
            RequireHttpsUrl(url);
            _apiKey = apiKey;
            _apiBaseUrl = url;
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
                RunSync(() => _sendFormPostRequest(_apiBaseUrl + "/validate", form, successCallback, failureCallback));
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

            RunSync(() => _sendRequest(query, successCallback, failureCallback));
        }

        /// <param name="emailBatch">A list of ZBValidateEmailRow objects</param>
        /// <param name="successCallback"> The success callback function, called with a ZBValidateResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void ValidateBatch(List<ZBValidateEmailRow> emailBatch, Action<ZBValidateBatchResponse> successCallback,
            Action<string> failureCallback)
        {
            ValidateBatch(emailBatch, null, successCallback, failureCallback);
        }

        /// <summary>
        /// Validates a batch of addresses in one request (POST /validatebatch).
        /// <para>
        /// Of <see cref="ZBValidateOptions"/> the batch sends only
        /// <see cref="ZBValidateOptions.Timeout"/> (10-120 seconds),
        /// <see cref="ZBValidateOptions.ActivityData"/> and
        /// <see cref="ZBValidateOptions.VerifyPlus"/>.
        /// <see cref="ZBValidateOptions.UsePost"/>,
        /// <see cref="ZBValidateOptions.DomainInfo"/>,
        /// <see cref="ZBValidateOptions.GreylistProcessing"/> and
        /// <see cref="ZBValidateOptions.CallbackUrl"/> apply to
        /// <see cref="Validate(string, string, ZBValidateOptions, Action{ZBValidateResponse}, Action{string})"/>
        /// only; the batch ignores them and does not check them.
        /// </para>
        /// </summary>
        public void ValidateBatch(List<ZBValidateEmailRow> emailBatch, ZBValidateOptions options,
            Action<ZBValidateBatchResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (RejectTimeout(options?.Timeout, true, failureCallback)) return;
            if (emailBatch == null || emailBatch.Count == 0)
            {
                Fail(failureCallback, "Empty parameter: email_batch", 0);
                return;
            }
            foreach (var row in emailBatch)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.EmailAddress))
                {
                    Fail(failureCallback, "Empty parameter: email_address", 0);
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

            RunSync(() => _sendJsonRequest(
                _apiBaseUrl + "/validatebatch",
                json,
                successCallback,
                failureCallback));
        }

        /// <sumary>This API will tell you how many credits you have left on your account. It's simple, fast and easy to use.</sumary>
        /// <param name="successCallback"> The success callback function, called with a ZBCreditsResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void GetCredits(Action<ZBCreditsResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            RunSync(() => _sendRequest(_apiBaseUrl + "/getcredits?api_key=" + Enc(_apiKey),
                successCallback, failureCallback));
        }

        /// <param name="startDate">The start date of when you want to view API usage</param>
        /// <param name="endDate">The end date of when you want to view API usage</param>
        public void GetApiUsage(
            DateTime startDate, DateTime endDate,
            Action<ZBGetApiUsageResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            RunSync(() => _sendRequest(
                _apiBaseUrl + "/getapiusage?api_key=" + Enc(_apiKey)
                + "&start_date=" + Enc(startDate.ToString("yyyy-MM-dd"))
                + "&end_date=" + Enc(endDate.ToString("yyyy-MM-dd")),
                successCallback,
                failureCallback));
        }

        /// <param name="email">The email address for which we are interested in getting the engagement</param>
        public void GetActivity(string email, Action<ZBActivityResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            RunSync(() => _sendRequest(
                _apiBaseUrl + "/activity?api_key=" + Enc(_apiKey)
                + "&email=" + Enc(email),
                successCallback,
                failureCallback));
        }

        /// <summary>Real-time AI scoring for a single email (GET /scoring).</summary>
        public void ScoreEmail(string email, Action<ZBScoreEmailResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            RunSync(() => _sendRequest(
                _apiBaseUrl + "/scoring?api_key=" + Enc(_apiKey)
                + "&email=" + Enc(email),
                successCallback,
                failureCallback));
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
            RunSync(() => _sendFormPostRequest(_apiBaseUrl + "/scoring", form, successCallback, failureCallback));
        }

        /// <param name="domain">The email domain for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
        /// <param name="middleName">The middle name of the person whose email format is being searched. [optional]</param>
        /// <param name="lastName">The last name of the person whose email format is being searched. [optional]</param>
        public void FindEmailByDomain(
            string domain, string firstName, string middleName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindEmail(domain, null, firstName, middleName, lastName, successCallback, failureCallback);
        }

        /// <param name="domain">The email domain for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
        /// <param name="lastName">The last name of the person whose email format is being searched. [optional]</param>
        public void FindEmailByDomain(
            string domain, string firstName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindEmail(domain, null, firstName, null, lastName, successCallback, failureCallback);
        }

        /// <param name="domain">The email domain for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
        public void FindEmailByDomain(
            string domain, string firstName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindEmail(domain, null, firstName, null, null, successCallback, failureCallback);
        }

        /// <param name="companyName">The company name for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
        /// <param name="middleName">The middle name of the person whose email format is being searched. [optional]</param>
        /// <param name="lastName">The last name of the person whose email format is being searched. [optional]</param>
        public void FindEmailByCompanyName(
            string companyName, string firstName, string middleName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindEmail(null, companyName, firstName, middleName, lastName, successCallback, failureCallback);
        }

        /// <param name="companyName">The company name for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
        /// <param name="lastName">The last name of the person whose email format is being searched. [optional]</param>
        public void FindEmailByCompanyName(
            string companyName, string firstName, string lastName,
            Action<ZBEmailFinderResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindEmail(null, companyName, firstName, null, lastName, successCallback, failureCallback);
        }

        /// <param name="companyName">The company name for which to find the email format.</param>
        /// <param name="firstName">The first name of the person whose email format is being searched.</param>
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
                Fail(failureCallback, "Empty parameter: domain or company_name required", 0);
                return;
            }
            if (!string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(companyName))
            {
                Fail(failureCallback, "Parameter error: domain and company_name cannot be used together", 0);
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

            RunSync(() => _sendRequest(
                url,
                successCallback,
                failureCallback,
                messageOnlyIsFailure: true));
        }


        /// <param name="domain">The email domain for which to find the email format.</param>
        public void FindDomainByDomain(
            string domain,
            Action<ZBDomainSearchResponse> successCallback,
            Action<string> failureCallback)
        {
            _FindDomain(domain, null, successCallback, failureCallback);
        }

        /// <param name="companyName">The company name for which to find the email format</param>
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
                Fail(failureCallback, "Empty parameter: domain xor company_name required", 0);
                return;
            }
            if (!string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(companyName))
            {
                Fail(failureCallback, "Parameter error: domain and company_name cannot be used together", 0);
                return;
            }

            string url = _apiBaseUrl + "/guessformat?api_key=" + Enc(_apiKey);
            if (domain != null) {
                url += "&domain=" + Enc(domain ?? "");
            }
            if (companyName != null) {
                url += "&company_name=" + Enc(companyName ?? "");
            }

            RunSync(() => _sendRequest(
                url,
                successCallback,
                failureCallback,
                messageOnlyIsFailure: true));
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
                Fail(failureCallback, "Empty parameter: file_id", 0);
                return;
            }

            RunSync(() => _sendRequest(
                BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/filestatus?api_key=" + Enc(_apiKey) + "&file_id=" + Enc(fileId),
                successCallback, failureCallback));
        }

        
        public class SendFileOptions
        {
            public string ReturnUrl;
            public int EmailAddressColumn;
            public int FirstNameColumn;
            public int LastNameColumn;
            public int GenderColumn;
            public int IpAddressColumn;
            /// <summary>
            /// Sent as <c>has_header_row</c> when set, as <c>true</c> or
            /// <c>false</c>. Left unset, it is not sent and the API default applies.
            /// </summary>
            public bool? HasHeaderRow;
            /// <summary>
            /// Sent as <c>remove_duplicate</c> when set, as <c>true</c> or
            /// <c>false</c>. Left unset, it is not sent and the API default
            /// applies, which the sendfile docs give as <c>true</c>: duplicates
            /// are removed. Set it to <c>false</c> to keep them.
            /// </summary>
            public bool? RemoveDuplicate;
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
            if (!TryOpenUpload(filePath, failureCallback, out var file, out var fileName)) return;
            using (file)
            {
                RunSync(() => _SendFile(true, file, fileName, options, successCallback, failureCallback));
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
            RunSync(() => _SendFile(true, file, fileName, options, successCallback, failureCallback));
        }

        /// <summary>
        /// The sendfile API allows user to send a file for bulk email validation
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void SendFile(string filePath, SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (!TryOpenUpload(filePath, failureCallback, out var file, out var fileName)) return;
            using (file)
            {
                RunSync(() => _SendFile(false, file, fileName, options, successCallback, failureCallback));
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
            RunSync(() => _SendFile(false, file, fileName, options, successCallback, failureCallback));
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
                    // An unset option is left out, as in 2.x, so the API's own
                    // default applies: remove_duplicate defaults to true there,
                    // and sending false would silently stop the de-duplication.
                    if (options.HasHeaderRow.HasValue)
                        content.Add(new StringContent(options.HasHeaderRow.Value ? "true" : "false"), "has_header_row");
                    if (options.RemoveDuplicate.HasValue)
                        content.Add(new StringContent(options.RemoveDuplicate.Value ? "true" : "false"), "remove_duplicate");
                }

                var url = BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/sendfile";
                var result = await _client.PostAsync(url, content).ConfigureAwait(false);
                await DispatchJson(result, successCallback, failureCallback).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Fail(failureCallback, RedactApiKey(e.Message, _apiKey), 0);
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
            RunSync(() => _GetFile(true, fileId, localDownloadPath, null, successCallback, failureCallback));
        }

        /// <summary>
        /// Scoring getfile with optional <see cref="GetFileOptions.DownloadType"/> (<c>activity_data</c> is ignored).
        /// </summary>
        public void ScoringGetFile(string fileId, string localDownloadPath, GetFileOptions getFileOptions,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            RunSync(() => _GetFile(true, fileId, localDownloadPath, getFileOptions, successCallback, failureCallback));
        }
        
        /// <summary>
        /// The getfile API allows users to get the validation results file for the file been submitted using sendfile API
        /// <param name="successCallback"> The success callback function, called with a ZBGetFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void GetFile(string fileId, string localDownloadPath, 
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            RunSync(() => _GetFile(false, fileId, localDownloadPath, null, successCallback, failureCallback));
        }

        /// <summary>
        /// Validation getfile with optional <c>download_type</c> and <c>activity_data</c> query parameters.
        /// </summary>
        public void GetFile(string fileId, string localDownloadPath, GetFileOptions getFileOptions,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            RunSync(() => _GetFile(false, fileId, localDownloadPath, getFileOptions, successCallback, failureCallback));
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
                Fail(failureCallback, "Empty parameter: file_id", 0);
                return;
            }

            try
            {
                var url = BuildGetFileUrl(BulkApiBaseUrl, scoring, _apiKey, fileId, getFileOptions);
                var response = await _client.GetAsync(url).ConfigureAwait(false);
                await DeliverDownloadedFile(response, localDownloadPath, successCallback, failureCallback).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Fail(failureCallback, RedactApiKey(e.Message, _apiKey), 0);
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
                Fail(failureCallback, "Empty parameter: file_id", 0);
                return;
            }

            RunSync(() => _sendRequest(
                BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/deletefile?api_key=" + Enc(_apiKey) + "&file_id=" + Enc(fileId),
                successCallback,
                failureCallback));
        }

        /// <summary>
        /// The outcome of the most recent call on this client: null when it
        /// succeeded, otherwise the short message its failure callback was
        /// given plus the HTTP status (0 when the failure never reached the
        /// API: a rejected argument, a missing file, or a transport error).
        /// <para>
        /// It is set synchronously, immediately before either callback runs,
        /// so reading it from inside the failure callback always describes the
        /// call that is failing, and a later success clears it. Use one client
        /// per key and per thread, as the rest of this SDK already asks.
        /// </para>
        /// </summary>
        public ZBApiError LastApiError { get; private set; }

        /// <summary>Records the detail, then hands the short message to the caller.</summary>
        private void Fail(Action<string> failureCallback, string message, int statusCode)
        {
            var error = new ZBApiError(message, statusCode);
            Deliver(() =>
            {
                LastApiError = error;
                failureCallback(message);
            });
        }

        /// <summary>
        /// Clears the last error, then hands the result to the caller. A null
        /// success callback is allowed: a download is still written to disk
        /// and the call returns normally, as in the Java and C++ SDKs.
        /// </summary>
        private void Succeed<T>(Action<T> successCallback, T value)
        {
            Deliver(() =>
            {
                LastApiError = null;
                successCallback?.Invoke(value);
            });
        }

        /// <summary>
        /// The callbacks of the request <see cref="RunSync"/> is waiting for.
        /// They are held back until the request has finished, so they run on
        /// the caller's own thread and outside the SDK's error handling.
        /// </summary>
        private static readonly AsyncLocal<List<Action>> PendingCallbacks = new AsyncLocal<List<Action>>();

        private static void Deliver(Action callback)
        {
            var pending = PendingCallbacks.Value;
            if (pending != null)
                pending.Add(callback);
            else
                callback();
        }

        /// <summary>
        /// Runs one request to completion, then its callback on the calling
        /// thread.
        /// <para>
        /// The public methods are synchronous over async I/O. Blocking on a
        /// task from a UI thread (WPF, WinForms, MAUI) deadlocks when anything
        /// in it resumes on the captured SynchronizationContext, which waits
        /// for the very thread that is blocked. So the request starts with no
        /// SynchronizationContext, every await in the SDK uses
        /// ConfigureAwait(false), and the caller's context is restored before
        /// the callback runs.
        /// </para>
        /// <para>
        /// An exception thrown by the caller's own callback propagates to the
        /// caller as it was thrown; it is never reported through the failure
        /// callback or <see cref="LastApiError"/> as if the API had failed.
        /// </para>
        /// </summary>
        private static void RunSync(Func<Task> request)
        {
            var pending = new List<Action>();
            var outer = PendingCallbacks.Value;
            var context = SynchronizationContext.Current;
            PendingCallbacks.Value = pending;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                request().GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
                PendingCallbacks.Value = outer;
            }
            foreach (var callback in pending)
                callback();
        }

        /// <summary>
        /// Opens a file to upload. A missing or unreadable file is reported
        /// through the failure callback; the upload itself happens outside this
        /// check, so an exception from a callback is never mistaken for a file
        /// error.
        /// </summary>
        private bool TryOpenUpload(string filePath, Action<string> failureCallback, out FileStream file, out string fileName)
        {
            try
            {
                fileName = Path.GetFileName(filePath);
                file = File.OpenRead(filePath);
                return true;
            }
            catch (Exception e)
            {
                file = null;
                fileName = null;
                Fail(failureCallback, RedactApiKey(e.Message, _apiKey), 0);
                return false;
            }
        }

        private bool InvalidApiKey(Action<string> failureCallback)
        {
            if (_apiKey != null) return false;
            Fail(failureCallback, "ZeroBounce SDK is not initialized. " +
                                  "Please call ZeroBounce.Instance.Initialize(apiKey) first", 0);
            return true;
        }

        private bool RejectTimeout(int? timeout, bool batch, Action<string> failureCallback)
        {
            if (!timeout.HasValue)
                return false;
            var value = timeout.Value;
            if (batch)
            {
                if (value >= 10 && value <= 120)
                    return false;
                Fail(failureCallback, "Invalid parameter: timeout (must be between 10 and 120 seconds)", 0);
                return true;
            }
            if (value >= 3 && value <= 60)
                return false;
            Fail(failureCallback, "Invalid parameter: timeout (must be between 3 and 60 seconds)", 0);
            return true;
        }

        /// <summary>
        /// The callback is where the greylist retry posts its result, so it is
        /// meaningless — and rejected by the API — without the retry itself.
        /// Checked before any request is sent.
        /// </summary>
        private bool RejectCallbackUrl(ZBValidateOptions options, Action<string> failureCallback)
        {
            if (options?.CallbackUrl == null)
                return false;
            if (string.IsNullOrWhiteSpace(options.CallbackUrl))
            {
                Fail(failureCallback, "Empty parameter: callback_url", 0);
                return true;
            }
            if (options.GreylistProcessing != true)
            {
                Fail(failureCallback, "Invalid parameter: callback_url requires greylist_processing to be true", 0);
                return true;
            }
            return false;
        }

        private async Task _sendRequest<T>(string url, Action<T> successCallback, Action<string> failureCallback,
            bool messageOnlyIsFailure = false)
            where T : ZBResponse
        {
            try
            {
                var result = await _client.GetAsync(url).ConfigureAwait(false);
                await DispatchJson(result, successCallback, failureCallback, messageOnlyIsFailure).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Fail(failureCallback, RedactApiKey(HttpErrors.MessageFromBody(e.Message, 0), _apiKey), 0);
            }
        }

        private async Task _sendListRequest<T>(string url, Action<List<T>> successCallback, Action<string> failureCallback)
        {
            var status = 0;
            try
            {
                var result = await _client.GetAsync(url).ConfigureAwait(false);
                status = (int)result.StatusCode;
                var body = await ReadUsableBody(result, failureCallback).ConfigureAwait(false);
                if (body == null)
                    return;
                List<T> response;
                try
                {
                    response = JsonConvert.DeserializeObject<List<T>>(body);
                }
                catch (JsonException)
                {
                    Fail(failureCallback, "ZeroBounce returned a response that is not valid JSON.", status);
                    return;
                }
                Succeed(successCallback, response ?? new List<T>());
            }
            catch (Exception e)
            {
                Fail(failureCallback, RedactApiKey(HttpErrors.MessageFromBody(e.Message, 0), _apiKey), 0);
            }
        }

        private async Task _sendJsonRequest<T>(string url, string json, Action<T> successCallback, Action<string> failureCallback)
            where T : ZBResponse
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var result = await _client.PostAsync(url, content).ConfigureAwait(false);
                await DispatchJson(result, successCallback, failureCallback).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Fail(failureCallback, RedactApiKey(HttpErrors.MessageFromBody(e.Message, 0), _apiKey), 0);
            }
        }

        private async Task _sendFormPostRequest<T>(string url, Dictionary<string, string> form,
            Action<T> successCallback, Action<string> failureCallback)
            where T : ZBResponse
        {
            try
            {
                var content = new FormUrlEncodedContent(form);
                var result = await _client.PostAsync(url, content).ConfigureAwait(false);
                await DispatchJson(result, successCallback, failureCallback).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Fail(failureCallback, RedactApiKey(HttpErrors.MessageFromBody(e.Message, 0), _apiKey), 0);
            }
        }

        /// <param name="messageOnlyIsFailure">
        /// For a call whose result is never a bare message: the email finder and
        /// domain search. Not Validate, which answers a repeated greylist_processing
        /// request with {"Message": "Greylist validation still in progress..."} -
        /// a result, read through ZBValidateResponse.Message.
        /// </param>
        private async Task DispatchJson<T>(HttpResponseMessage result, Action<T> successCallback, Action<string> failureCallback,
            bool messageOnlyIsFailure = false)
            where T : ZBResponse
        {
            var status = (int)result.StatusCode;
            var body = await ReadUsableBody(result, failureCallback).ConfigureAwait(false);
            if (body == null)
                return;
            // LIVE: guessformat answers an invalid or drained key with an HTTP 200
            // and {"Message": "..."} alone, which used to come back as an empty
            // finder result.
            if (messageOnlyIsFailure && HttpErrors.MessageOnlyBody(body))
            {
                Fail(failureCallback, RedactApiKey(HttpErrors.MessageFromBody(body, status), _apiKey), status);
                return;
            }
            T response;
            try
            {
                response = JsonConvert.DeserializeObject<T>(body);
            }
            catch (ZBInvalidValueException e)
            {
                Fail(failureCallback, e.Message, status);
                return;
            }
            catch (JsonException)
            {
                Fail(failureCallback, RedactApiKey(HttpErrors.MessageFromBody(body, status), _apiKey), status);
                return;
            }
            if (response == null)
            {
                Fail(failureCallback, RedactApiKey(HttpErrors.MessageFromBody(body, status), _apiKey), status);
                return;
            }
            if (response is ZBValidateBatchResponse batch && ReportAccountBatchError(batch, status, failureCallback))
                return;
            Succeed(successCallback, response);
        }

        private bool ReportAccountBatchError(ZBValidateBatchResponse batch, int status, Action<string> failureCallback)
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
                Fail(failureCallback, RedactApiKey(message, _apiKey), status);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the body when it can be parsed as an API payload. On HTML, an empty body,
        /// or a non-success status, reports a short error and returns null.
        /// <para>
        /// <paramref name="allowEmptyBody"/> is for the endpoints whose success
        /// really is an empty body, such as the Inbox Placement Tester's DELETE,
        /// which answers HTTP 204 with nothing in it.
        /// </para>
        /// </summary>
        private async Task<string> ReadUsableBody(HttpResponseMessage result, Action<string> failureCallback,
            bool allowEmptyBody = false)
        {
            var body = result.Content == null ? "" : await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            var media = result.Content?.Headers.ContentType?.MediaType ?? "";
            var status = (int)result.StatusCode;
            if (allowEmptyBody && status <= 299 && string.IsNullOrWhiteSpace(body))
                return "";
            if (status > 299 || HttpErrors.ContentTypeIsHtml(media) || HttpErrors.BodyLooksLikeHtml(body) || string.IsNullOrWhiteSpace(body))
            {
                Fail(failureCallback, RedactApiKey(HttpErrors.MessageFromBody(body, status), _apiKey), status);
                return null;
            }
            // LIVE: two HTTP 200 bodies are failures, not results.
            // {"success": false} is how the API answers an unknown bulk file id.
            // {"error": "Invalid API key"}, with no "success" field at all, is how
            // it answers an invalid or drained key; deserialized, that used to
            // reach the success callback as a zeroed response.
            if (HttpErrors.SuccessMeansFailure(body) || HttpErrors.ErrorOnlyBody(body))
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
                : await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var body = Encoding.UTF8.GetString(bytes);
            var media = response.Content?.Headers.ContentType?.MediaType ?? "";
            var status = (int)response.StatusCode;

            if (status > 299 || HttpErrors.ContentTypeIsHtml(media) || HttpErrors.BodyLooksLikeHtml(body))
            {
                Fail(failureCallback, RedactApiKey(HttpErrors.MessageFromBody(body, status), _apiKey), status);
                return;
            }

            // A JSON body is never the file, whatever the content type says:
            // an unknown or unfinished file id comes back as {"success": false,
            // "message": ...} or {"error": ...} with an HTTP 200, sometimes
            // labelled text/plain or application/octet-stream. Neither is
            // written to disk, and neither is a success.
            if (media.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0 || HttpErrors.IsJson(body))
            {
                var message = HttpErrors.MessageFromBody(body, status);
                if (HttpErrors.IsJson(body) && message == body.Trim())
                    message = "ZeroBounce returned a JSON response instead of the file.";
                Fail(failureCallback, RedactApiKey(message, _apiKey), status);
                return;
            }

            var dirPath = Path.GetDirectoryName(localDownloadPath);
            if (!string.IsNullOrEmpty(dirPath)) Directory.CreateDirectory(dirPath);
            File.WriteAllBytes(localDownloadPath, bytes);
            Succeed(successCallback, new ZBGetFileResponse
            {
                Success = true,
                LocalFilePath = Path.GetFullPath(localDownloadPath)
            });
        }
    }
}
