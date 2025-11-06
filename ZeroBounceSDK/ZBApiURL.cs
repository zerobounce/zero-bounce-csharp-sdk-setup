using System;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public enum ZBApiURL
    {
        ApiDefaultURL,
        ApiUSAURL,
        ApiEUURL
    }

    public class ZBApiURLConverter
    {
        public static string GetApiURLString(ZBApiURL zbApiUrl)
        {
            switch (zbApiUrl)
            {
                case ZBApiURL.ApiDefaultURL:
                    return "https://api.zerobounce.net/v2";
                case ZBApiURL.ApiUSAURL:
                    return "https://api-us.zerobounce.net/v2";
                case ZBApiURL.ApiEUURL:
                    return "https://api-eu.zerobounce.net/v2";
                default:
                    return "https://api.zerobounce.net/v2";
            }
        }
    }
}