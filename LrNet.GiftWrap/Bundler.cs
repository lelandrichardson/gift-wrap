using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using dotless.Core;
using dotless.Core.configuration;
using dotless.Core.Loggers;
using Microsoft.Ajax.Utilities;
using File = System.IO.File;


namespace LrNet.GiftWrap
{
    public class Bundler
    {
        private static object locker = new object();


        private static string _relativeFolderPath;
        private static string _absoluteFolderPath;
        private static string _relativeSharedFolderPath;
        private static string _absoluteSharedFolderPath;

#if DEBUG
        private const bool DEBUG = true;
#else
        private const bool DEBUG =  false;
#endif

        private static readonly Minifier _Minifier;
        private static readonly DotlessConfiguration _lessConfig;

        static Bundler()
        {

            _lessConfig = new DotlessConfiguration
            {
                MinifyOutput = !DEBUG,
                CacheEnabled = !DEBUG,
                ImportAllFilesAsLess = true,
                DisableVariableRedefines = false
            };

            if (DEBUG)
            {
                _lessConfig.Logger = typeof(DiagnosticsLogger);
            }

            _Minifier = new Minifier();
        }

        public static string RelativeBundleFolderPath
        {
            get { return _relativeFolderPath; }
            set
            {
                _relativeFolderPath = value;
                _absoluteFolderPath = HostingEnvironment.MapPath("~" + value);
            }
        }

        public static string RelativeSharedBundleFolderPath
        {
            get { return _relativeSharedFolderPath; }
            set
            {
                _relativeSharedFolderPath = value;
                _absoluteSharedFolderPath = HostingEnvironment.MapPath("~" + value);
            }
        }

        public static string BundlePrefix { get; set; }


        private static string HASH(string input)
        {
            return Hashing.Hash(input, Hashing.HashingTypes.MD5).ToLowerInvariant();
        }

        /// <summary>
        /// Given a list of relative paths, outputs a cache key
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static string KeyForPaths(IEnumerable<string> paths)
        {
            return HASH(string.Join("|", paths));
        }


        private static string UrlFromTag(string tag, AssetType type)
        {
            return VirtualPathUtility.Combine(_relativeFolderPath, tag + type.FileExtension());
        }

        private static string AbsolutePathFromTag(string tag, AssetType type)
        {
            return Path.Combine(_absoluteFolderPath, tag + type.FileExtension());
        }

        private static string AbsoluteSharedPathFromTag(string tag, AssetType type)
        {
            return Path.Combine(_absoluteSharedFolderPath, tag + type.FileExtension());
        }

        private static List<string> NormalizePaths(IEnumerable<string> paths)
        {
            // we want to check the first path to see if it is a bundle name, rather than a path...
            var first = paths.FirstOrDefault();

            // bundle names must start with "#"
            if (first != null && first.StartsWith("#"))
            {
                var bundlePaths = BundleMap[first];
                return bundlePaths ?? new List<string>();
            }
            // NOTE: we are choosing to be case-insensitive here. Should help ensure we have
            // NOTE: optimum cacheability
            return paths.Select(x => x.ToLowerInvariant()).ToList();
        }


        private static string ConstructTag(IEnumerable<string> absolutePaths, AssetType resultType)
        {
            var content = new StringBuilder();

            foreach (var path in absolutePaths)
            {
                var type = path.ToAssetType();
                try
                {
                    using (var stream = new StreamReader(path, Encoding.UTF8))
                    {
                        content.Append(ProcessFile(stream, type));
                    }

                    content.Append(type.ConcatenationToken());
                }
                catch (FileNotFoundException e)
                {
                    if (DEBUG)
                    {
                        throw;
                    }
                }
                catch (Exception e)
                {
                    var message = string.Format("Error occurred processing file {0}", path);
                    if (DEBUG)
                    {
                        throw new Exception(message, e);
                    }
                }
            }

            // get the text content of the resulting file
            var result = content.ToString();

            // the tag is the MD5 hash of the resulting file's content
            // plus config setting for version of the file, which we can increment when we need to circumvent cached files
            var tag = (BundlePrefix ?? "") + HASH(result);

            // path for the file to be written
            Write(AbsolutePathFromTag(tag, resultType), result);

            // if multi-node environment, may want to save to a shared non-local file as well...
            if (_absoluteSharedFolderPath != null)
            {
                Write(AbsoluteSharedPathFromTag(tag, resultType), result);
            }

            return tag;
        }

        private static void Write(string path, string content)
        {
            // it's possible that the same file is being created concurrently by two separate threads
            lock (locker)
            {
                // write the file statically to the server
                if (!File.Exists(path))
                {
                    try
                    {
                        File.WriteAllText(path, content);
                    }
                    catch (Exception e)
                    {
                        if (DEBUG)
                        {
                            throw;
                        }
                    }

                }
            }
        }

        private static string ProcessFile(StreamReader stream, AssetType type)
        {
            switch (type)
            {
                case AssetType.Css:
                    return _Minifier.MinifyStyleSheet(stream.ReadToEnd(),
                        new CssSettings()
                        {
                            CommentMode = CssComment.None
                        });
                case AssetType.Less:
                    return Less.Parse(stream.ReadToEnd(), _lessConfig);
                case AssetType.JavaScript:
                    return _Minifier.MinifyJavaScript(stream.ReadToEnd(),
                        new CodeSettings()
                        {
                            EvalTreatment = EvalTreatment.MakeImmediateSafe,
                            PreserveImportantComments = false
                        });
                default:
                    return stream.ReadToEnd();
            }
        }


        public static string GetTag(IEnumerable<string> paths, AssetType type)
        {
            var lowerPaths = NormalizePaths(paths);
            var key = KeyForPaths(lowerPaths);
            var tag = HttpRuntime.Cache[key] as string;

            if (tag == null)
            {
                // get the absolute paths of the files
                var absolutePaths = lowerPaths.Select(HostingEnvironment.MapPath).ToArray();

                tag = ConstructTag(absolutePaths, type);

                HttpRuntime.Cache.Insert(key, tag, new CacheDependency(absolutePaths));
            }

            return tag;
        }


        public static string RenderStyles(IEnumerable<string> paths)
        {
            if (DEBUG || HttpContext.Current.Request["bundle_debug"] != null)
            {
                return string.Join("\n",
                    NormalizePaths(paths)
                        //.Select(url => UrlFactory.StaticContent(url))
                        .Select(url => string.Format("<link rel=\"stylesheet\" href=\"{0}\" />", url))
                    );
            }
            else
            {
                var tag = GetTag(paths, AssetType.Css);
                var relativePath = UrlFromTag(tag, AssetType.Css);

                return string.Format("<link rel=\"stylesheet\" href=\"{0}\" />", relativePath);
            }
        }

        public static string RenderScripts(IEnumerable<string> paths)
        {
            if (DEBUG || HttpContext.Current.Request["bundle_debug"] != null)
            {
                return string.Join("\n",
                    NormalizePaths(paths)
                        //.Select(url => UrlFactory.StaticContent(url))
                        .Select(url => string.Format("<script type=\"text/javascript\" src=\"{0}\"></script>", url))
                    );
            }
            else
            {
                var tag = GetTag(paths, AssetType.JavaScript);
                var relativePath = UrlFromTag(tag, AssetType.JavaScript);

                return string.Format("<script type=\"text/javascript\" src=\"{0}\"></script>", relativePath);
            }
        }

        private static readonly ConcurrentDictionary<string, List<string>> BundleMap = new ConcurrentDictionary<string, List<string>>();

        public static void RegisterBundle(string name, IEnumerable<string> paths)
        {
            var lowerPaths = NormalizePaths(paths);

            BundleMap.TryAdd(name, lowerPaths);
        }

        public static void RegisterBundle(string name, params string[] paths)
        {
            RegisterBundle(name, paths.AsEnumerable());
        }


    }
}
