﻿using System;
using System.Collections.Generic;
using System.Web.Optimization;

namespace AspNetBundling
{
    /// <summary>
    /// A BundleBuilder that wil render the Content added to a Bundle at runtime. For Internal usage only
    /// </summary>
    internal class AdHocBundleBuilder : IBundleBuilder
    {
        public string BuildBundleContent(Bundle bundle, BundleContext context, IEnumerable<BundleFile> files)
        {
            var adHocBundle = bundle as AdHocBundle;
            if (adHocBundle == null)
            {
                throw new ArgumentException(String.Format("The AdHocBundleBuilder is only meant to be used by an AdHocBundle. This one is called from a bundle of typy '{0}'", bundle.GetType()), "bundle");
            }
            return adHocBundle.Content;
        }
    }
}
