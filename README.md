# gift-wrap
An asset bundler and minifier (CSS, JS, LESS) for ASP.Net MVC projects


## Installation

```bash
PM> Install-Package LrNet.GiftWrap
```

## Usage

In your `Global.asax` or `App_Start` folder:

```csharp
// specify where your bundles should go
GiftWrap.Bundler.RelativeBundleFolderPath = "/bundle/"; // directory that you want your bundled files to be created in

// optional: register bundle aliases at app start
Bundler.RegisterBundle("#js/foobar", // the alias of your bundle. needs to start with "#"
    // => paths to the scripts included
    "~/scripts/foo.js",
    "~/scripts/bar.js",
    // ...
);
```

In your Razor view:

```
<!doctype html>
<html>
  <head>
    <!-- render a registered bundle -->
    @Bundler.RenderStyles("#css/my-bundle")
    
    <!-- render a bundle on the fly -->
    @Bundler.RenderStyles("~/styles/foo.css", "~/styles/foo.css")
  </head>
  <body>
    <!-- ... -->
  
    
    <!-- render a registered bundle -->
    @Bundler.RenderScripts("#js/foobar")
    
    <!-- render a bundle on the fly -->
    @Bundler.RenderScripts("~/scripts/foo.js", "~/scripts/bar.js")
  </body>
</html>
```


## Features

1. In debug mode, renders files individually
2. In production, allows for a "bundle_debug" parameter to render the page unbundled.
2. Watches filesystem for changes, updates bundles automatically
3. Cache-breaking built in. Cache as aggressively as you'd like.




## How it works

Gift Wrap accepts any number of file paths to generate a bundle. When it receives the file paths, it creates a bundle key as a hash of all of the file names which ends up being a key of an in-memory dictionary which maps to the hash of the resulting compiled and concatenated documents MD5 hash. The compiled bundle is saved with a file name including the hash of it's contents (so that when one of the files changes, it would result in a different file name).  A `CacheDependency` is used to watch the files on the filesystem for changes and recompile the files.


