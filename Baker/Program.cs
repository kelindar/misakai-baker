using System;
using System.Collections.Generic;
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

            SiteProject.Bake(@"..\..\..\Test\");
        }
    }
}
