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
           
            var files = new DiskAssetProvider(@"..\..\..\Test\")
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

            //files.TryProcess<IAssetTemplate>("*.cshtml", RazorProcessor.Default);
            //files.TryProcess<IAssetFile>("*.md", MarkdownProcessor.Default);
            //files.TryProcess<IAssetFile>("*.css", CssMinifier.Default);
            //files.TryProcess<IAssetFile>("*.js", JavaScriptMinifier.Default);

 

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
