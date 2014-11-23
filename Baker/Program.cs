using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Baker.Processors;
using Baker.Providers;

namespace Baker
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = new Options();
            if (args.Length == 0)
                Console.Write(options.GetUsage());

            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                try
                {
                    if (options.Bake != null)
                    {
                        // Bake is requested
                        SiteProject.Bake(options.Bake);
                    }
                    else if (options.Serve != null)
                    {
                        // Serve is requested
                        SiteProject.Serve(options.Serve);
                    }
                    else if (Debugger.IsAttached)
                    {
                        // Under debugger, just serve
                        SiteProject.Serve(@"..\..\..\Test\");
                    }
                }
                catch(Exception ex)
                {
                    Tracing.Error("Baker", ex.Message);
                }

            }
        }
            
    }
}
