using System;
using System.Collections;
using SpookyStreet.Core.Localization;
using UnityEngine;
using UnityEngine.Networking;

namespace SpookyStreet.Localization
{
    /// <summary>
    /// <see cref="IPackTransport"/> over UnityWebRequest: one plain GET with no body, no custom headers and nothing
    /// about the player (engagement §2.6). HTTPS only (loopback HTTP only when a test allows it). Runs on unscaled
    /// frames, so it keeps going while the game is paused behind the language screen.
    /// </summary>
    public sealed class PackDownloader : MonoBehaviour, IPackTransport
    {
        public const int TimeoutSeconds = 30;

        /// <summary>Test fixtures only: lets a local HttpListener (http://127.0.0.1) stand in for the host.</summary>
        public bool AllowLoopbackHttp { get; set; }

        public void Get(string url, long maxBytes, Action<float> progress, Action<PackFetch> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (!PackUrls.IsAllowed(url, AllowLoopbackHttp))
            {
                done(PackFetch.Failure(PackError.NotHttps, url));
                return;
            }

            StartCoroutine(Run(url, maxBytes, progress, done));
        }

        private static IEnumerator Run(string url, long maxBytes, Action<float> progress, Action<PackFetch> done)
        {
            UnityWebRequest request;
            try
            {
                request = UnityWebRequest.Get(url);
            }
            catch (Exception e)
            {
                done(PackFetch.Failure(PackError.Offline, e.Message));
                yield break;
            }

            using (request)
            {
                request.timeout = TimeoutSeconds;
                request.redirectLimit = 2;
                UnityWebRequestAsyncOperation operation;
                try
                {
                    operation = request.SendWebRequest();
                }
                catch (InvalidOperationException e)
                {
                    // e.g. "Insecure connection not allowed" when the player settings forbid plain HTTP.
                    done(PackFetch.Failure(PackError.NotHttps, e.Message));
                    yield break;
                }

                while (!operation.isDone)
                {
                    if (maxBytes > 0 && (long)request.downloadedBytes > maxBytes)
                    {
                        request.Abort();
                        done(PackFetch.Failure(PackError.TooLarge, request.downloadedBytes + " bytes"));
                        yield break;
                    }

                    progress?.Invoke(request.downloadProgress < 0f ? 0f : request.downloadProgress);
                    yield return null;
                }

                byte[] bytes = request.downloadHandler != null ? request.downloadHandler.data : null;
                switch (request.result)
                {
                    case UnityWebRequest.Result.Success:
                        progress?.Invoke(1f);
                        done(PackFetch.Success(bytes));
                        break;
                    case UnityWebRequest.Result.ConnectionError:
                        bool partial = bytes != null && bytes.Length > 0;
                        done(PackFetch.Failure(partial ? PackError.Interrupted : PackError.Offline, request.error, bytes));
                        break;
                    default:
                        done(PackFetch.Failure(PackError.Offline, request.responseCode + " " + request.error));
                        break;
                }
            }
        }
    }
}
