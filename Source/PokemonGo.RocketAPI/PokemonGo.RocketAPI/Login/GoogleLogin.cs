﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PokemonGo.RocketAPI.Helpers;
using PokemonGo.RocketAPI.Logging;

namespace PokemonGo.RocketAPI.Login
{
    public class GoogleLogin : ILoginProvider
    {
        private const string OauthTokenEndpoint = "https://www.googleapis.com/oauth2/v4/token";
        private const string OauthEndpoint = "https://accounts.google.com/o/oauth2/device/code";
        private const string ClientId = "848232511240-73ri3t7plvk96pj4f85uj8otdat2alem.apps.googleusercontent.com";
        private const string ClientSecret = "NCjF1TLi2CcY6t5mt0ZveuL7";

        public DeviceCodeModel DeviceCode { get; set; }
        public string RefreshToken { get; set; }

        public async Task<TokenHolder> Authorize()
        {
            TokenResponseModel response = null;

            if (DeviceCode == null && string.IsNullOrEmpty(RefreshToken))
            {
                var deviceResponse = await GetDeviceCode();
                DeviceCode = deviceResponse;
            }

            if (!string.IsNullOrEmpty(RefreshToken))
            {
                response = await GetAccessToken(RefreshToken);
            }
            else if (DeviceCode != null)
            {
                response = await GetAccessToken(DeviceCode);
            }
            else
            {
                throw new Exception("No DeviceCode or RefreshToken were given!");
            }
            
            return new TokenHolder { AccessToken = response.access_token, RefreshToken = response.refresh_token, IdToken = response.id_token, TokenType = response.token_type, ExpiresIn = response.expires_in };
        }

        #region Static methods
        private static async Task<DeviceCodeModel> GetDeviceCode()
        {
            var deviceCode = await HttpClientHelper.PostFormEncodedAsync<DeviceCodeModel>(OauthEndpoint,
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("scope", "openid email https://www.googleapis.com/auth/userinfo.email"));

            Logger.Write($"Please visit {deviceCode.verification_url} and enter {deviceCode.user_code}", LogLevel.None);
            return deviceCode;
        }

        private static async Task<TokenResponseModel> PollSubmittedToken(string deviceCode)
        {
            return await HttpClientHelper.PostFormEncodedAsync<TokenResponseModel>(OauthTokenEndpoint,
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret),
                new KeyValuePair<string, string>("code", deviceCode),
                new KeyValuePair<string, string>("grant_type", "http://oauth.net/grant_type/device/1.0"),
                new KeyValuePair<string, string>("scope", "openid email https://www.googleapis.com/auth/userinfo.email"));
        }

        public static async Task<TokenResponseModel> GetAccessToken(string refreshToken)
        {
            return await HttpClientHelper.PostFormEncodedAsync<TokenResponseModel>(OauthTokenEndpoint,
                new KeyValuePair<string, string>("access_type", "offline"),
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("scope", "openid email https://www.googleapis.com/auth/userinfo.email"));
        }

        private static async Task<TokenResponseModel> GetAccessToken(DeviceCodeModel deviceCode)
        {
            //Poll until user submitted code..
            TokenResponseModel tokenResponse;
            do
            {
                await Task.Delay(2000);
                tokenResponse = await PollSubmittedToken(deviceCode.device_code);
            } while (tokenResponse.access_token == null || tokenResponse.refresh_token == null);

            return tokenResponse;
        }
        #endregion

        internal class ErrorResponseModel
        {
            public string error { get; set; }
            public string error_description { get; set; }
        }

        public class TokenResponseModel
        {
            public string access_token { get; set; }
            public string token_type { get; set; }
            public int expires_in { get; set; }
            public string refresh_token { get; set; }
            public string id_token { get; set; }
        }

        public class DeviceCodeModel
        {
            public string verification_url { get; set; }
            public int expires_in { get; set; }
            public int interval { get; set; }
            public string device_code { get; set; }
            public string user_code { get; set; }
        }
    }
}
