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
        private DateTime ChangeCurrent = DateTime.MinValue;
        private bool ChangeFire = false;

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

            this.ChangeCurrent = DateTime.Now;
            this.ChangeFire = true;
            this.LastFile = args.FullPath;
        }

        private void OnTick(object state)
        {
            if (!this.ChangeFire)
                return;

            var now = DateTime.Now;
            if (now < this.ChangeCurrent + TimeSpan.FromMilliseconds(350))
                return;

            // We fired
            this.ChangeFire = false;
            this.Callback();
        }


    }
}
