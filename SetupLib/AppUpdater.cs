﻿using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Net.Http;

namespace SetupLib
{
    public class AppUpdater
    {
        public string currentVersion;
        private readonly GitHubClient client;

        public AppUpdater(string currentVersion)
        {
            client = new GitHubClient(new Octokit.ProductHeaderValue("VKUI3"));
            this.currentVersion = currentVersion;
        }

        // Определите делегат и событие
        public delegate void DownloadProgressChangedEventHandler(object sender, DownloadProgressChangedEventArgs e);
        public event DownloadProgressChangedEventHandler DownloadProgressChanged;

        // Определите класс аргументов события
        public class DownloadProgressChangedEventArgs : EventArgs
        {
            public long TotalBytes { get; set; }
            public long BytesDownloaded { get; set; }
            public double Percentage { get; set; }
        }

        public string version;
        public string Name { get; private set; }
        public string Tit { get; private set; }
        public int sizeFile;
        public string UriDownload { get; private set; }
        public string UriDownloadMSIX { get; private set; }

        public async Task<bool> CheckForUpdates()
        {
            var releases = await client.Repository.Release.GetAll("MaKrotos", "VKUI3");
            var latestRelease = releases[0];

            if (latestRelease.TagName != currentVersion)
            {
                Console.WriteLine($"New version available: {latestRelease.TagName}");

                this.version = latestRelease.TagName;
                this.Name = latestRelease.Name;
                this.Tit = latestRelease.Body.ToString();

                // Ищем файлы .cer и .msix в активах
                var cerAsset = latestRelease.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".cer"));
                var msixAsset = latestRelease.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".msix"));

                if (cerAsset != null && msixAsset != null)
                {
                    this.sizeFile = msixAsset.Size;
                    this.UriDownload = cerAsset.BrowserDownloadUrl;
                    this.UriDownloadMSIX = msixAsset.BrowserDownloadUrl;
                    return true;
                }
            }

            Console.WriteLine("Your app is up to date.");
            return false;
        }

        public async Task DownloadAndOpenFile()
        {
            var httpClient = new HttpClient();



            // Сначала скачиваем и устанавливаем сертификат
            using (var response = await httpClient.GetAsync(UriDownload, HttpCompletionOption.ResponseHeadersRead))
            using (var streamToReadFrom = await response.Content.ReadAsStreamAsync())
            {
                var path = Path.Combine(Path.GetTempPath(), Path.GetFileName(UriDownload));
                using (var streamToWriteTo = File.Create(path))
                {
                    var totalRead = 0L;
                    var buffer = new byte[8192];
                    var isMoreToRead = true;

                    do
                    {
                        var read = await streamToReadFrom.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0)
                        {
                            isMoreToRead = false;
                        }
                        else
                        {
                            await streamToWriteTo.WriteAsync(buffer, 0, read);

                            totalRead += read;
                            var percentage = totalRead * 100d / response.Content.Headers.ContentLength.Value;

                            // Вызовите событие
                            DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs
                            {
                                TotalBytes = response.Content.Headers.ContentLength.Value,
                                BytesDownloaded = totalRead,
                                Percentage = percentage
                            });
                        }
                    } while (isMoreToRead);
                }

                // Установка сертификата
                X509Certificate2 cert = new X509Certificate2(path);
                X509Store store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);

                // Проверка на существование сертификата и его срок действия
                bool certificateExists = store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).Count > 0;
                if (!certificateExists || cert.NotAfter <= DateTime.Now)
                {
                    store.Add(cert);
                }

                store.Close();
            }


            // Затем скачиваем файл .msix

            using (var response = await httpClient.GetAsync(UriDownloadMSIX))
            using (var streamToReadFrom = await response.Content.ReadAsStreamAsync())
            {
                var path = Path.Combine(Path.GetTempPath(), Path.GetFileName(UriDownloadMSIX));
                using (var streamToWriteTo = File.Create(path))
                {
                    var totalRead = 0L;
                    var buffer = new byte[8192];
                    var isMoreToRead = true;

                    do
                    {
                        var read = await streamToReadFrom.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0)
                        {
                            isMoreToRead = false;
                        }
                        else
                        {
                            await streamToWriteTo.WriteAsync(buffer, 0, read);

                            totalRead += read;
                            var percentage = totalRead * 100d / response.Content.Headers.ContentLength.Value;

                            // Вызовите событие
                            DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs
                            {
                                TotalBytes = response.Content.Headers.ContentLength.Value,
                                BytesDownloaded = totalRead,
                                Percentage = percentage
                            });
                        }
                    } while (isMoreToRead);
                }

                System.Diagnostics.Process process = new System.Diagnostics.Process();
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/C start " + path;
                process.StartInfo = startInfo;
                process.Start();
            }
        }
    }
}
