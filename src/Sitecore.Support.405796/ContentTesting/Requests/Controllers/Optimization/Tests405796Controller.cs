using Sitecore.ContentTesting;
using Sitecore.ContentTesting.Data;
using Sitecore.ContentTesting.Extensions;
using Sitecore.ContentTesting.Helpers;
using Sitecore.ContentTesting.Model.Data.Items;
using Sitecore.ContentTesting.ViewModel;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Links;
using Sitecore.Web.Http.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Results;

namespace Sitecore.Support.ContentTesting.Requests.Controllers.Optimization
{
    [Authorize]
    [ValidateHttpAntiForgeryToken]
    public class Tests405796Controller : Sitecore.ContentTesting.Requests.Controllers.Optimization.TestsController
    {
        private const string DateFormat = "dd-MMM-yyyy";

        private readonly IContentTestStore _contentTestStore;

        public Tests405796Controller()
            : this(ContentTestingFactory.Instance.ContentTestStore)
        {
        }

        public Tests405796Controller(IContentTestStore contentTestStore) : base(contentTestStore)
        {
            this._contentTestStore = contentTestStore;
        }

        private static int GetEstimatedDurationDays(Item hostItem, int experienceCount, TestDefinitionItem testDef)
        {
            var deviceName = string.Empty;
            if (testDef.Device.TargetItem != null)
            {
                deviceName = testDef.Device.TargetItem.Name;
            }

            var estimator = ContentTestingFactory.Instance.GetTestRunEstimator(testDef.Language, deviceName);
            estimator.HostItem = hostItem;

            var result = estimator.GetEstimate(experienceCount, Sitecore.ContentTesting.Constants.RequiredStatisticalPower,
              testDef.TrafficAllocationPercentage, testDef.ConfidenceLevelPercentage, testDef);

            var endDate = testDef.StartDate.AddDays(result.EstimatedDayCount.HasValue ? (double)result.EstimatedDayCount : 0);
            var calculatedDays = (int)Math.Ceiling((endDate - DateTime.UtcNow).TotalDays);
            var testRunningDays = DateTime.UtcNow - testDef.StartDate;
            if (calculatedDays < 1)
            {
                return int.Parse(testDef.MaxDuration);
            }

            calculatedDays = Math.Min(calculatedDays, int.Parse(testDef.MaxDuration) - testRunningDays.Days);
            calculatedDays = Math.Max(calculatedDays, int.Parse(testDef.MinDuration) - testRunningDays.Days);

            return calculatedDays;
        }

        private double GetWinningEffect([NotNull] ITestConfiguration test)
        {
            var performance = PerformanceFactory.GetPerformanceForTest(test);
            if (performance.BestExperiencePerformance != null)
            {
                return performance.GetExperienceEffect(performance.BestExperiencePerformance.Combination);
            }

            return 0;
        }

        [HttpGet]
        public JsonResult<TestListViewModel> GetActiveTestsWithScSiteParam(int? page = null, int? pageSize = null, string hostItemId = null, string searchText = null)
        {
            page = page ?? 1;
            pageSize = pageSize ?? 20;

            DataUri dataUri = null;

            if (!string.IsNullOrEmpty(hostItemId))
            {
                dataUri = DataUriParser.Parse(hostItemId);
            }

            var tests = ContentTestStore.GetActiveTests(dataUri, searchText).ToArray();

            var results = new List<ExecutedTestViewModel>();
            var parsedTests = new Dictionary<ID, ITestConfiguration>();

            foreach (var test in tests)
            {
                var testItem = Database.GetItem(test.Uri);
                if (testItem == null)
                {
                    continue;
                }

                var testDefItem = TestDefinitionItem.Create(testItem);
                if (testDefItem == null)
                {
                    continue;
                }

                var hostItem = test.HostItemUri != null ? testItem.Database.GetItem(test.HostItemUri) : null;
                if (hostItem == null)
                {
                    continue;
                }

                var testConfiguration = _contentTestStore.LoadTestForItem(hostItem, testDefItem);
                if (testConfiguration == null)
                {
                    continue;
                }

                parsedTests.Add(testConfiguration.TestDefinitionItem.ID, testConfiguration);

                var siteInfo = LinkManager.GetPreviewSiteContext(testItem);

                results.Add(new Sitecore.Support.ContentTesting.ViewModel.ExecutedTestViewModel
                {
                    HostPageId = hostItem.ID.ToString(),
                    HostPageUri = hostItem.Uri.ToDataUri(),
                    HostPageName = hostItem.DisplayName,
                    DeviceId = testConfiguration.DeviceId.ToString(),
                    DeviceName = testConfiguration.DeviceName,
                    Language = testConfiguration.LanguageName,
                    CreatedBy = FormattingHelper.GetFriendlyUserName(testItem.Security.GetOwner()),
                    Date = DateUtil.ToServerTime(testDefItem.StartDate).ToString(DateFormat),
                    ExperienceCount = testConfiguration.TestSet.GetExperienceCount(),
                    Days = GetEstimatedDurationDays(hostItem, testConfiguration.TestSet.GetExperienceCount(), testDefItem),
                    ItemId = testDefItem.ID.ToString(),
                    ContentOnly = testConfiguration.TestSet.Variables.Count == testDefItem.PageLevelTestVariables.Count,
                    TestType = testConfiguration.TestType,
                    TestId = testConfiguration.TestDefinitionItem.ID,
                    SiteName = siteInfo.Name
                });
            }

            results = results.OrderBy(x => x.Days).Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToList();

            foreach (var result in results)
            {
                var host = Database.GetItem(result.HostPageUri);
                if (host == null)
                {
                    continue;
                }

                result.Effect = GetWinningEffect(parsedTests[result.TestId]);

                if (result.Effect < 0)
                {
                    result.EffectCss = "value-decrease";
                }
                else if (result.Effect == 0)
                {
                    result.EffectCss = "value-nochange";
                }
                else
                {
                    result.EffectCss = "value-increase";
                }
            }

            return Json(new TestListViewModel
            {
                Items = results,
                TotalResults = tests.Count()
            });
        }
    }
}