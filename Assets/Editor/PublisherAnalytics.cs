//
//  PublisherAnalytics.cs
//
//  Publisher Analytics
//  https://github.com/SpaceMadness/publisher-analytics
//
//  Copyright 2017 Alex Lementuev, SpaceMadness.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;

using UnityEngine;
using UnityEditor;

namespace SpaceMadness
{
    public static class PublisherAnalytics
    {
        public static readonly string kDisableApplicationTracking = "DisableApplicationTracking";

        /// <summary>
        /// Basic raw GET request URL for Google Analytics
        /// </summary>
        private static readonly string kTrackingURL = "https://www.google-analytics.com/collect";

        private const int kUndefinedValue = int.MinValue;

        /// <summary>
        /// Basic app specific payload for the GET request
        /// </summary>
        private static string defaultPayload;

        public static void Initialize(string trackingId, string packageVersion, IDictionary<string, bool> configuration = null)
        {
            if (string.IsNullOrEmpty(trackingId))
            {
                throw new ArgumentException("Tracking id is null or empty");
            }

            if (string.IsNullOrEmpty(packageVersion))
            {
                throw new ArgumentException("Package version is null or empty");
            }

            // Create shared package specific payload
            defaultPayload = CreateDefaultPayload(trackingId, packageVersion, configuration ?? new Dictionary<string, bool>());

            // Track package version update (if any)
            TrackPackageVersionUpdate(trackingId, packageVersion);
        }

        /// <summary>
        /// Notifies the server about plugin update.
        /// </summary>
        private static void TrackPackageVersionUpdate(string trackingId, string version)
        {
            var prefsKey = "Com.SpaceMadness.PublisherAnalytics." + trackingId + ".LastKnownPackageVersion";
            var lastKnownVersion = EditorPrefs.GetString(prefsKey);
            if (lastKnownVersion != version)
            {
                EditorPrefs.SetString(prefsKey, version);
                TrackEvent("Version", "updated_version");
            }
        }

        public static void TrackEvent(string category, string action, int value = kUndefinedValue)
        {
            if (defaultPayload == null)
            {
                Debug.LogWarningFormat("Can't track event '{0}': instance is not initialized", action);
                return;
            }

            var payloadStr = CreatePayload(category, action, value);
            if (payloadStr != null)
            {
                Log.d("Event track payload: " + payloadStr);

                PublisherHttpClient downloader = new PublisherHttpClient(kTrackingURL);
                downloader.UploadData(payloadStr, delegate(string result, Exception error)
                {
                    if (error != null)
                    {
                        Log.e("Event track failed: " + error);
                    }
                    else
                    {
                        Log.d("Event track result: " + result);
                    }
                });
            }
        }

        private static string CreatePayload(string category, string action, int value)
        {
            var payload = new StringBuilder(defaultPayload);
            payload.AppendFormat("&ec={0}", WWW.EscapeURL(category));
            payload.AppendFormat("&ea={0}", WWW.EscapeURL(action));
            if (value != kUndefinedValue)
            {
                payload.AppendFormat("&ev={0}", value.ToString());
            }

            return payload.ToString();
        }

        private static string CreateDefaultPayload(string trackingId, string packageVersion, IDictionary<string, bool> configuration)
        {
            var payload = new StringBuilder("v=1&t=event");
            payload.AppendFormat("&tid={0}", trackingId);
            payload.AppendFormat("&cid={0}", WWW.EscapeURL(SystemInfo.deviceUniqueIdentifier));
            payload.AppendFormat("&ua={0}", WWW.EscapeURL(SystemInfo.operatingSystem));
            payload.AppendFormat("&av={0}", WWW.EscapeURL(packageVersion));
            #if UNITY_EDITOR
            payload.AppendFormat("&ds={0}", "editor");
            #else
            payload.AppendFormat("&ds={0}", "player");
            #endif

            bool disableAppTracking;
            configuration.TryGetValue(kDisableApplicationTracking, out disableAppTracking);

            if (!disableAppTracking)
            {
                if (!string.IsNullOrEmpty(Application.productName))
                {
                    var productName = WWW.EscapeURL(Application.productName);
                    if (productName.Length <= 100)
                    {
                        payload.AppendFormat("&an={0}", productName);
                    }
                }

                #if UNITY_5_6_OR_NEWER
                var identifier = Application.identifier;
                #else
                var identifier = Application.bundleIdentifier;
                #endif
                if (!string.IsNullOrEmpty(identifier))
                {
                    var bundleIdentifier = WWW.EscapeURL(identifier);
                    if (bundleIdentifier.Length <= 150)
                    {
                        payload.AppendFormat("&aid={0}", bundleIdentifier);
                    }
                }
                if (!string.IsNullOrEmpty(Application.companyName))
                {
                    var companyName = WWW.EscapeURL(Application.companyName);
                    if (companyName.Length <= 150)
                    {
                        payload.AppendFormat("&aiid={0}", companyName);
                    }
                }
            }

            return payload.ToString();
        }
    }

    delegate void LunarConsoleHttpDownloaderCallback(string result,Exception error);

    class PublisherHttpClient
    {
        private Uri m_uri;
        private WebClient m_client;

        #if UNITY_EDITOR_WIN
        
        static PublisherHttpClient()
        {
            ServicePointManager.ServerCertificateValidationCallback = MyRemoteCertificateValidationCallback;
        }

        private static bool MyRemoteCertificateValidationCallback(System.Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                for (int i = 0; i < chain.ChainStatus.Length; i++)
                {
                    if (chain.ChainStatus[i].Status != X509ChainStatusFlags.RevocationStatusUnknown)
                    {
                        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                        chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                        bool chainIsValid = chain.Build((X509Certificate2)certificate);
                        if (!chainIsValid)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        #endif // UNITY_EDITOR_WIN

        public PublisherHttpClient(string uri)
            : this(new Uri(uri))
        {
        }

        public PublisherHttpClient(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("Uri is null");
            }

            m_uri = uri;
            m_client = new WebClient();
        }

        public void UploadData(string data, LunarConsoleHttpDownloaderCallback callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("Callback is null");
            }

            if (m_client == null)
            {
                throw new InvalidOperationException("Already downloading something");
            }

            if (callback != null)
            {
                m_client.UploadStringCompleted += (object sender, UploadStringCompletedEventArgs e) => callback(e.Result, e.Error);
            }

            m_client.UploadStringAsync(m_uri, data);
        }
    }

    static class Log
    {
        public static void d(string message)
        {
            Debug.Log(message);
        }

        public static void e(string message)
        {
            Debug.LogError(message);
        }
    }
}