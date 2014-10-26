---
layout: _view/home
title: Misakai Baker - Static Site Generator for .NET
swatch: black-yellow
swatchalt: white-black
headline: Baker
subheadline: Static Site Generator in C#
features:
   - {
        title: "Markdown and Razor", 
   		  url: "#",
   		  img: "/img/custom-icon-composer.png",
   		  text: "Combine Markdown and Razor view engine, layouts, sections and helpers."
     }
   - {
        title: "Minifiers", 
   		  url: "#",
   		  img: "/img/custom-icon-envelope-large.png",
   		  text: "Various optimizations: HTML minifier, CSS minifier, JavaScript minifier and PNG optimizer."
     }
   - {
        title: "Simple Pipeline", 
   		  url: "#",
   		  img: "/img/custom-icon-cogs.png",
   		  text: "Elegant design using a pipeline model for processors and various combinations."
     }
   - {
        title: "Rapid Development", 
   		  url: "#",
   		  img: "/img/custom-icon-multilanguage.png",
   		  text: "Integrated web server for testing and effective development cycle support with live page reload."
     }
---


@section Headline
{
  Bake your website with all optimizations:
  ```
  Baker.exe --bake c:\Project
  ```
}


@section TwoColumn
{
  In order to launch and serve the static website in a in-process webserver, use 'serve' command. This will watch any modifications you make to the files and auto-update and reload your browser for you. The default port is 8080, so please open a browser on 127.0.0.1:8080 once you started.

  ```
  Baker.exe --serve c:\Project
  ```
}

@section Features
{
  More features and extensible pipeline. Clone and extend!
}


