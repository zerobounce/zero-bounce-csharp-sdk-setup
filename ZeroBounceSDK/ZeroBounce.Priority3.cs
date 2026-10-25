using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZeroBounceSDK
{
    public partial class ZeroBounce
    {
        private const string BulkApiRootUrl = "https://bulkapi.zerobounce.net";
        private const string EmailFinderBulkBaseUrl = BulkApiRootUrl + "/email-finder";
        private const string DomainSearchBulkBaseUrl = BulkApiRootUrl + "/domain-search";

        /// <summary>
        /// The list evaluator and the Inbox Placement Tester have their own base
        /// URLs, not paths built from the bulk one. Override them with
        /// <see cref="SetListEvaluatorBaseUrl"/> and
        /// <see cref="SetInboxPlacementBaseUrl"/>; overriding the main base URL
        /// alone leaves them pointing at the live API.
        /// </summary>
        private string _listEvaluatorBaseUrl = BulkApiRootUrl + "/v2/listevaluator";

        private string _inboxPlacementBaseUrl = BulkApiRootUrl + "/api/ipt";

        /// <summary>Overrides the list evaluator base URL.</summary>
        public void SetListEvaluatorBaseUrl(string url)
        {
            if (url == null) return;
            var trimmed = url.TrimEnd('/');
            RequireHttpsUrl(trimmed);
            _listEvaluatorBaseUrl = trimmed;
        }

        /// <summary>Overrides the Inbox Placement Tester base URL.</summary>
        public void SetInboxPlacementBaseUrl(string url)
        {
            if (url == null) return;
            var trimmed = url.TrimEnd('/');
            RequireHttpsUrl(trimmed);
            _inboxPlacementBaseUrl = trimmed;
        }

        public class EmailFinderSendFileOptions
        {
            public bool? HasHeaderRow;
            public int FirstNameColumn;
            public int LastNameColumn;
            public int MiddleNameColumn;
            public int FullNameColumn;
        }

        public class DomainSearchSendFileOptions
        {
            public bool? HasHeaderRow;
        }

        /// <summary>
        /// The fields of <see cref="ValidateS3File"/>. <see cref="FileName"/> and
        /// <see cref="ReturnUrl"/> are required; the rest are sent only when set.
        /// </summary>
        public class ValidateS3FileOptions
        {
            public string FileName;
            public bool? HasHeaderRow;
            public bool? RemoveDuplicates;
            public int? EmailAddressColumn;
            public string ReturnUrl;
            public int FirstNameColumn;
            public int LastNameColumn;
            public int GenderColumn;
            public int IpAddressColumn;
            public bool? AllowPhase2;
        }

        /// <summary>
        /// The fields of <see cref="ScoreS3File"/>. <see cref="FileName"/> and
        /// <see cref="ReturnUrl"/> are required; the rest are sent only when set.
        /// </summary>
        public class ScoreS3FileOptions
        {
            public string FileName;
            public bool? HasHeaderRow;
            public bool? RemoveDuplicates;
            public int? EmailAddressColumn;
            public string ReturnUrl;
        }

        public void SendEmailFinderFile(string filePath, int domainColumn, EmailFinderSendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                using (var file = File.OpenRead(filePath))
                {
                    SendEmailFinderFile(file, fileName, domainColumn, options, successCallback, failureCallback);
                }
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(e.Message, _apiKey));
            }
        }

        /// <summary>
        /// Queues the bulk email finder on an uploaded file (POST /email-finder/sendfile).
        /// <para>
        /// Besides domain_column the endpoint needs a name to work from, so
        /// either first_name_column or full_name_column must be set. Neither is
        /// checked by the server until the job runs, so the SDK rejects the
        /// call before the upload.
        /// </para>
        /// </summary>
        public void SendEmailFinderFile(Stream file, string fileName, int domainColumn, EmailFinderSendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (domainColumn < 1)
            {
                failureCallback("Empty parameter: domain_column");
                return;
            }
            if (options == null || (options.FirstNameColumn < 1 && options.FullNameColumn < 1))
            {
                failureCallback("Empty parameter: first_name_column or full_name_column required");
                return;
            }
            var fields = BuildEmailFinderFields(domainColumn, options);
            _SendBulkRootMultipart(EmailFinderBulkBaseUrl + "/sendfile", file, fileName, fields, successCallback, failureCallback)
                .Wait();
        }

        public void EmailFinderFileStatus(string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootFileStatus(EmailFinderBulkBaseUrl, fileId, successCallback, failureCallback);
        }

        public void EmailFinderGetFile(string fileId, string localDownloadPath,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootGetFile(EmailFinderBulkBaseUrl, fileId, localDownloadPath, successCallback, failureCallback).Wait();
        }

        public void EmailFinderDeleteFile(string fileId,
            Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootDeleteFile(EmailFinderBulkBaseUrl, fileId, successCallback, failureCallback);
        }

        public void SendDomainSearchFile(string filePath, int domainColumn, DomainSearchSendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                using (var file = File.OpenRead(filePath))
                {
                    SendDomainSearchFile(file, fileName, domainColumn, options, successCallback, failureCallback);
                }
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(e.Message, _apiKey));
            }
        }

        public void SendDomainSearchFile(Stream file, string fileName, int domainColumn, DomainSearchSendFileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (domainColumn < 1)
            {
                failureCallback("Empty parameter: domain_column");
                return;
            }
            var fields = BuildDomainSearchFields(domainColumn, options);
            _SendBulkRootMultipart(DomainSearchBulkBaseUrl + "/sendfile", file, fileName, fields, successCallback, failureCallback)
                .Wait();
        }

        public void DomainSearchFileStatus(string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootFileStatus(DomainSearchBulkBaseUrl, fileId, successCallback, failureCallback);
        }

        public void DomainSearchGetFile(string fileId, string localDownloadPath,
            Action<ZBGetFileResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootGetFile(DomainSearchBulkBaseUrl, fileId, localDownloadPath, successCallback, failureCallback).Wait();
        }

        public void DomainSearchDeleteFile(string fileId,
            Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootDeleteFile(DomainSearchBulkBaseUrl, fileId, successCallback, failureCallback);
        }

        public void SendListEvaluatorFile(string filePath, int emailAddressColumn,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                using (var file = File.OpenRead(filePath))
                {
                    SendListEvaluatorFile(file, fileName, emailAddressColumn, successCallback, failureCallback);
                }
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(e.Message, _apiKey));
            }
        }

        public void SendListEvaluatorFile(Stream file, string fileName, int emailAddressColumn,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (emailAddressColumn < 1)
            {
                failureCallback("Empty parameter: email_address_column");
                return;
            }
            var fields = new Dictionary<string, string>
            {
                { "email_address_column", emailAddressColumn.ToString() }
            };
            _SendBulkRootMultipart(_listEvaluatorBaseUrl + "/", file, fileName, fields, successCallback, failureCallback)
                .Wait();
        }

        public void GetListEvaluatorResult(string fileId, Action<JObject> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (string.IsNullOrWhiteSpace(fileId))
            {
                failureCallback("Empty parameter: file_id");
                return;
            }
            // The file id is one path segment, so a slash in it must stay encoded.
            _sendJsonCallback(
                _listEvaluatorBaseUrl + "/" + Enc(fileId.Trim()) + "/?api_key=" + Enc(_apiKey),
                successCallback,
                failureCallback).Wait();
        }

        /// <summary>
        /// Submits a finished evaluation for validation (POST /v2/send-evaluated-file/).
        /// <para>
        /// LIVE: this takes the list evaluator's file_id as a form field, not
        /// the file upload the docs describe - an upload is rejected with
        /// "file_id: This field is required". It returns a new validation
        /// job's file_id, which answers "File cannot be found" for a short
        /// while after it is accepted.
        /// </para>
        /// </summary>
        /// <param name="fileId">the file_id returned by the list evaluator</param>
        public void SendEvaluatedFile(string fileId,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                failureCallback("Empty parameter: file_id");
                return;
            }
            _PostBulkRootForm("/v2/send-evaluated-file/",
                new Dictionary<string, string> { { "file_id", fileId.Trim() } },
                successCallback, failureCallback).Wait();
        }

        public void AddFilter(string rule, string target, string value,
            Action<JObject> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (string.IsNullOrWhiteSpace(rule) || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(value))
            {
                failureCallback("Empty parameter: rule, target, and value are required");
                return;
            }
            var form = new Dictionary<string, string>
            {
                { "api_key", _apiKey },
                { "rule", rule },
                { "target", target },
                { "value", value }
            };
            _sendFormJsonCallback(_apiBaseUrl + "/filters/add", form, successCallback, failureCallback).Wait();
        }

        public void ListFilters(bool usePost, Action<JToken> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (usePost)
            {
                var form = new Dictionary<string, string> { { "api_key", _apiKey } };
                _sendFormJsonTokenCallback(_apiBaseUrl + "/filters/list", form, successCallback, failureCallback).Wait();
            }
            else
            {
                _sendJsonTokenCallback(
                    _apiBaseUrl + "/filters/list?api_key=" + Enc(_apiKey),
                    successCallback,
                    failureCallback).Wait();
            }
        }

        public void DeleteFilter(string rule, string target, string value,
            Action<JObject> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (string.IsNullOrWhiteSpace(rule) || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(value))
            {
                failureCallback("Empty parameter: rule, target, and value are required");
                return;
            }
            var form = new Dictionary<string, string>
            {
                { "api_key", _apiKey },
                { "rule", rule },
                { "target", target },
                { "value", value }
            };
            _sendFormJsonCallback(_apiBaseUrl + "/filters/delete", form, successCallback, failureCallback).Wait();
        }

        public void ListS3Files(string bucket, string search,
            Action<List<ZBS3ListFileEntry>> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            var query = BulkApiRootUrl + "/listS3files?api_key=" + Enc(_apiKey);
            if (!string.IsNullOrWhiteSpace(bucket))
                query += "&bucket=" + Enc(bucket);
            if (!string.IsNullOrWhiteSpace(search))
                query += "&search=" + Enc(search);
            _sendListRequest<ZBS3ListFileEntry>(query, successCallback, failureCallback).Wait();
        }

        /// <summary>
        /// Validates a file in your Amazon S3 bucket (POST /validateS3file).
        /// <para>
        /// LIVE: return_url is required, as it is on every other S3 and SFTP
        /// job. Without it the API answers HTTP 400 "Provided return URL is not
        /// permitted: None", so the SDK rejects the call before sending it.
        /// </para>
        /// </summary>
        public void ValidateS3File(ValidateS3FileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (options == null)
            {
                failureCallback("Empty parameter: file_name");
                return;
            }
            _BulkRootFileJob("/validateS3file", options.FileName, options.HasHeaderRow,
                options.RemoveDuplicates, options.ReturnUrl, BuildValidateS3ExtraFields(options),
                successCallback, failureCallback);
        }

        /// <summary>
        /// Scores a file in your Amazon S3 bucket (POST /scoreS3file).
        /// <para>
        /// LIVE: return_url is required here too. It was never sent at all
        /// before, which made this call impossible to complete.
        /// </para>
        /// </summary>
        public void ScoreS3File(ScoreS3FileOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (options == null)
            {
                failureCallback("Empty parameter: file_name");
                return;
            }
            var extra = new Dictionary<string, string>();
            if (options.EmailAddressColumn.HasValue)
                extra["email_address_column"] = options.EmailAddressColumn.Value.ToString();
            _BulkRootFileJob("/scoreS3file", options.FileName, options.HasHeaderRow,
                options.RemoveDuplicates, options.ReturnUrl, extra, successCallback, failureCallback);
        }

        public void ExportS3File(string fileId, string exportBucket,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                failureCallback("Empty parameter: file_id");
                return;
            }
            if (exportBucket != null && string.IsNullOrWhiteSpace(exportBucket))
            {
                failureCallback("Empty parameter: export_bucket");
                return;
            }
            // Without an export_bucket the one from the account's settings is used.
            var fields = new Dictionary<string, string> { { "file_id", fileId } };
            if (exportBucket != null)
                fields["export_bucket"] = exportBucket;
            _PostBulkRootForm("/exportS3file", fields, successCallback, failureCallback).Wait();
        }

        /// <summary>
        /// The documented fields shared by the six S3 and SFTP/FTP file jobs.
        /// <para>
        /// <see cref="FileName"/> is required by every job except
        /// <see cref="ValidateSFTPFile"/>, where the docs list it as optional:
        /// leaving it null there uses the input bucket configured in your
        /// ZeroBounce settings.
        /// </para>
        /// </summary>
        public class BulkFileJobOptions
        {
            public string FileName;
            public bool HasHeaderRow = true;
            public bool? RemoveDuplicates;
            public string ReturnUrl;
        }

        /// <summary>Runs the email finder on a file in your Amazon S3 bucket (POST /emailFinderS3file).</summary>
        public void EmailFinderS3File(BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootFileJob("/emailFinderS3file", options, successCallback, failureCallback);
        }

        /// <summary>Runs domain search on a file in your Amazon S3 bucket (POST /domainSearchS3file).</summary>
        public void DomainSearchS3File(BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootFileJob("/domainSearchS3file", options, successCallback, failureCallback);
        }

        // SFTP / FTP bulk. The server, path and protocol are never sent by the
        // SDK; they come from your ZeroBounce settings.

        /// <summary>
        /// Validates a file from your SFTP/FTP server (POST /validateSFTPfile).
        /// <para>
        /// LIVE: file_name is required here too, although the docs list it as
        /// optional for this one endpoint.
        /// </para>
        /// </summary>
        public void ValidateSFTPFile(BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootFileJob("/validateSFTPfile", options, successCallback, failureCallback);
        }

        /// <summary>Scores a file from your SFTP/FTP server (POST /scoreSFTPfile).</summary>
        public void ScoreSFTPFile(BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootFileJob("/scoreSFTPfile", options, successCallback, failureCallback);
        }

        /// <summary>Runs the email finder on a file from your SFTP/FTP server (POST /emailFinderSFTPfile).</summary>
        public void EmailFinderSFTPFile(BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootFileJob("/emailFinderSFTPfile", options, successCallback, failureCallback);
        }

        /// <summary>Runs domain search on a file from your SFTP/FTP server (POST /domainSearchSFTPfile).</summary>
        public void DomainSearchSFTPFile(BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            _BulkRootFileJob("/domainSearchSFTPfile", options, successCallback, failureCallback);
        }

        /// <summary>
        /// Exports a validated or scored file back to your SFTP/FTP server
        /// (POST /exportSFTPfile).
        /// <para>
        /// Note the field name: this endpoint takes export_path, not the
        /// export_bucket that <see cref="ExportS3File"/> sends. Pass null to
        /// use the export path from your ZeroBounce settings.
        /// </para>
        /// </summary>
        public void ExportSFTPFile(string fileId, string exportPath,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                failureCallback("Empty parameter: file_id");
                return;
            }
            if (exportPath != null && string.IsNullOrWhiteSpace(exportPath))
            {
                failureCallback("Empty parameter: export_path");
                return;
            }
            var fields = new Dictionary<string, string> { { "file_id", fileId } };
            if (exportPath != null)
                fields["export_path"] = exportPath;
            _PostBulkRootForm("/exportSFTPfile", fields, successCallback, failureCallback).Wait();
        }

        /// <summary>
        /// POSTs one of the six documented S3 / SFTP file jobs on the bulkapi
        /// root. Booleans go on the wire as the strings "true"/"false", and an
        /// option the caller left unset is not sent at all.
        /// </summary>
        /// <summary>
        /// POSTs one of the six documented S3 / SFTP file jobs on the bulkapi root.
        /// <para>
        /// LIVE: file_name and return_url are required on every one of them,
        /// validateSFTPfile included, although the docs list file_name as
        /// optional for that one. Without either the API answers
        /// "This field is required".
        /// </para>
        /// </summary>
        private void _BulkRootFileJob(string path, BulkFileJobOptions options,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (options == null)
            {
                failureCallback("Empty parameter: file_name");
                return;
            }
            // has_header_row is not nullable here, so it always goes on the wire.
            _BulkRootFileJob(path, options.FileName, options.HasHeaderRow, options.RemoveDuplicates,
                options.ReturnUrl, null, successCallback, failureCallback);
        }

        /// <summary>
        /// The shared body of every S3 / SFTP file job: the required file_name
        /// and return_url check, the documented fields, and anything extra the
        /// individual job takes. A null <paramref name="hasHeaderRow"/> or
        /// <paramref name="removeDuplicates"/> is not sent at all.
        /// </summary>
        private void _BulkRootFileJob(string path, string fileName, bool? hasHeaderRow, bool? removeDuplicates,
            string returnUrl, Dictionary<string, string> extraFields,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                failureCallback("Empty parameter: file_name");
                return;
            }
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                failureCallback("Empty parameter: return_url");
                return;
            }

            var fields = new Dictionary<string, string>
            {
                { "file_name", fileName },
                { "return_url", returnUrl }
            };
            PutOptionalBool(fields, "has_header_row", hasHeaderRow);
            PutOptionalBool(fields, "remove_duplicates", removeDuplicates);
            if (extraFields != null)
            {
                foreach (var entry in extraFields)
                    fields[entry.Key] = entry.Value;
            }

            _PostBulkRootForm(path, fields, successCallback, failureCallback).Wait();
        }

        private static Dictionary<string, string> BuildEmailFinderFields(int domainColumn, EmailFinderSendFileOptions options)
        {
            var fields = new Dictionary<string, string> { { "domain_column", domainColumn.ToString() } };
            if (options == null) return fields;
            if (options.HasHeaderRow.HasValue)
                fields["has_header_row"] = options.HasHeaderRow.Value ? "true" : "false";
            if (options.FirstNameColumn > 0)
                fields["first_name_column"] = options.FirstNameColumn.ToString();
            if (options.LastNameColumn > 0)
                fields["last_name_column"] = options.LastNameColumn.ToString();
            if (options.MiddleNameColumn > 0)
                fields["middle_name_column"] = options.MiddleNameColumn.ToString();
            if (options.FullNameColumn > 0)
                fields["full_name_column"] = options.FullNameColumn.ToString();
            return fields;
        }

        private static Dictionary<string, string> BuildDomainSearchFields(int domainColumn, DomainSearchSendFileOptions options)
        {
            var fields = new Dictionary<string, string> { { "domain_column", domainColumn.ToString() } };
            if (options?.HasHeaderRow.HasValue == true)
                fields["has_header_row"] = options.HasHeaderRow.Value ? "true" : "false";
            return fields;
        }

        /// <summary>
        /// The fields validateS3file takes on top of the ones every S3 / SFTP
        /// job shares. file_name, return_url, has_header_row and
        /// remove_duplicates are added by <see cref="_BulkRootFileJob"/>.
        /// </summary>
        private static Dictionary<string, string> BuildValidateS3ExtraFields(ValidateS3FileOptions options)
        {
            var fields = new Dictionary<string, string>();
            if (options.EmailAddressColumn.HasValue)
                fields["email_address_column"] = options.EmailAddressColumn.Value.ToString();
            if (options.FirstNameColumn > 0)
                fields["first_name_column"] = options.FirstNameColumn.ToString();
            if (options.LastNameColumn > 0)
                fields["last_name_column"] = options.LastNameColumn.ToString();
            if (options.GenderColumn > 0)
                fields["gender_column"] = options.GenderColumn.ToString();
            if (options.IpAddressColumn > 0)
                fields["ip_address_column"] = options.IpAddressColumn.ToString();
            if (options.AllowPhase2.HasValue)
                fields["allow_phase_2"] = options.AllowPhase2.Value ? "true" : "false";
            return fields;
        }

        private static void PutOptionalBool(IDictionary<string, string> fields, string key, bool? value)
        {
            if (value.HasValue)
                fields[key] = value.Value ? "true" : "false";
        }

        private async Task _SendBulkRootMultipart(string url, Stream file, string fileName,
            Dictionary<string, string> fields,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            try
            {
                var content = new MultipartFormDataContent();
                foreach (var entry in fields)
                    content.Add(new StringContent(entry.Value), entry.Key);
                content.Add(new StringContent(_apiKey), "api_key");
                content.Add(new StreamContent(file), "file", SafeMultipartFileName(fileName));

                var result = await _client.PostAsync(url, content);
                await DispatchJson(result, successCallback, failureCallback);
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(e.Message, _apiKey));
            }
        }

        private async Task _PostBulkRootForm(string path, Dictionary<string, string> fields,
            Action<ZBSendFileResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            fields["api_key"] = _apiKey;
            try
            {
                var content = new FormUrlEncodedContent(fields);
                var result = await _client.PostAsync(BulkApiRootUrl + path, content);
                await DispatchJson(result, successCallback, failureCallback);
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(e.Message, _apiKey));
            }
        }

        private void _BulkRootFileStatus(string baseUrl, string fileId,
            Action<ZBFileStatusResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (string.IsNullOrWhiteSpace(fileId))
            {
                failureCallback("Empty parameter: file_id");
                return;
            }
            _sendRequest(
                baseUrl + "/filestatus?api_key=" + Enc(_apiKey) + "&file_id=" + Enc(fileId),
                successCallback,
                failureCallback).Wait();
        }

        private void _BulkRootDeleteFile(string baseUrl, string fileId,
            Action<ZBDeleteFileResponse> successCallback, Action<string> failureCallback)
        {
            if (InvalidApiKey(failureCallback)) return;
            if (string.IsNullOrWhiteSpace(fileId))
            {
                failureCallback("Empty parameter: file_id");
                return;
            }
            _sendRequest(
                baseUrl + "/deletefile?api_key=" + Enc(_apiKey) + "&file_id=" + Enc(fileId),
                successCallback,
                failureCallback).Wait();
        }

        private async Task _BulkRootGetFile(string baseUrl, string fileId, string localDownloadPath,
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
                var url = baseUrl + "/getfile?api_key=" + Enc(_apiKey) + "&file_id=" + Enc(fileId);
                var response = await _client.GetAsync(url);
                await DeliverDownloadedFile(response, localDownloadPath, successCallback, failureCallback);
            }
            catch (Exception e)
            {
                failureCallback(RedactApiKey(e.Message, _apiKey));
            }
        }

        private async Task _sendJsonCallback(string url, Action<JObject> successCallback, Action<string> failureCallback)
        {
            try
            {
                var result = await _client.GetAsync(url);
                var body = await ReadUsableBody(result, failureCallback);
                if (body == null)
                    return;
                var token = JToken.Parse(body);
                if (token is JObject obj)
                    successCallback(obj);
                else
                    failureCallback("ZeroBounce returned an unexpected JSON response.");
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

        private async Task _sendJsonTokenCallback(string url, Action<JToken> successCallback, Action<string> failureCallback)
        {
            try
            {
                var result = await _client.GetAsync(url);
                var body = await ReadUsableBody(result, failureCallback);
                if (body == null)
                    return;
                successCallback(JToken.Parse(body));
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

        private async Task _sendFormJsonCallback(string url, Dictionary<string, string> form,
            Action<JObject> successCallback, Action<string> failureCallback)
        {
            try
            {
                var content = new FormUrlEncodedContent(form);
                var result = await _client.PostAsync(url, content);
                var body = await ReadUsableBody(result, failureCallback);
                if (body == null)
                    return;
                var token = JToken.Parse(body);
                if (token is JObject obj)
                    successCallback(obj);
                else
                    failureCallback("ZeroBounce returned an unexpected JSON response.");
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

        private async Task _sendFormJsonTokenCallback(string url, Dictionary<string, string> form,
            Action<JToken> successCallback, Action<string> failureCallback)
        {
            try
            {
                var content = new FormUrlEncodedContent(form);
                var result = await _client.PostAsync(url, content);
                var body = await ReadUsableBody(result, failureCallback);
                if (body == null)
                    return;
                successCallback(JToken.Parse(body));
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
    }
}
