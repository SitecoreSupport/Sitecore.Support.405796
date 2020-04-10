using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sitecore.Support.ContentTesting.ViewModel
{
    public class ExecutedTestViewModel: Sitecore.ContentTesting.ViewModel.ExecutedTestViewModel
    {
        public string SiteName { get; set; }
    }
}