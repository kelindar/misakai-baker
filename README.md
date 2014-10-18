Misakai Baker
=============

The aim of this project is to write a flexible and extensible static website generator for C# / .Net people.

[![Build status](https://ci.appveyor.com/api/projects/status/h89p713jb1fkuthv?svg=true)](https://ci.appveyor.com/project/Kelindar/misakai-baker)

Features
========
* Combine Markdown + Razor view engine, layouts, sections and helpers
* Jekyll-like headers for the model
* Various optimizations: HTML minifier, CSS minifier, JavaScript minifier and PNG optimizer
* Pipeline model for processors and various combinations
* Yaml configuration file
* Integrated web server for testing
* File watcher and live reload for development cycle updates
 

Usage
=====

In order to build the final project with all optimizations on:

```
Baker.exe --bake c:\Project
```


In order to launch and serve the static website in a in-process webserver, use 'serve' command. This will watch any modifications you make to the files and auto-update and reload your browser for you. The default port is 8080, so please open a browser on 127.0.0.1:8080 once you started.

```
Baker.exe --serve c:\Project
```

Planned Features
================
* CSS and JavaScript unifier (merging multiple files into one big file for performance)
* Translations/localization support
* Jpeg optimizer
* Configurable pipeline directly from _config.yaml file
