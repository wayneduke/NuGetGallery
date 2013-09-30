﻿using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IPackageFileService
    {
        /// <summary>
        ///     Creates an ActionResult that allows a third-party client to download the nupkg for the package.
        /// </summary>
        UriOrStream GetDownloadUriOrStream(Package package);

        /// <summary>
        ///     Creates an ActionResult that allows a third-party client to download the nupkg for the package.
        /// </summary>
        UriOrStream GetDownloadUriOrStream(string unsafeId, string unsafeVersion);

        /// <summary>
        ///     Deletes the nupkg from the file storage.
        /// </summary>
        Task DeletePackageFileAsync(string id, string version);

        /// <summary>
        ///     Saves the contents of the package represented by the stream into the file storage.
        /// </summary>
        Task SavePackageFileAsync(Package package, Stream packageFile);

        /// <summary>
        ///     Downloads the package from the file storage and reads it into a Stream asynchronously.
        /// </summary>
        Task<Stream> DownloadPackageFileAsync(Package package);
    }
}