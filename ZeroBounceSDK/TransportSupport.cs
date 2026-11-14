using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

namespace ZeroBounceSDK
{
    /// <summary>
    /// Transport and download helpers shared by the Bulk, Priority 3 and Warmup
    /// clients: the HTTP handler, the multipart file part, the download target
    /// checks and the body sniffing that keeps an error from being saved as a file.
    /// </summary>
    internal static class TransportSupport
    {
        /// <summary>How long opening a connection may take before the call fails.</summary>
        internal static TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The handler every client uses: no redirects, and a connect timeout where
        /// the platform has one. On .NET Core / .NET 5+ that is
        /// SocketsHttpHandler.ConnectTimeout, found by reflection because the
        /// library targets netstandard2.0. On .NET Framework there is no connect
        /// timeout to set; only the 120 s HttpClient.Timeout applies there.
        /// </summary>
        internal static HttpMessageHandler CreateHandler()
        {
            try
            {
                var type = Type.GetType("System.Net.Http.SocketsHttpHandler, System.Net.Http", false);
                if (type != null)
                {
                    var handler = Activator.CreateInstance(type);
                    type.GetProperty("AllowAutoRedirect")?.SetValue(handler, false);
                    var connect = type.GetProperty("ConnectTimeout");
                    if (connect != null && handler is HttpMessageHandler http)
                    {
                        connect.SetValue(handler, ConnectTimeout);
                        return http;
                    }
                }
            }
            catch (Exception)
            {
                // Fall back to the portable handler below.
            }
            return new HttpClientHandler { AllowAutoRedirect = false };
        }

        /// <summary>Strips a leading UTF-8 byte order mark (U+FEFF) that a decoder kept.</summary>
        internal static string StripBom(string text)
        {
            return text != null && text.Length > 0 && text[0] == '﻿' ? text.TrimStart('﻿') : text;
        }

        /// <summary>application/json, or any structured-syntax +json type.</summary>
        internal static bool IsJsonMediaType(string media)
        {
            if (string.IsNullOrEmpty(media)) return false;
            var m = media.Trim();
            return m.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                || m.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Whether a download body is JSON (an error), by its media type or by sniffing it after a BOM.</summary>
        internal static bool IsJsonDownload(string media, string body)
        {
            return IsJsonMediaType(media) || HttpErrors.IsJson(StripBom(body));
        }

        /// <summary>
        /// Why a download target cannot be used, or null. Checked before any
        /// request is sent, and creates nothing.
        /// </summary>
        internal static string RefuseDownloadTarget(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "Empty parameter: local_download_path";
            var last = path[path.Length - 1];
            if (last == '/' || last == '\\' || last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
                return "local_download_path must be a file path, not a directory (it ends with a path separator)";
            try
            {
                var full = Path.GetFullPath(path);
                if (Directory.Exists(full))
                    return "local_download_path is an existing directory; give a file path";
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
            {
                return "local_download_path is not a valid file path";
            }
            return null;
        }

        /// <summary>Follows a symbolic link at <paramref name="target"/> to the file it points at (where the platform can), so a write goes through it.</summary>
        internal static string ResolveLink(string target)
        {
            try
            {
                var info = new FileInfo(target);
                if (!info.Exists && !Directory.Exists(target) && (info.Attributes == (FileAttributes)(-1))) return target;
                if ((info.Attributes & FileAttributes.ReparsePoint) == 0) return target;
                var method = typeof(FileSystemInfo).GetMethod("ResolveLinkTarget", new[] { typeof(bool) });
                if (method == null) return target;
                var resolved = method.Invoke(info, new object[] { true }) as FileSystemInfo;
                return resolved != null ? resolved.FullName : target;
            }
            catch (Exception)
            {
                return target;
            }
        }

        /// <summary>The Unix mode of <paramref name="path"/> as a boxed UnixFileMode, or null (Windows, .NET Framework, .NET before 7).</summary>
        internal static object GetUnixMode(string path)
        {
            try
            {
                var get = typeof(File).GetMethod("GetUnixFileMode", new[] { typeof(string) });
                return get == null ? null : get.Invoke(null, new object[] { path });
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Best effort: gives <paramref name="path"/> the mode returned by <see cref="GetUnixMode"/>.</summary>
        internal static void SetUnixMode(string path, object mode)
        {
            if (mode == null) return;
            try
            {
                var set = typeof(File).GetMethod("SetUnixFileMode", new[] { typeof(string), mode.GetType() });
                set?.Invoke(null, new[] { path, mode });
            }
            catch (Exception)
            {
                // Keeping the mode is best effort; the file content is what matters.
            }
        }

        /// <summary>
        /// Adds the upload's file part with the name as raw UTF-8 bytes in
        /// filename="..." and no filename* parameter (as the Java and Android
        /// SDKs do); the framework would send a non-ASCII name as an RFC 2047
        /// =?utf-8?B?...?= word. The header text is the UTF-8 bytes read as
        /// Latin-1, which the HTTP stack writes back as those same bytes.
        /// </summary>
        internal static void AddFilePart(MultipartFormDataContent content, Stream file, string safeFileName)
        {
            var part = new StreamContent(file);
            var raw = Encoding.GetEncoding("ISO-8859-1").GetString(Encoding.UTF8.GetBytes(safeFileName));
            part.Headers.TryAddWithoutValidation("Content-Disposition",
                "form-data; name=\"file\"; filename=\"" + raw + "\"");
            content.Add(part);
        }
    }
}
