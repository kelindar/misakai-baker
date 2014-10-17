using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spike;
using Spike.Network.Http;

namespace Baker
{
    /// <summary>
    /// Handles HTTP Requests
    /// </summary>
    internal class SiteHandler : IHttpHandler
    {
        private readonly SiteProject Project;
        private readonly HttpMimeMap Mime = new HttpMimeMap();
        private SiteWatcher Watcher;

        /// <summary>
        /// Constructs a new handler.
        /// </summary>
        /// <param name="project"></param>
        public SiteHandler(SiteProject project)
        {
            this.Project = project;
            Service.Started += () =>
            {
                // Bind the watcher
                this.Watcher = new SiteWatcher(project);
                this.Watcher.Bind((file) =>
                {
                    var info = new FileInfo(file);
                    if (!info.Exists)
                        return;

                    Tracing.Info("Change", info.Name);

                    this.Project.Update();

                    // Create a new asset
                    /*var asset = new AssetInputFile(this.Project, info);

                    // Print and process
                    this.Project.Process(new AssetInputFile[]{ asset })*/;
                });
            };
        }

        #region IHttpHandler Members
        /// <summary>
        /// Checks whether the handler can handle the incoming request.
        /// </summary>
        public bool CanHandle(HttpContext context, HttpVerb verb, string url)
        {
            FileInfo file;
            return TryGet(url, out file);
        }

        /// <summary>
        /// Processes the incoming request.
        /// </summary>
        public void ProcessRequest(HttpContext context)
        {
            FileInfo file;
            if(TryGet(context.Request.Path, out file))
            {
                // Return with the appropriate content-type
                context.Response.ContentType = Mime.GetMime(file.Extension);
                context.Response.Write(
                    File.ReadAllBytes(file.FullName)
                    );
            }
        }

        #endregion

        #region Private Members
        /// <summary>
        /// Tries to fetch the file for a particular URL.
        /// </summary>
        private bool TryGet(string url, out FileInfo file)
        {
            // Wrap in the info
            try
            {
                if (url.EndsWith("/"))
                    url += "index.html";
                if (url.StartsWith("/"))
                    url = url.Substring(1);
                var path = Path.Combine(this.Project.Directory.FullName, this.Project.Configuration.Destination, url)
                    .Replace("\\", "/");
                file = new FileInfo(path);
                return file.Exists;
            }
            catch
            {
                // Failed
                file = null;
                return false;
            }
        }
        #endregion
    }
}
