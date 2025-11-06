using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZeroBounce
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
        protected HttpClient _client = new HttpClient();
        private string _apiKey;
        private string _apiBaseUrl = ZBApiURLConverter.GetApiURLString(ZBApiURL.ApiDefaultURL);

        public void Initialize(string apiKey)
        {
            _apiKey = apiKey;
        }

        public void Initialize(string apiKey, ZBApiURL apiBaseUrl)
        {
            _apiKey = apiKey;
            _apiBaseUrl = ZBApiURLConverter.GetApiURLString(apiBaseUrl);
        }

        /// <param name="email">The email address you want to validate</param>
        /// <param name="ipAddress"> The IP Address the email signed up from (Can be blank).</param>
        /// <param name="successCallback"> The success callback function, called with a ZBValidateResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void Validate(string email, string ipAddress, Action<ZBValidateResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                _apiBaseUrl + "/validate?api_key=" + _apiKey + "&email=" + email + "&ip_address=" + (ipAddress ?? ""),
                successCallback,
                failureCallback).Wait(); 
        }

        /// <param name="emailBatch">A list of ZBValidateEmailRow objects</param>
        /// <param name="successCallback"> The success callback function, called with a ZBValidateResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        public void ValidateBatch(List<ZBValidateEmailRow> emailBatch, Action<ZBValidateBatchResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            var requestData = new ZBValidateEmailRequest { ApiKey = _apiKey, EmailBatch = emailBatch };
            var json = JsonConvert.SerializeObject(requestData);

            _sendJsonRequest(
                BulkApiBaseUrl + "/validatebatch",
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

            _sendRequest(_apiBaseUrl + "/getcredits?api_key=" + _apiKey,
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
                _apiBaseUrl + "/getapiusage?api_key=" + _apiKey
                + "&start_date=" + startDate.ToString("yyyy-MM-dd")  
                + "&end_date=" + endDate.ToString("yyyy-MM-dd"),
                successCallback,
                failureCallback).Wait();
        }

        /// <param name="email">The email address for which we are interested in getting the engagement</param>
        public void GetActivity(string email, Action<ZBActivityResponse> successCallback,
            Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            _sendRequest(
                _apiBaseUrl + "/activity?api_key=" + _apiKey
                + "&email=" + email,
                successCallback,
                failureCallback).Wait();
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

            string url = _apiBaseUrl + "/guessformat?api_key=" + _apiKey;
            if (domain != null) {
                url += "&domain=" + (domain ?? "");
            }
            if (companyName != null) {
                url += "&company_name=" + (companyName ?? "");
            }
            if (firstName != null) {
                url += "&first_name=" + (firstName ?? "");
            }
            if (middleName != null) {
                url += "&middle_name=" + (middleName ?? "");
            }
            if (lastName != null) {
                url += "&last_name=" + (lastName ?? "");
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

            string url = _apiBaseUrl + "/guessformat?api_key=" + _apiKey;
            if (domain != null) {
                url += "&domain=" + (domain ?? "");
            }
            if (companyName != null) {
                url += "&company_name=" + (companyName ?? "");
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
                _apiBaseUrl + "/guessformat?api_key=" + _apiKey
                + "&domain=" + (domain ?? "")
                + "&first_name=" + (firstName ?? "")
                + "&middle_name=" + (middleName ?? "")
                + "&last_name=" + (lastName ?? ""),
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

            _sendRequest(
                BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/filestatus?api_key=" + _apiKey + "&file_id=" + fileId,
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
        }
        
        /// <summary>
        /// The scoringSendfile API allows user to send a file for bulk email validation
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void ScoringSendFile(string filePath, SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _SendFile(true, filePath, options, successCallback, failureCallback).Wait();
        }

        /// <summary>
        /// The sendfile API allows user to send a file for bulk email validation
        /// <param name="successCallback"> The success callback function, called with a ZBSendFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void SendFile(string filePath, SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _SendFile(false, filePath, options, successCallback, failureCallback).Wait();
        }

        private async Task _SendFile(bool scoring, string filePath, SendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            
            try
            {
                var content = new MultipartFormDataContent();
                var file = File.OpenRead(filePath);
                content.Add(new StreamContent(file), "file", Path.GetFileName(filePath));

                content.Add(new StringContent(_apiKey), "api_key");

                if (options != null)
                {
                    content.Add(new StringContent(options.EmailAddressColumn.ToString()), "email_address_column");
                    if (options.ReturnUrl != null)
                        content.Add(new StringContent(options.ReturnUrl), "return_url");
                    if (options.FirstNameColumn > 0)
                        content.Add(new StringContent(options.FirstNameColumn.ToString()), "first_name_column");
                    if (options.LastNameColumn > 0)
                        content.Add(new StringContent(options.LastNameColumn.ToString()), "last_name_column");
                    if (options.GenderColumn > 0)
                        content.Add(new StringContent(options.GenderColumn.ToString()), "gender_column");
                    if (options.IpAddressColumn > 0)
                        content.Add(new StringContent(options.IpAddressColumn.ToString()), "ip_address_column");
                    if (options.HasHeaderRow)
                        content.Add(new StringContent("true"), "has_header_row");
                    if (options.RemoveDuplicate)
                        content.Add(new StringContent("true"), "remove_duplicate");
                }

                var url = BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/sendfile";
                var result = await _client.PostAsync(url, content);
                var responseString = await result.Content.ReadAsStringAsync();
                var response = JsonConvert.DeserializeObject<ZBSendFileResponse>(responseString);
                file.Close();
                successCallback(response);
            }
            catch (Exception e)
            {
                failureCallback(e.Message);
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
            _GetFile(true, fileId, localDownloadPath, successCallback, failureCallback).Wait();
        }
        
        /// <summary>
        /// The getfile API allows users to get the validation results file for the file been submitted using sendfile API
        /// <param name="successCallback"> The success callback function, called with a ZBGetFileResponse object</param>
        /// <param name="failureCallback"> The failure callback function, called with a string error message</param>
        /// </summary>
        public void GetFile(string fileId, string localDownloadPath, 
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            _GetFile(false, fileId, localDownloadPath, successCallback, failureCallback).Wait();
        }

        private async Task _GetFile(bool scoring, string fileId, string localDownloadPath, 
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;

            try
            {
                var url = BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/getfile?api_key=" + _apiKey + "&file_id=" + fileId;
                var stream = await _client.GetStreamAsync(url);
               
                var dirPath = Path.GetDirectoryName(localDownloadPath);
                if(dirPath != null & dirPath != "") Directory.CreateDirectory(dirPath);
                var fileStream = new FileStream(localDownloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                    true);
                await stream.CopyToAsync(fileStream);
                fileStream.Close();
                successCallback(new ZBGetFileResponse
                {
                    LocalFilePath = Path.GetFullPath(localDownloadPath)
                });
            }
            catch (Exception e)
            {
                failureCallback(e.Message);
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

            _sendRequest(
                BulkApiBaseUrl + (scoring ? "/scoring" : "") + "/deletefile?api_key=" + _apiKey + "&file_id=" + fileId,
                successCallback,
                failureCallback).Wait();
        }

        private bool InvalidApiKey(Action<string> failureCallback)
        {
            if (_apiKey != null) return false;
            failureCallback("ZeroBounce SDK is not initialized. " +
                            "Please call ZeroBounce.Instance.Initialize(apiKey) first");
            return true;
        }

        private async Task _sendRequest<T>(string url, Action<T> successCallback, Action<string> failureCallback)
            where T : ZBResponse
        {
            try
            {
                var responseString = await _client.GetStringAsync(url);
                //Debug.WriteLine("sendRequest response: "+responseString);
                var response = JsonConvert.DeserializeObject<T>(responseString);
                successCallback(response);
            }
            catch (Exception e)
            {
                failureCallback(e.Message);
            }
        }

        private async Task _sendJsonRequest<T>(string url, string json, Action<T> successCallback, Action<string> failureCallback)
            where T : ZBResponse
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var result = await _client.PostAsync(url, content);
                string responseString = await result.Content.ReadAsStringAsync();
                var response = JsonConvert.DeserializeObject<T>(responseString);
                successCallback(response);
            }
            catch (Exception e)
            {
                failureCallback(e.Message);
            }
        }
    }
}
