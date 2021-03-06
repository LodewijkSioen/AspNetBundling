﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Optimization;
using Microsoft.Ajax.Utilities;

namespace AspNetBundling
{
    /// <summary>
    /// Represents a custom AjaxMin bundle builder for bundling from individual file contents.
    /// Generates source map files for JS.
    /// </summary>
    public class ScriptWithSourceMapBundleBuilder : IBundleBuilder
    {
        public string BuildBundleContent(Bundle bundle, BundleContext context, IEnumerable<BundleFile> files)
        {
            if (files == null)
            {
                return string.Empty;
            }
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            if (bundle == null)
            {
                throw new ArgumentNullException("bundle");
            }

            // Generates source map using an approach documented here: http://ajaxmin.codeplex.com/discussions/446616
            var sourcePath = VirtualPathUtility.ToAbsolute(bundle.Path);
            var mapVirtualPath = string.Concat(bundle.Path, ".map");
            var mapPath = VirtualPathUtility.ToAbsolute(mapVirtualPath);

            // Concatenate file contents to be minified, including the sourcemap hints
            var contentConcatedString = GetContentConcated(context, files);

            // Try minify (+ source map) using AjaxMin dll
            try
            {
                var contentBuilder = new StringBuilder();
                var mapBuilder = new StringBuilder();
                using (var contentWriter = new StringWriter(contentBuilder))
                using (var mapWriter = new StringWriter(mapBuilder))
                using (var sourceMap = new V3SourceMap(mapWriter))
                {
                    var settings = new CodeSettings()
                    {
                        EvalTreatment = EvalTreatment.MakeImmediateSafe,
                        PreserveImportantComments = false,
                        SymbolsMap = sourceMap,
                        TermSemicolons = true
                    };

                    sourceMap.StartPackage(sourcePath, mapPath);

                    var minifier = new Minifier();
                    string contentMinified = minifier.MinifyJavaScript(contentConcatedString, settings);
                    if (minifier.ErrorList.Count > 0)
                    {
                        return GenerateMinifierErrorsContent(contentConcatedString, minifier);
                    }

                    contentWriter.Write(contentMinified);

                    sourceMap.EndPackage();
                    sourceMap.EndFile(contentWriter, "\r\n");
                }
                
                contentBuilder.Replace("//@ sourceMappingURL=", "//# sourceMappingURL=");

                //Write the SourceMap to another Bundle
                AddContentToAdHocBundle(context, mapVirtualPath, mapBuilder.ToString());

                return contentBuilder.ToString();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("An exception occurred trying to build bundle contents for bundle with virtual path: " + bundle.Path + ". See Exception details.", ex, typeof(ScriptWithSourceMapBundleBuilder));
                return GenerateGenericErrorsContent(contentConcatedString);
            }
        }

        private static string GenerateGenericErrorsContent(string contentConcatedString)
        {
            var sbContent = new StringBuilder();
            sbContent.Append("/* ");
            sbContent.Append("An error occurred during minification, see Trace log for more details - returning concatenated content unminified.").Append("\r\n");
            sbContent.Append(" */\r\n");
            sbContent.Append(contentConcatedString);
            return sbContent.ToString();
        }

        private static string GenerateMinifierErrorsContent(string contentConcatedString, Minifier minifier)
        {
            var sbContent = new StringBuilder();
            sbContent.Append("/* ");
            sbContent.Append("An error occurred during minification, see errors below - returning concatenated content unminified.").Append("\r\n");
            foreach (var error in minifier.ErrorList)
            {
                sbContent.Append(error).Append("\r\n");
            }
            sbContent.Append(" */\r\n");
            sbContent.Append(contentConcatedString);
            return sbContent.ToString();
        }

        private static string GetContentConcated(BundleContext context, IEnumerable<BundleFile> files)
        {
            var contentConcated = new StringBuilder();

            foreach (var file in files)
            {
                // Get the contents of the bundle,
                // noting it may have transforms applied that could mess with any source mapping we want to do
                var contents = file.ApplyTransforms();

                // If there were transforms that were applied
                if (file.Transforms.Count > 0)
                {
                    // Write the transformed contents to another Bundle
                    var fileVirtualPath = file.IncludedVirtualPath;
                    var virtualPathTransformed = "~/" + Path.ChangeExtension(fileVirtualPath, string.Concat(".transformed",  Path.GetExtension(fileVirtualPath)));
                    AddContentToAdHocBundle(context, virtualPathTransformed, contents);
                }

                // Source header line then source code
                contentConcated.AppendLine("///#source 1 1 " + file.VirtualFile.VirtualPath);
                contentConcated.AppendLine(contents);
            }

            return contentConcated.ToString();
        }

        private static void AddContentToAdHocBundle(BundleContext context, string virtualPath, string content)
        {
            var mapBundle = context.BundleCollection.GetBundleFor(virtualPath);
            if (mapBundle == null)
            {
                mapBundle = new AdHocBundle(virtualPath);
            }
            var correctlyCastMapBundle = mapBundle as AdHocBundle;
            if (correctlyCastMapBundle == null)
            {
                throw new InvalidOperationException(string.Format("There is a bundle on the VirtualPath '{0}' of the type '{1}' when it was expected to be of the type 'SourceMapBundle'. That Virtual Path is reserved for the SourceMaps.", virtualPath, mapBundle.GetType()));
            }
            correctlyCastMapBundle.SetContent(content);
        }
    }
}
