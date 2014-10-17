using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spike.Network.Http;

namespace Baker
{
    /// <summary>
    /// Handles HTTP Requests
    /// </summary>
    internal class SiteHandler : IHttpHandler
    {
        private readonly SiteProject Site;
        private readonly HttpMimeMap Mime = new HttpMimeMap();

        /// <summary>
        /// Constructs a new handler.
        /// </summary>
        /// <param name="site"></param>
        public SiteHandler(SiteProject site)
        {
            this.Site = site;
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
                var path = Path.Combine(this.Site.Directory.FullName, this.Site.Configuration.Destination, url)
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
