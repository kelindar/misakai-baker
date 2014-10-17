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
           
            var files = new DiskAssetProvider(@"c:\Workspace\Misakai.Baker\Test\")
                .Fetch();


            
            MarkdownProcessor.Default
                .Next(HtmlMinifier.Default)
                .Process(files.Filter("*.md"))
                .Write();

            RazorProcessor.Default
                .Process(files.Filter("*.cshtml"))
                .Write();

            CssMinifier.Default
                .Process(files.Filter("*.css"))
                .Write();

            JavaScriptMinifier.Default
                .Process(files.Filter("*.js"))
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
