using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Baker
{
    public class SiteWatcher : Spike.DisposableObject
    {
        private readonly FileSystemWatcher Watcher = new FileSystemWatcher();
        private readonly SiteProject Project;
        private Action Callback;
        private Timer Timer;
        private DateTime LastFired = DateTime.MinValue;
        private DateTime LastReal = DateTime.MinValue;

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
            this.Timer = new Timer(this.OnTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Bind the watcher.
        /// </summary>
        /// <param name="onChange"></param>
        public void Bind(Action onChange)
        {
            this.Callback = onChange;
            this.Watcher.EnableRaisingEvents = true;
            this.Timer.Change(0, 100);
        }

        /// <summary>
        /// Cleans up the resources and disposes properly.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            this.Watcher.EnableRaisingEvents = false;
            this.Watcher.Changed -= OnChanged;
            this.Watcher.Created -= OnChanged;
            this.Timer.Dispose();
        }

        string LastFile;
        private void OnChanged(object sender, FileSystemEventArgs args)
        {
            var fullPath = args.FullPath;
            if (fullPath.Contains(this.Project.Configuration.Destination))
                return;

            if (args.FullPath == LastFile)
            {
                LastFile = String.Empty;
                return;
            }

            this.LastReal = DateTime.Now;
            this.LastFile = args.FullPath;
        }

        private void OnTick(object state)
        {
            // If nothing happened, return
            if (this.LastReal + TimeSpan.FromMilliseconds(5000) < this.LastFired)
                return;

            // Fire with a delay
            //if (this.LastFired < this.LastReal + TimeSpan.FromMilliseconds(5000))
           // {
                // Fire the callback
                this.LastFired = DateTime.Now;
                this.Callback();
            //}
        }


    }
}
