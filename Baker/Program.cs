using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Baker.Providers;

namespace Baker
{
    class Program
    {
        static void Main(string[] args)
        {
            var project = SiteProject.FromDisk(@"..\..\..\Test\");


            var files = project
                .Provider
                .Fetch()
                .Except("_site*");

            MarkdownProcessor.Default
                .Next(HtmlMinifier.Default)
                .Process(files.Only("*.md"))
                .Write();

            RazorProcessor.Default
                .Process(files.Only("*.cshtml"))
                .Write();

            CssMinifier.Default
                .Process(files.Only("*.css"))
                .Write();

            JavaScriptMinifier.Default
                .Process(files.Only("*.js"))
                .Write();

            PngOptimizer.Default
                .Process(files.Only("*.png"))
                .Write();

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
