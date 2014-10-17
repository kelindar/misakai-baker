using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Baker.Text;
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
        private readonly string   ReloadScript;
        private readonly string[] ReloadExtensions = new string[]{
            ".htm", ".html"
        };

        /// <summary>
        /// Constructs a new handler.
        /// </summary>
        /// <param name="project"></param>
        public SiteHandler(SiteProject project)
        {
            this.Project = project;
            this.ReloadScript = GetScript();
            
            Service.Started += () =>
            {
                // Bind the watcher
                this.Watcher = new SiteWatcher(project);
                this.Watcher.Bind(() =>
                {
                    this.Project.Update();
                });
            };
        }

        #region IHttpHandler Members
        /// <summary>
        /// Checks whether the handler can handle the incoming request.
        /// </summary>
        public bool CanHandle(HttpContext context, HttpVerb verb, string url)
        {
            if (url == "/last-update")
                return true;

            FileInfo file;
            return TryGet(url, out file);
        }

        /// <summary>
        /// Processes the incoming request.
        /// </summary>
        public void ProcessRequest(HttpContext context)
        {
            var url = context.Request.Path;
            if (url == "/last-update")
            {
                // Print out last update time
                context.Response.ContentType = "text/plain";
                context.Response.Write(this.Project.LastUpdate);
            }
            else
            {
                // Load a resource
                FileInfo file;
                if (TryGet(url, out file))
                {
                    // Return with the appropriate content-type
                    context.Response.ContentType = Mime.GetMime(file.Extension);
                    context.Response.Write(
                        File.ReadAllBytes(file.FullName)
                        );

                    // We should add the script
                    if (ReloadExtensions.Contains(file.Extension))
                    {
                        // Script
                        var script = Encoding.UTF8.GetBytes(
                            "<script>" +
                            "window._update = '" + this.Project.LastUpdate + "';" +
                            this.ReloadScript +
                            "</script>");

                        // Write the script
                        context.Response.Write(script);
                    }
                }
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

        /// <summary>
        /// Gets the script from the assembly.
        /// </summary>
        /// <returns></returns>
        private string GetScript()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Baker.Diagnostics.Reload.js";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                var minifier = new Minifier();
                return minifier.MinifyJavaScript(reader.ReadToEnd());
            }
        }
        #endregion
    }
}
