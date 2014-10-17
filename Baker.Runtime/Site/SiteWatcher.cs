using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spike;

namespace Baker.Site
{
    public class SiteWatcher : DisposableObject
    {
        private readonly FileSystemWatcher Watcher = new FileSystemWatcher();
        private readonly SiteProject Project;
        private Action<string> Callback;

        /// <summary>
        /// Constructs a new watcher.
        /// </summary>
        /// <param name="project">The project to watch.</param>
        public SiteWatcher(SiteProject project)
        {
            this.Project = project;
            this.Watcher.EnableRaisingEvents = false;
            this.Watcher.Path = this.Project.Directory.FullName;
            this.Watcher.Filter = "*.*";
            this.Watcher.IncludeSubdirectories = true;
            this.Watcher.Changed += OnChanged;
            this.Watcher.Created += OnChanged;
        }

        /// <summary>
        /// Bind the watcher.
        /// </summary>
        /// <param name="onChange"></param>
        public void Bind(Action<string> onChange)
        {
            this.Callback = onChange;
            this.Watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Cleans up the resources and disposes properly.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            Watcher.EnableRaisingEvents = false;
            Watcher.Changed -= OnChanged;
            Watcher.Created -= OnChanged;
        }

        string lastFile;
        private void OnChanged(object sender, FileSystemEventArgs args)
        {
            var fullPath = args.FullPath;
            if (fullPath.Contains(this.Project.Configuration.Destination))
                return;

            if (args.FullPath == lastFile)
            {
                lastFile = String.Empty;
                return;
            }

            Callback(args.FullPath);
            lastFile = args.FullPath;
        }
    }
}
