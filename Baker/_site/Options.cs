using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace Baker
{
    class Options
    {
        [Option('s', "serve", Required = false, HelpText = "Starts a development in-process web server and builds the project with live-reload.")]
        public string Serve { get; set; }

        [Option('b', "bake", Required = false, HelpText = "Builds an optimized version of the static website.")]
        public string Bake { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
